import Foundation
import Observation

struct SearchAggregate: Identifiable {
    let key: String
    let representative: SearchResult
    let sourceNames: [String]

    var id: String { key }
}

/// A cancelled `URLSession` task throws `URLError.cancelled`, not
/// `CancellationError`, so `catch is CancellationError` alone lets superseded
/// requests fall through and clobber newer state.
func isCancellation(_ error: Error?) -> Bool {
    if Task.isCancelled { return true }
    guard let error else { return false }
    if error is CancellationError { return true }
    return (error as? URLError)?.code == .cancelled
}

enum LoadPhase: Equatable {
    case idle
    case loading
    case loaded
    case failed(String)

    var isLoading: Bool {
        self == .loading
    }

    var failureMessage: String? {
        if case .failed(let message) = self { return message }
        return nil
    }
}

@MainActor
@Observable
final class HomeStore {
    var movies: [DoubanMovie] = []
    var tvShows: [DoubanMovie] = []
    var anime: [DoubanMovie] = []
    var shows: [DoubanMovie] = []
    var bangumi: [BangumiItem] = []
    /// Non-fatal per-section failures when other sections still returned data.
    var sectionWarnings: [String] = []
    var phase: LoadPhase = .idle

    func load(using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }

        phase = .loading
        sectionWarnings = []

        // Fire all five in parallel, then settle each independently so one
        // failing Douban route does not blank the whole home screen.
        async let movieRequest = client.category(.movie)
        async let tvRequest = client.category(.tv)
        async let animeRequest = client.category(.anime)
        async let showRequest = client.category(.show)
        async let bangumiRequest = client.bangumiCalendar()

        var warnings: [String] = []

        do {
            movies = try await movieRequest
        } catch {
            movies = []
            if !isCancellation(error) {
                warnings.append("电影：\(error.localizedDescription)")
            }
        }
        do {
            tvShows = try await tvRequest
        } catch {
            tvShows = []
            if !isCancellation(error) {
                warnings.append("剧集：\(error.localizedDescription)")
            }
        }
        do {
            anime = try await animeRequest
        } catch {
            anime = []
            if !isCancellation(error) {
                warnings.append("动漫：\(error.localizedDescription)")
            }
        }
        do {
            shows = try await showRequest
        } catch {
            shows = []
            if !isCancellation(error) {
                warnings.append("综艺：\(error.localizedDescription)")
            }
        }
        do {
            bangumi = try await bangumiRequest
        } catch {
            bangumi = []
            if !isCancellation(error) {
                warnings.append("番组：\(error.localizedDescription)")
            }
        }

        sectionWarnings = warnings

        if [movies, tvShows, anime, shows].allSatisfy(\.isEmpty) {
            let detail = sectionWarnings.first ?? "暂时没有加载到内容，请检查服务器的 Douban 接口。"
            phase = .failed(detail)
        } else {
            phase = .loaded
        }
    }
}

@MainActor
@Observable
final class CategoryStore {
    var items: [DoubanMovie] = []
    var phase: LoadPhase = .idle

    func load(category: MediaCategory, using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }
        phase = .loading

