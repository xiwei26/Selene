import Foundation

@MainActor
final class SSESearchClient {
    struct SearchProgress: Equatable {
        var totalSources: Int = 0
        var completedSources: Int = 0
        var currentSource: String?
        var isComplete: Bool = false
        var error: String?

        var progressPercentage: Double {
            guard totalSources > 0 else { return isComplete ? 1 : 0 }
            return min(max(Double(completedSources) / Double(totalSources), 0), 1)
        }
    }

    let incrementalResults: AsyncStream<[SearchResult]>
    let progress: AsyncStream<SearchProgress>
    let errors: AsyncStream<String>

    private var resultsContinuation: AsyncStream<[SearchResult]>.Continuation?
    private var progressContinuation: AsyncStream<SearchProgress>.Continuation?
    private var errorsContinuation: AsyncStream<String>.Continuation?
    private var task: Task<Void, Never>?

    init() {
        var resultsContinuation: AsyncStream<[SearchResult]>.Continuation?
        incrementalResults = AsyncStream { continuation in
            resultsContinuation = continuation
        }
        self.resultsContinuation = resultsContinuation

        var progressContinuation: AsyncStream<SearchProgress>.Continuation?
        progress = AsyncStream { continuation in
            progressContinuation = continuation
        }
        self.progressContinuation = progressContinuation

        var errorsContinuation: AsyncStream<String>.Continuation?
        errors = AsyncStream { continuation in
            errorsContinuation = continuation
        }
        self.errorsContinuation = errorsContinuation
    }

    func startSearch(query: String, serverURL: URL, cookie: String) async {
        stopSearch()

        task = Task { [weak self] in
            guard let self else { return }
            do {
                var components = URLComponents(
                    url: serverURL.appendingPathComponent("/api/search/ws"),
                    resolvingAgainstBaseURL: false
                )
                components?.queryItems = [URLQueryItem(name: "q", value: query)]
                guard let url = components?.url else { throw APIError.invalidURL }

                var request = URLRequest(url: url, timeoutInterval: 15)
                request.setValue("text/event-stream", forHTTPHeaderField: "Accept")
                request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")
                if !cookie.isEmpty {
                    request.setValue(cookie, forHTTPHeaderField: "Cookie")
                }

                let (bytes, response) = try await URLSession.shared.bytes(for: request)
                guard let httpResponse = response as? HTTPURLResponse,
                      (200...299).contains(httpResponse.statusCode) else {
                    throw APIError.sseConnectionFailed
                }

                var eventLines: [String] = []
                var currentProgress = SearchProgress()

                for try await line in bytes.lines {
                    if Task.isCancelled { return }
                    if line.isEmpty {
                        if let event = Self.parseEvent(lines: eventLines) {
                            currentProgress = self.handle(event: event, currentProgress: currentProgress)
                            if currentProgress.isComplete { break }
                        }
                        eventLines.removeAll()
                    } else {
                        eventLines.append(line)
                    }
                }
            } catch {
                let message = error.localizedDescription
                self.errorsContinuation?.yield(message)
                self.progressContinuation?.yield(SearchProgress(isComplete: true, error: message))
            }
        }
    }

    func stopSearch() {
        task?.cancel()
        task = nil
    }

    static func parseEvent(lines: [String]) -> (type: String, data: [String: Any])? {
        var eventType = "message"
        var dataLines: [String] = []

        for line in lines {
            if line.hasPrefix("event:") {
                eventType = String(line.dropFirst("event:".count)).trimmingCharacters(in: .whitespaces)
            } else if line.hasPrefix("data:") {
                dataLines.append(String(line.dropFirst("data:".count)).trimmingCharacters(in: .whitespaces))
            }
        }

        guard !dataLines.isEmpty,
              let data = dataLines.joined(separator: "\n").data(using: .utf8),
              let payload = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }

        return (eventType, payload)
    }

    func handle(
        event: (type: String, data: [String: Any]),
        currentProgress: SearchProgress
    ) -> SearchProgress {
        var next = currentProgress

        switch event.type {
        case "start":
            next.totalSources = intValue(event.data["totalSources"] ?? event.data["total_sources"]) ?? 0
            next.completedSources = 0
            next.currentSource = nil
            next.error = nil
            next.isComplete = false
            progressContinuation?.yield(next)
        case "sourceResult":
            next.completedSources += 1
            next.currentSource = event.data["sourceName"] as? String ?? event.data["source_name"] as? String
            next.error = nil
            if let rawResults = event.data["results"] as? [[String: Any]] {
                let results = rawResults.compactMap { raw -> SearchResult? in
                    guard let data = try? JSONSerialization.data(withJSONObject: raw) else { return nil }
                    return try? JSONDecoder().decode(SearchResult.self, from: data)
                }
                resultsContinuation?.yield(results)
            }
            progressContinuation?.yield(next)
        case "sourceError":
            next.completedSources += 1
            next.currentSource = event.data["sourceName"] as? String ?? event.data["source_name"] as? String
            next.error = event.data["error"] as? String
            if let error = next.error {
                errorsContinuation?.yield(error)
            }
            progressContinuation?.yield(next)
        case "complete":
            next.isComplete = true
            next.error = nil
            next.completedSources = max(next.completedSources, next.totalSources)
            progressContinuation?.yield(next)
        default:
            break
        }

        return next
    }
}
