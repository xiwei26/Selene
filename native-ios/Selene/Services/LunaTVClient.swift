import Foundation

struct SearchStreamUpdate: Sendable {
    let results: [SearchResult]
    let completedSources: Int
    let totalSources: Int
    let currentSource: String?
    let isComplete: Bool
    let error: String?
}

actor LunaTVClient {
    let baseURL: URL
    private let cookie: String
    private let urlSession: URLSession
    private let decoder = JSONDecoder()

    init(baseURL: URL, cookie: String = "", urlSession: URLSession = .shared) {
        self.baseURL = baseURL
        self.cookie = cookie
        self.urlSession = urlSession
    }

    func login(username: String, password: String) async throws -> UserSession {
        var request = try makeRequest(path: "/api/login", method: "POST")
        request.httpBody = try JSONSerialization.data(withJSONObject: [
            "username": username,
            "password": password
        ])

        let (_, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }

        let sessionCookie = httpResponse.value(forHTTPHeaderField: "Set-Cookie")?
            .split(separator: ";", maxSplits: 1)
            .first
            .map(String.init) ?? ""

        return UserSession(
            serverURL: baseURL,
            username: username,
            cookie: sessionCookie
        )
    }

    func category(_ category: MediaCategory) async throws -> [DoubanMovie] {
        let request = try makeRequest(
            path: "/api/douban/categories",
            queryItems: category.lunaQuery
        )
        let data = try await perform(request)

        if let list = try? decoder.decode([DoubanMovie].self, from: data) {
            return list
        }
        if let envelope = try? decoder.decode(DoubanCategoryEnvelope.self, from: data) {
            return envelope.list
        }
        if let envelope = try? decoder.decode(DataEnvelope<[DoubanMovie]>.self, from: data) {
            return envelope.data
        }
        throw APIError.decodingFailed
    }

    func doubanDetails(id: String) async throws -> DoubanMovieDetails {
        let request = try makeRequest(
            path: "/api/douban/details",
            queryItems: [URLQueryItem(name: "id", value: id)]
        )
        let data = try await perform(request)
        if let envelope = try? decoder.decode(DataEnvelope<DoubanMovieDetails>.self, from: data) {
            return envelope.data
        }
        if let value = try? decoder.decode(DoubanMovieDetails.self, from: data) {
            return value
        }
        throw APIError.decodingFailed
    }

    func search(query: String) async throws -> [SearchResult] {
        let request = try makeRequest(
            path: "/api/search",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
        let data = try await perform(request)

        if let results = try? decoder.decode([SearchResult].self, from: data) {
            return results
        }
        if let envelope = try? decoder.decode(SearchEnvelope.self, from: data) {
            return envelope.results
        }
        if let envelope = try? decoder.decode(DataEnvelope<[SearchResult]>.self, from: data) {
            return envelope.data
        }
        throw APIError.decodingFailed
    }

    func streamingSearch(query: String) -> AsyncThrowingStream<SearchStreamUpdate, Error> {
        let baseURL = self.baseURL
        let cookie = self.cookie
        let urlSession = self.urlSession

        return AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    guard var components = URLComponents(
                        url: baseURL.appendingPathComponent("api/search/ws"),
                        resolvingAgainstBaseURL: false
                    ) else {
                        throw APIError.invalidServerURL
                    }
                    components.queryItems = [URLQueryItem(name: "q", value: query)]
                    guard let url = components.url else { throw APIError.invalidServerURL }

                    var request = URLRequest(url: url, timeoutInterval: 30)
                    request.setValue("text/event-stream", forHTTPHeaderField: "Accept")
                    request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")
                    if !cookie.isEmpty {
                        request.setValue(cookie, forHTTPHeaderField: "Cookie")
                    }

                    let (bytes, response) = try await urlSession.bytes(for: request)
                    guard let response = response as? HTTPURLResponse,
                          (200...299).contains(response.statusCode) else {
                        throw APIError.invalidResponse
                    }

                    var lines: [String] = []
                    var total = 0
                    var completed = 0

                    for try await line in bytes.lines {
                        try Task.checkCancellation()
                        if line.isEmpty {
                            guard let event = Self.parseSSE(lines) else {
                                lines.removeAll(keepingCapacity: true)
                                continue
                            }
                            lines.removeAll(keepingCapacity: true)
                            let type = event.type
                            switch type {
                            case "start":
                                total = flexibleInt(event.data["totalSources"] ?? event.data["total_sources"]) ?? 0
                                continuation.yield(
                                    SearchStreamUpdate(
                                        results: [],
                                        completedSources: 0,
                                        totalSources: total,
                                        currentSource: nil,
                                        isComplete: false,
                                        error: nil
                                    )
                                )
                            case "source_result":
                                completed += 1
                                let results = Self.decodeSearchResults(event.data["results"])
                                continuation.yield(
                                    SearchStreamUpdate(
                                        results: results,
                                        completedSources: completed,
                                        totalSources: total,
                                        currentSource: event.data["sourceName"] as? String ?? event.data["source_name"] as? String,
                                        isComplete: false,
                                        error: nil
                                    )
                                )
                            case "source_error":
                                completed += 1
                                continuation.yield(
                                    SearchStreamUpdate(
                                        results: [],
                                        completedSources: completed,
                                        totalSources: total,
                                        currentSource: event.data["sourceName"] as? String ?? event.data["source_name"] as? String,
                                        isComplete: false,
                                        error: event.data["error"] as? String
                                    )
                                )
                            case "complete":
                                continuation.yield(
                                    SearchStreamUpdate(
                                        results: [],
                                        completedSources: max(completed, total),
                                        totalSources: total,
                                        currentSource: nil,
                                        isComplete: true,
                                        error: nil
                                    )
                                )
                                continuation.finish()
                                return
                            default:
                                break
                            }
                        } else {
                            lines.append(line)
                        }
                    }
                    continuation.finish()
                } catch {
                    continuation.finish(throwing: error)
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }

    func detail(source: String, id: String) async throws -> SearchResult {
        let request = try makeRequest(
            path: "/api/detail",
            queryItems: [
                URLQueryItem(name: "source", value: source),
                URLQueryItem(name: "id", value: id)
            ]
        )
        let data = try await perform(request)

        if let envelope = try? decoder.decode(DataEnvelope<SearchResult>.self, from: data) {
            return envelope.data
        }
        if let envelope = try? decoder.decode(DetailEnvelope.self, from: data),
           let detail = envelope.result {
            return detail
        }
        if let detail = try? decoder.decode(SearchResult.self, from: data) {
            return detail
        }
        throw APIError.decodingFailed
    }

    func searchResources() async throws -> [SearchResource] {
        let data = try await perform(try makeRequest(path: "/api/search/resources"))
        if let values = try? decoder.decode([SearchResource].self, from: data) { return values }
        if let wrapped = try? decoder.decode(DataEnvelope<[SearchResource]>.self, from: data) { return wrapped.data }
        return []
    }

    func favorites() async throws -> [FavoriteItem] {
        let data = try await perform(try makeRequest(path: "/api/favorites"))
        let object = try jsonObject(data)
        let map = keyedMap(object, preferredKey: "favorites")
        return map.map { FavoriteItem.fromJSON(key: $0.key, data: $0.value) }
            .sorted { $0.saveTime > $1.saveTime }
    }

    func addFavorite(_ result: SearchResult) async throws {
        let favorite = FavoriteItem(
            id: "\(result.source)+\(result.id)",
            source: result.source,
            title: result.title,
            sourceName: result.sourceName,
            year: result.year,
            cover: result.poster,
            totalEpisodes: result.episodes.count,
            saveTime: Int64(Date().timeIntervalSince1970 * 1_000),
            searchTitle: result.title,
            origin: "vod",
            type: result.typeName,
            releaseDate: nil,
            remarks: result.remarks
        )
        _ = try await performJSON(
            path: "/api/favorites",
            method: "POST",
            body: ["key": favorite.id, "favorite": favorite.jsonValue()]
        )
    }

    func removeFavorite(source: String, id: String) async throws {
        _ = try await perform(
            try makeRequest(
                path: "/api/favorites",
                method: "DELETE",
                queryItems: [URLQueryItem(name: "key", value: "\(source)+\(id)")]
            )
        )
    }

    func playRecords() async throws -> [PlayRecord] {
        let data = try await perform(try makeRequest(path: "/api/playrecords"))
        let map = keyedMap(try jsonObject(data), preferredKey: "records")
        return map.map { PlayRecord.fromJSON(key: $0.key, data: $0.value) }
            .sorted { $0.saveTime > $1.saveTime }
    }

    func savePlayRecord(_ record: PlayRecord) async throws {
        _ = try await performJSON(
            path: "/api/playrecords",
            method: "POST",
            body: ["key": record.id, "record": record.jsonValue()]
        )
    }

    func clearPlayRecords() async throws {
        _ = try await perform(try makeRequest(path: "/api/playrecords", method: "DELETE"))
    }

    func searchHistory() async throws -> [String] {
        let data = try await perform(try makeRequest(path: "/api/searchhistory"))
        if let values = try? decoder.decode([String].self, from: data) { return values }
        let object = try jsonObject(data)
        guard let dictionary = object as? [String: Any] else { return [] }
        return dictionary["history"] as? [String] ?? dictionary["keywords"] as? [String] ?? []
    }

    func addSearchHistory(_ query: String) async throws {
        _ = try await performJSON(
            path: "/api/searchhistory",
            method: "POST",
            body: ["keyword": query]
        )
    }

    func deleteSearchHistory(_ query: String) async throws {
        _ = try await perform(
            try makeRequest(
                path: "/api/searchhistory",
                method: "DELETE",
                queryItems: [URLQueryItem(name: "keyword", value: query)]
            )
        )
    }

    func clearSearchHistory() async throws {
        _ = try await perform(try makeRequest(path: "/api/searchhistory", method: "DELETE"))
    }

    func suggestions(query: String) async throws -> [SearchSuggestion] {
        let data = try await perform(
            try makeRequest(
                path: "/api/search/suggestions",
                queryItems: [URLQueryItem(name: "q", value: query)]
            )
        )
        if let values = try? decoder.decode([SearchSuggestion].self, from: data) { return values }
        if let wrapped = try? decoder.decode(SuggestionEnvelope.self, from: data) { return wrapped.suggestions }
        return []
    }

    func liveSources() async throws -> [LiveSource] {
        let data = try await perform(try makeRequest(path: "/api/live/sources"))
        if let values = try? decoder.decode([LiveSource].self, from: data) { return values }
        if let wrapped = try? decoder.decode(DataEnvelope<[LiveSource]>.self, from: data) { return wrapped.data }
        if let wrapped = try? decoder.decode(LiveSourcesEnvelope.self, from: data) { return wrapped.sources }
        return []
    }

    func liveChannels(source: String) async throws -> [LiveChannel] {
        let data = try await perform(
            try makeRequest(
                path: "/api/live/channels",
                queryItems: [URLQueryItem(name: "source", value: source)]
            )
        )
        if let values = try? decoder.decode([LiveChannel].self, from: data) { return values }
        if let wrapped = try? decoder.decode(DataEnvelope<[LiveChannel]>.self, from: data) { return wrapped.data }
        if let wrapped = try? decoder.decode(LiveChannelsEnvelope.self, from: data) { return wrapped.channels }
        return []
    }

    func liveEPG(tvgID: String, source: String) async throws -> EPGData? {
        let data = try await perform(
            try makeRequest(
                path: "/api/live/epg",
                queryItems: [
                    URLQueryItem(name: "tvgId", value: tvgID),
                    URLQueryItem(name: "source", value: source)
                ]
            )
        )
        if let value = try? decoder.decode(DataEnvelope<EPGData>.self, from: data).data {
            return value
        }
        return try? decoder.decode(EPGData.self, from: data)
    }

    func recommendedShortDramas() async throws -> [SearchResult] {
        try await shortDramaList(
            path: "/api/shortdrama/recommend",
            queryItems: [URLQueryItem(name: "size", value: "30")]
        )
    }

    func searchShortDramas(_ query: String) async throws -> [SearchResult] {
        try await shortDramaList(
            path: "/api/shortdrama/search",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
    }

    func shortDramaDetail(id: String, name: String) async throws -> SearchResult? {
        let data = try await perform(
            try makeRequest(
                path: "/api/shortdrama/detail",
                queryItems: [
                    URLQueryItem(name: "id", value: id),
                    URLQueryItem(name: "episode", value: "1"),
                    URLQueryItem(name: "name", value: name)
                ]
            )
        )
        if let result = try? decoder.decode(DataEnvelope<SearchResult>.self, from: data).data {
            return result
        }
        return try? decoder.decode(SearchResult.self, from: data)
    }

    func bilibiliPopular() async throws -> [MediaPlatformItem] {
        try await platformItems(path: "/api/bilibili/popular", source: "bilibili")
    }

    func searchBilibili(_ query: String) async throws -> [MediaPlatformItem] {
        try await platformItems(
            path: "/api/bilibili/search",
            queryItems: [URLQueryItem(name: "q", value: query)],
            source: "bilibili"
        )
    }

    func youtubePopular(region: String = "US") async throws -> [MediaPlatformItem] {
        try await platformItems(
            path: "/api/youtube/popular",
            queryItems: [URLQueryItem(name: "regionCode", value: region)],
            source: "youtube"
        )
    }

    func searchYouTube(_ query: String) async throws -> [MediaPlatformItem] {
        try await platformItems(
            path: "/api/youtube/search",
            queryItems: [URLQueryItem(name: "q", value: query)],
            source: "youtube"
        )
    }

    func adminConfig() async throws -> AdminConfig? {
        let data = try await perform(try makeRequest(path: "/api/admin/config"))
        if let config = try? decoder.decode(DataEnvelope<AdminConfig>.self, from: data).data {
            return config
        }
        return try? decoder.decode(AdminConfig.self, from: data)
    }

    func saveYouTubeConfig(_ config: YouTubeAdminConfig) async throws {
        _ = try await performJSON(
            path: "/api/admin/youtube",
            method: "POST",
            body: [
                "enabled": config.enabled,
                "apiKey": config.apiKey,
                "enableDemo": config.enableDemo,
                "maxResults": config.maxResults,
                "enabledRegions": config.enabledRegions,
                "enabledCategories": config.enabledCategories
            ]
        )
    }

    func saveBilibiliConfig(enabled: Bool) async throws {
        _ = try await performJSON(
            path: "/api/admin/bilibili",
            method: "POST",
            body: ["enabled": enabled]
        )
    }

    func bangumiCalendar() async throws -> [BangumiItem] {
        var request = URLRequest(url: URL(string: "https://api.bgm.tv/calendar")!, timeoutInterval: 20)
        request.setValue("SeleneNative-iOS/1.1", forHTTPHeaderField: "User-Agent")
        let (data, response) = try await urlSession.data(for: request)
        guard let response = response as? HTTPURLResponse, (200...299).contains(response.statusCode) else {
            throw APIError.invalidResponse
        }
        let days = try decoder.decode([BangumiCalendarDay].self, from: data)
        let weekday = Calendar.current.component(.weekday, from: .now)
        return days.first { $0.weekday.id == weekday }?.items ?? days.flatMap(\.items)
    }

    private func perform(_ request: URLRequest) async throws -> Data {
        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            if httpResponse.statusCode == 403,
               let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let message = object["message"] as? String ?? object["error"] as? String {
                throw APIError.featureDisabled(message, statusCode: httpResponse.statusCode)
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }
        return data
    }

    private func performJSON(path: String, method: String, body: [String: Any]) async throws -> Data {
        var request = try makeRequest(path: path, method: method)
        request.httpBody = try JSONSerialization.data(withJSONObject: body)
        return try await perform(request)
    }

    private func jsonObject(_ data: Data) throws -> Any {
        guard !data.isEmpty else { return [:] }
        return try JSONSerialization.jsonObject(with: data)
    }

    private func keyedMap(_ object: Any, preferredKey: String) -> [String: [String: Any]] {
        if let dictionary = object as? [String: Any],
           let value = dictionary[preferredKey] as? [String: [String: Any]] {
            return value
        }
        if let dictionary = object as? [String: Any],
           let data = dictionary["data"] as? [String: Any],
           let value = data[preferredKey] as? [String: [String: Any]] {
            return value
        }
        if let value = object as? [String: [String: Any]] { return value }
        return [:]
    }

    private func shortDramaList(path: String, queryItems: [URLQueryItem]) async throws -> [SearchResult] {
        let data = try await perform(try makeRequest(path: path, queryItems: queryItems))
        if let values = try? decoder.decode([SearchResult].self, from: data) { return values }
        for key in ["data", "list", "results", "items"] {
            if let values: [SearchResult] = decodeArray(data, key: key) { return values }
        }
        return []
    }

    private func platformItems(
        path: String,
        queryItems: [URLQueryItem] = [],
        source: String
    ) async throws -> [MediaPlatformItem] {
        let data = try await perform(try makeRequest(path: path, queryItems: queryItems))
        if let values = try? decoder.decode([MediaPlatformItem].self, from: data) { return values }
        let rows = wrappedRows(try jsonObject(data), keys: ["data", "items", "results", "videos"])
        return rows.compactMap { row in
            let snippet = row["snippet"] as? [String: Any] ?? [:]
            let title = string(row, ["title", "name"]).nonEmpty ?? string(snippet, ["title"])
            guard !title.isEmpty else { return nil }
            return MediaPlatformItem(
                id: string(row, ["id", "videoId", "bvid", "aid"]),
                title: title,
                cover: string(row, ["cover", "pic", "thumbnail", "poster"]),
                author: string(row, ["author", "owner", "channelTitle", "up"]),
                description: string(row, ["description", "desc"]).nonEmpty ?? string(snippet, ["description"]),
                duration: string(row, ["duration", "length"]),
                source: string(row, ["source"]).nonEmpty ?? source,
                url: string(row, ["url", "webpage_url", "link"])
            )
        }
    }

    private func decodeArray<Value: Decodable>(_ data: Data, key: String) -> [Value]? {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let value = object[key],
              JSONSerialization.isValidJSONObject(value),
              let payload = try? JSONSerialization.data(withJSONObject: value) else {
            return nil
        }
        if let values = try? decoder.decode([Value].self, from: payload) { return values }
        if let nested = value as? [String: Any] {
            for nestedKey in ["list", "results", "items"] {
                guard let rows = nested[nestedKey],
                      JSONSerialization.isValidJSONObject(rows),
                      let payload = try? JSONSerialization.data(withJSONObject: rows),
                      let values = try? decoder.decode([Value].self, from: payload) else { continue }
                return values
            }
        }
        return nil
    }

    private func wrappedRows(_ object: Any, keys: [String]) -> [[String: Any]] {
        if let rows = object as? [[String: Any]] { return rows }
        guard let dictionary = object as? [String: Any] else { return [] }
        for key in keys {
            if let rows = dictionary[key] as? [[String: Any]] { return rows }
            if let nested = dictionary[key] as? [String: Any] {
                for nestedKey in keys where nestedKey != key {
                    if let rows = nested[nestedKey] as? [[String: Any]] { return rows }
                }
            }
        }
        return []
    }

    private func string(_ dictionary: [String: Any], _ keys: [String]) -> String {
        for key in keys {
            if let value = dictionary[key] as? String, !value.isEmpty { return value }
            if let value = dictionary[key] as? NSNumber { return value.stringValue }
        }
        return ""
    }

    private static func parseSSE(_ lines: [String]) -> (type: String, data: [String: Any])? {
        var eventType = "message"
        var dataLines: [String] = []
        for line in lines {
            if line.hasPrefix("event:") {
                eventType = String(line.dropFirst(6)).trimmingCharacters(in: .whitespaces)
            } else if line.hasPrefix("data:") {
                dataLines.append(String(line.dropFirst(5)).trimmingCharacters(in: .whitespaces))
            }
        }
        guard let data = dataLines.joined(separator: "\n").data(using: .utf8),
              let payload = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }
        if eventType == "message", let embeddedType = payload["type"] as? String {
            eventType = embeddedType
        }
        return (eventType, payload)
    }

    private static func decodeSearchResults(_ value: Any?) -> [SearchResult] {
        guard let rows = value as? [[String: Any]] else { return [] }
        return rows.compactMap { row in
            guard let data = try? JSONSerialization.data(withJSONObject: row) else { return nil }
            return try? JSONDecoder().decode(SearchResult.self, from: data)
        }
    }

    private func makeRequest(
        path: String,
        method: String = "GET",
        queryItems: [URLQueryItem] = []
    ) throws -> URLRequest {
        guard var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false) else {
            throw APIError.invalidServerURL
        }

        let basePath = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let requestPath = path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        components.path = "/" + [basePath, requestPath].filter { !$0.isEmpty }.joined(separator: "/")
        components.queryItems = queryItems.isEmpty ? nil : queryItems

        guard let url = components.url else {
            throw APIError.invalidServerURL
        }

        var request = URLRequest(url: url, timeoutInterval: 25)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("SeleneNative-iOS/1.1", forHTTPHeaderField: "User-Agent")
        if !cookie.isEmpty {
            request.setValue(cookie, forHTTPHeaderField: "Cookie")
        }
        return request
    }
}

private struct DoubanCategoryEnvelope: Decodable {
    let list: [DoubanMovie]
}

private struct SearchEnvelope: Decodable {
    let results: [SearchResult]
}

private struct DetailEnvelope: Decodable {
    let result: SearchResult?
}

private struct DataEnvelope<Value: Decodable>: Decodable {
    let data: Value
}

private struct SuggestionEnvelope: Decodable {
    let suggestions: [SearchSuggestion]
}

private struct LiveSourcesEnvelope: Decodable {
    let sources: [LiveSource]
}

private struct LiveChannelsEnvelope: Decodable {
    let channels: [LiveChannel]
}

private struct BangumiCalendarDay: Decodable {
    let weekday: BangumiWeekday
    let items: [BangumiItem]
}

private struct BangumiWeekday: Decodable {
    let id: Int
}

private extension String {
    var nonEmpty: String? { isEmpty ? nil : self }
}