        do {
            items = try await client.category(category)
            phase = .loaded
        } catch {
            phase = isCancellation(error) ? .idle : .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class MediaDetailStore {
    var details: DoubanMovieDetails?
    var phase: LoadPhase = .idle

    func load(id: String, using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }
        phase = .loading
        do {
            details = try await client.doubanDetails(id: id)
            phase = .loaded
        } catch {
            phase = isCancellation(error) ? .idle : .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class SearchStore {
    var query = ""
    var submittedQuery = ""
    var results: [SearchResult] = []
    var suggestions: [SearchSuggestion] = []
    var completedSources = 0
    var totalSources = 0
    var currentSource: String?
    var sourceFilter: String?
    var yearFilter: String?
    var blockedKeywordsText = ""
    var isAggregating = false
    var sortNewestFirst = true
    var phase: LoadPhase = .idle
    /// Non-fatal per-source failures reported by the SSE stream.
    var sourceWarnings: [String] = []

    @ObservationIgnored private var generation = 0

    var availableSources: [String] {
        Array(Set(results.map { $0.sourceName.isEmpty ? $0.source : $0.sourceName })).sorted()
    }

    var availableYears: [String] {
        Array(Set(results.map(\.year).filter { !$0.isEmpty })).sorted(by: >)
    }

    var filteredResults: [SearchResult] {
        results
            .filter(matchesFilters)
            .sorted { sortNewestFirst ? $0.year > $1.year : $0.year < $1.year }
    }

    var filteredAggregates: [SearchAggregate] {
        let grouped = Dictionary(grouping: filteredResults) {
            "\($0.title)|\($0.year)|\($0.typeName ?? "unknown")"
        }
        return grouped.map { key, values in
            SearchAggregate(
                key: key,
                representative: values[0],
                sourceNames: Array(
                    Set(values.map { $0.sourceName.isEmpty ? $0.source : $0.sourceName })
                ).sorted()
            )
        }
        .sorted {
            sortNewestFirst
                ? $0.representative.year > $1.representative.year
                : $0.representative.year < $1.representative.year
        }
    }

    func submit() {
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalized.isEmpty else {
            submittedQuery = ""
            results = []
            phase = .idle
            return
        }
        submittedQuery = normalized
    }

    func search(
        using client: LunaTVClient,
        useStreaming: Bool,
        isLocalMode: Bool = false,
        localResources: [SearchResource] = []
    ) async {
        let query = submittedQuery
        guard !query.isEmpty else { return }

        generation += 1
        let token = generation
        results = []
        completedSources = 0
        totalSources = 0
        currentSource = nil
        sourceWarnings = []
        phase = .loading

        // Local subscription mode has no LunaTV server, so it must never fall
        // through to /api/search on the placeholder base URL.
        if isLocalMode {
            await searchLocally(query: query, token: token, client: client, resources: localResources)
            return
        }

        do {
            if useStreaming {
                let updates = await client.streamingSearch(query: query)
                for try await update in updates {
                    guard token == generation else { return }
                    appendUnique(update.results)
                    completedSources = update.completedSources
                    totalSources = update.totalSources
                    currentSource = update.currentSource
                    if let error = update.error, let source = update.currentSource {
                        sourceWarnings.append("\(source): \(error)")
                    }
                }
            } else {
                let values = try await client.search(query: query)
                guard token == generation else { return }
                results = values
            }
            guard token == generation, !Task.isCancelled else { return }
            await recordHistory(query, using: client, isLocalMode: isLocalMode)
            guard token == generation else { return }
            phase = .loaded
        } catch {
            // A superseded task must not touch state the newer one already owns.
            guard token == generation, !isCancellation(error) else { return }
            do {
                if results.isEmpty {
                    let values = try await client.search(query: query)
                    guard token == generation else { return }
                    results = values
                }
                await recordHistory(query, using: client, isLocalMode: isLocalMode)
                guard token == generation else { return }
                phase = .loaded
            } catch {
                guard token == generation, !isCancellation(error) else { return }
                phase = .failed(error.localizedDescription)
            }
        }
    }

    private func searchLocally(
        query: String,
        token: Int,
        client: LunaTVClient,
        resources: [SearchResource]
    ) async {
        let enabled = resources.filter { !$0.disabled && !$0.api.isEmpty }
        guard !enabled.isEmpty else {
            phase = .failed("当前订阅没有可用的搜索资源。")
            return
        }

        totalSources = enabled.count
        let outcome = await client.localSearch(query: query, resources: enabled)
        guard token == generation, !Task.isCancelled else { return }

        results = outcome.results
        completedSources = outcome.completedSources
        totalSources = outcome.totalSources
        sourceWarnings = outcome.warnings
        currentSource = nil

        if results.isEmpty, !outcome.warnings.isEmpty {
            phase = .failed("本地订阅资源全部搜索失败，请稍后重试。")
        } else {
            phase = .loaded
        }
    }

    private func recordHistory(_ query: String, using client: LunaTVClient, isLocalMode: Bool) async {
        guard !isLocalMode else { return }
        try? await client.addSearchHistory(query)
    }

    func loadSuggestions(using client: LunaTVClient, isLocalMode: Bool = false) async {
        // `/api/search/suggestions` does not exist in local subscription mode.
        guard !isLocalMode else {
            suggestions = []
            return
        }
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard normalized.count >= 2 else {
            suggestions = []
            return
        }
        try? await Task.sleep(for: .milliseconds(350))
        guard !Task.isCancelled else { return }
        // Only assign on success: a cancelled request would otherwise blank the
        // menu on every keystroke.
        guard let values = try? await client.suggestions(query: normalized),
              !Task.isCancelled else { return }
        suggestions = values
    }

    private func appendUnique(_ batch: [SearchResult]) {
        var existing = Set(results.map { "\($0.source)|\($0.id)" })
        for result in batch where existing.insert("\(result.source)|\(result.id)").inserted {
            results.append(result)
        }
    }

    func clearFilters() {
        sourceFilter = nil
        yearFilter = nil
        blockedKeywordsText = ""
    }

    private func matchesFilters(_ result: SearchResult) -> Bool {
        if let sourceFilter {
            let sourceName = result.sourceName.isEmpty ? result.source : result.sourceName
            guard sourceName == sourceFilter || result.source == sourceFilter else { return false }
        }
        if let yearFilter, result.year != yearFilter {
            return false
        }
        let keywords = blockedKeywordsText
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
            .filter { !$0.isEmpty }
        let searchableText = [
            result.title,
            result.description ?? "",
            result.sourceName,
            result.typeName ?? ""
        ]
        .joined(separator: " ")
        .lowercased()
        return !keywords.contains { searchableText.contains($0) }
    }
}

@MainActor
@Observable
final class PlayableResourceStore {
    var resources: [SearchResult] = []
    var phase: LoadPhase = .idle

    func find(title: String, using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }
        phase = .loading
        do {
            resources = try await client.search(query: title)
            phase = .loaded
        } catch {
            phase = isCancellation(error) ? .idle : .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class SearchDetailStore {
    var detail: SearchResult?
    var alternatives: [SearchResult] = []
    var phase: LoadPhase = .idle

    func load(result: SearchResult, using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }
        phase = .loading

        // A local row carries its own MacCMS endpoint, so detail loading stays
        // off /api/detail and /api/search.
        if result.isLocal {
            await loadLocal(result: result, using: client)
            return
        }

        do {
            async let detailRequest = client.detail(source: result.source, id: result.id)
            async let alternativesRequest = client.search(query: result.title)
            detail = try await detailRequest
            alternatives = (try? await alternativesRequest) ?? [result]
            phase = .loaded
        } catch {
            guard !isCancellation(error) else {
                phase = .idle
                return
            }
            detail = result
            phase = .failed(error.localizedDescription)
        }
    }

    private func loadLocal(result: SearchResult, using client: LunaTVClient) async {
        alternatives = [result]
        do {
            detail = try await client.localDetail(result: result)
            phase = .loaded
        } catch {
            guard !isCancellation(error) else {
                phase = .idle
                return
            }
            // The search row is already playable, so a failed detail refresh
            // degrades to the row instead of blocking playback.
            detail = result
            phase = result.episodes.isEmpty
                ? .failed(error.localizedDescription)
                : .loaded
        }
    }
}
