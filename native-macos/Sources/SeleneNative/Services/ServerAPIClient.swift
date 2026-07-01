import Foundation

final class ServerAPIClient: ContentProvider, Sendable {
    let baseURL: URL
    private let cookie: String
    private let session: URLSession
    private static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .useDefaultKeys
        return decoder
    }()

    init(baseURL: URL, cookie: String = "", session: URLSession = .shared) {
        self.baseURL = baseURL
        self.cookie = cookie
        self.session = session
    }

    func login(username: String, password: String) async throws -> LoginSession {
        let url = baseURL.appendingPathComponent("/api/login")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let body = ["username": username, "password": password]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (_, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse else {
            throw APIError.message("无效的服务器响应")
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }

        let cookie = extractCookie(from: httpResponse)
        return LoginSession(
            serverURL: baseURL,
            username: username,
            cookie: cookie
        )
    }

    func search(query: String) async throws -> [SearchResult] {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/search"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: query)]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        applyCookie(to: &request)

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("搜索请求失败")
        }

        let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        guard let results = json?["results"] as? [[String: Any]] else { return [] }

        return try results.map { dict in
            let data = try JSONSerialization.data(withJSONObject: dict)
            return try JSONDecoder().decode(SearchResult.self, from: data)
        }
    }

    func detail(source: String, id: String) async throws -> SearchResult? {
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/detail"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [
            URLQueryItem(name: "source", value: source),
            URLQueryItem(name: "id", value: id)
        ]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        applyCookie(to: &request)

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取详情失败")
        }

        return try JSONDecoder().decode(SearchResult.self, from: data)
    }

    func searchResources() async throws -> [SearchResource] {
        let url = baseURL.appendingPathComponent("/api/search/resources")

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        applyCookie(to: &request)

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取资源列表失败")
        }

        return try JSONDecoder().decode([SearchResource].self, from: data)
    }

    func getFavorites() async throws -> [FavoriteItem] {
        let data = try await performDataRequest(path: "/api/favorites")
        let object = try jsonObject(from: data)
        let map = keyedMap(from: object, preferredKey: "favorites")
        return map.map { FavoriteItem.fromJson(key: $0.key, data: $0.value) }
            .sorted { $0.saveTime > $1.saveTime }
    }

    func addFavorite(source: String, id: String, data: [String: Any]) async throws {
        let key = "\(source)+\(id)"
        var favorite = data
        if favorite["search_title"] == nil {
            favorite["search_title"] = favorite["title"] as? String ?? ""
        }
        if favorite["origin"] == nil {
            favorite["origin"] = "vod"
        }
        _ = try await performDataRequest(
            path: "/api/favorites",
            method: "POST",
            body: ["key": key, "favorite": favorite]
        )
    }

    func removeFavorite(source: String, id: String) async throws {
        _ = try await performDataRequest(
            path: "/api/favorites",
            method: "DELETE",
            queryItems: [URLQueryItem(name: "key", value: "\(source)+\(id)")]
        )
    }

    func savePlayRecord(_ record: PlayRecord) async throws {
        _ = try await performDataRequest(
            path: "/api/playrecords",
            method: "POST",
            body: ["key": "\(record.source)+\(record.itemId)", "record": record.toJson()]
        )
    }

    func deletePlayRecord(source: String, id: String) async throws {
        _ = try await performDataRequest(
            path: "/api/playrecords",
            method: "DELETE",
            queryItems: [URLQueryItem(name: "key", value: "\(source)+\(id)")]
        )
    }

    func clearPlayRecords() async throws {
        _ = try await performDataRequest(path: "/api/playrecords", method: "DELETE")
    }

    func getPlayRecords() async throws -> [PlayRecord] {
        let data = try await performDataRequest(path: "/api/playrecords")
        let object = try jsonObject(from: data)
        let map = keyedMap(from: object, preferredKey: "records")
        return map.map { PlayRecord.fromJson(key: $0.key, data: $0.value) }
            .sorted { $0.saveTime > $1.saveTime }
    }

    func getSearchHistory() async throws -> [String] {
        let data = try await performDataRequest(path: "/api/searchhistory")
        let object = try jsonObject(from: data)
        if let history = object as? [String] {
            return history
        }
        if let dict = object as? [String: Any],
           let history = dict["history"] as? [String] ?? dict["keywords"] as? [String] {
            return history
        }
        return []
    }

    func addSearchHistory(query: String) async throws {
        _ = try await performDataRequest(
            path: "/api/searchhistory",
            method: "POST",
            body: ["keyword": query]
        )
    }

    func deleteSearchHistory(query: String) async throws {
        _ = try await performDataRequest(
            path: "/api/searchhistory",
            method: "DELETE",
            queryItems: [URLQueryItem(name: "keyword", value: query)]
        )
    }

    func clearSearchHistory() async throws {
        _ = try await performDataRequest(path: "/api/searchhistory", method: "DELETE")
    }

    func searchSuggestions(query: String) async throws -> [SearchSuggestion] {
        let data = try await performDataRequest(
            path: "/api/search/suggestions",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
        if let suggestions = try? JSONDecoder().decode([SearchSuggestion].self, from: data) {
            return suggestions
        }
        let object = try jsonObject(from: data)
        guard let dict = object as? [String: Any],
              let rawSuggestions = dict["suggestions"] as? [[String: Any]] else {
            return []
        }
        return try rawSuggestions.map { raw in
            let data = try JSONSerialization.data(withJSONObject: raw)
            return try JSONDecoder().decode(SearchSuggestion.self, from: data)
        }
    }

    func getLiveSources() async throws -> [LiveSource] {
        let data = try await performDataRequest(path: "/api/live/sources")
        if let sources = try? JSONDecoder().decode([LiveSource].self, from: data) {
            return sources
        }
        if let wrapped = try? JSONDecoder().decode(DataResponse<[LiveSource]>.self, from: data) {
            return wrapped.data
        }
        let wrapped = try JSONDecoder().decode(LiveSourcesResponse.self, from: data)
        return wrapped.sources
    }

    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel] {
        let data = try await performDataRequest(
            path: "/api/live/channels",
            queryItems: [URLQueryItem(name: "source", value: sourceKey)]
        )
        if let channels = try? JSONDecoder().decode([LiveChannel].self, from: data) {
            return channels
        }
        if let wrapped = try? JSONDecoder().decode(DataResponse<[LiveChannel]>.self, from: data) {
            return wrapped.data
        }
        let wrapped = try JSONDecoder().decode(LiveChannelsResponse.self, from: data)
        return wrapped.channels
    }

    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData? {
        let data = try await performDataRequest(
            path: "/api/live/epg",
            queryItems: [
                URLQueryItem(name: "tvgId", value: tvgId),
                URLQueryItem(name: "source", value: sourceKey)
            ]
        )
        guard !data.isEmpty else { return nil }
        if let wrapped = try? JSONDecoder().decode(DataResponse<EpgData>.self, from: data) {
            return wrapped.data
        }
        return try JSONDecoder().decode(EpgData.self, from: data)
    }

    func getRecommendedShortDramas() async throws -> [SearchResult] {
        let data = try await performDataRequest(
            path: "/api/shortdrama/recommend",
            queryItems: [URLQueryItem(name: "size", value: "30")]
        )
        return try readShortDramaList(from: data)
    }

    func searchShortDramas(query: String) async throws -> [SearchResult] {
        let data = try await performDataRequest(
            path: "/api/shortdrama/search",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
        return try readShortDramaList(from: data)
    }

    func getShortDramaDetail(id: String, name: String? = nil) async throws -> SearchResult? {
        var items = [
            URLQueryItem(name: "id", value: id),
            URLQueryItem(name: "episode", value: "1")
        ]
        if let name, !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            items.append(URLQueryItem(name: "name", value: name))
        }
        let data = try await performDataRequest(path: "/api/shortdrama/detail", queryItems: items)
        guard !data.isEmpty else { return nil }
        return try Self.decoder.decode(SearchResult.self, from: data)
    }

    func getBilibiliPopular() async throws -> [MediaPlatformItem] {
        let data = try await performDataRequest(path: "/api/bilibili/popular")
        return try readPlatformItems(from: data, source: "bilibili")
    }

    func searchBilibili(query: String) async throws -> [MediaPlatformItem] {
        let data = try await performDataRequest(
            path: "/api/bilibili/search",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
        return try readPlatformItems(from: data, source: "bilibili")
    }

    func getYouTubePopular(regionCode: String = "US") async throws -> [MediaPlatformItem] {
        let data = try await performDataRequest(
            path: "/api/youtube/popular",
            queryItems: [URLQueryItem(name: "regionCode", value: regionCode)]
        )
        return try readPlatformItems(from: data, source: "youtube")
    }

    func searchYouTube(query: String) async throws -> [MediaPlatformItem] {
        let data = try await performDataRequest(
            path: "/api/youtube/search",
            queryItems: [URLQueryItem(name: "q", value: query)]
        )
        return try readPlatformItems(from: data, source: "youtube")
    }

    func getTMDBBackdrop(title: String, year: String? = nil, type: String? = nil) async throws -> TMDBBackdrop? {
        var items = [URLQueryItem(name: "title", value: title)]
        if let year, !year.isEmpty {
            items.append(URLQueryItem(name: "year", value: year))
        }
        if let type, !type.isEmpty {
            items.append(URLQueryItem(name: "stype", value: type))
        }
        let data = try await performDataRequest(path: "/api/tmdb/backdrop", queryItems: items)
        let object = try jsonObject(from: data)
        let root = unwrapObject(object)
        guard JSONSerialization.isValidJSONObject(root) else { return nil }
        let payload = try JSONSerialization.data(withJSONObject: root)
        return try Self.decoder.decode(TMDBBackdrop.self, from: payload)
    }

    func getDoubanQuickInfo(title: String) async throws -> DoubanQuickInfo? {
        let data = try await performDataRequest(
            path: "/api/douban/quick-info",
            queryItems: [URLQueryItem(name: "q", value: title)]
        )
        let object = unwrapObject(try jsonObject(from: data))
        guard let dict = object as? [String: Any] else { return nil }
        return DoubanQuickInfo(
            title: readString(dict, keys: ["title", "name"]),
            year: readOptionalString(dict, keys: ["year"]),
            rating: readOptionalString(dict, keys: ["rating", "rate", "score"]),
            summary: readOptionalString(dict, keys: ["summary", "desc", "description"]),
            genres: readStringArray(dict, keys: ["genres", "genre"]),
            directors: readStringArray(dict, keys: ["directors", "director"]),
            cast: readStringArray(dict, keys: ["cast", "actors"])
        )
    }

    func getDoubanComments(doubanId: String) async throws -> [DoubanComment] {
        let data = try await performDataRequest(
            path: "/api/douban/comments",
            queryItems: [URLQueryItem(name: "id", value: doubanId)]
        )
        return try readWrappedArray(from: data, keys: ["comments", "data", "list"]).compactMap { item in
            let content = readString(item, keys: ["content", "comment", "text"])
            guard !content.isEmpty else { return nil }
            return DoubanComment(
                author: readString(item, keys: ["author", "name"]),
                content: content,
                rating: readString(item, keys: ["rating", "score"])
            )
        }
    }

    func getDoubanRecommendations(doubanId: String) async throws -> [DoubanRecommendation] {
        let data = try await performDataRequest(
            path: "/api/douban/recommends",
            queryItems: [URLQueryItem(name: "id", value: doubanId)]
        )
        return try readWrappedArray(from: data, keys: ["recommends", "recommendations", "data", "list"]).compactMap { item in
            let title = readString(item, keys: ["title", "name"])
            guard !title.isEmpty else { return nil }
            return DoubanRecommendation(
                id: readString(item, keys: ["id", "douban_id"]),
                title: title,
                cover: readString(item, keys: ["cover", "poster", "pic"]),
                rating: readString(item, keys: ["rating", "rate", "score"])
            )
        }
    }

    func getAdminConfig() async throws -> AdminConfig? {
        let data = try await performDataRequest(path: "/api/admin/config")
        guard !data.isEmpty else { return nil }
        let config = try Self.decoder.decode(AdminConfig.self, from: data)
        return normalizedAdminConfig(config)
    }

    func saveYouTubeConfig(_ config: YouTubeAdminConfig) async throws {
        let body: [String: Any] = [
            "enabled": config.enabled,
            "apiKey": config.apiKey,
            "enableDemo": config.enableDemo,
            "maxResults": config.maxResults,
            "enabledRegions": config.enabledRegions,
            "enabledCategories": config.enabledCategories
        ]
        _ = try await performDataRequest(path: "/api/admin/youtube", method: "POST", body: body)
    }

    func saveBilibiliConfig(enabled: Bool) async throws {
        _ = try await performDataRequest(
            path: "/api/admin/bilibili",
            method: "POST",
            body: ["enabled": enabled]
        )
    }

    func sseSearchURL(query: String) -> URL? {
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/search/ws"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [URLQueryItem(name: "q", value: query)]
        return components?.url
    }

    func extractCookie(from response: HTTPURLResponse) -> String {
        guard let setCookie = response.allHeaderFields["Set-Cookie"] as? String else { return "" }
        let parts = setCookie.split(separator: ";", maxSplits: 1, omittingEmptySubsequences: true)
        return String(parts.first ?? "")
    }

    private func performDataRequest(
        path: String,
        method: String = "GET",
        queryItems: [URLQueryItem] = [],
        body: [String: Any]? = nil
    ) async throws -> Data {
        var components = URLComponents(url: baseURL.appendingPathComponent(path), resolvingAgainstBaseURL: false)
        if !queryItems.isEmpty {
            components?.queryItems = queryItems
        }
        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        applyCookie(to: &request)
        if let body {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try JSONSerialization.data(withJSONObject: body)
        }

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.message("无效的服务器响应")
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            if let disabled = featureDisabledMessage(from: data, statusCode: httpResponse.statusCode) {
                throw APIError.featureDisabled(disabled, statusCode: httpResponse.statusCode)
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }
        return data
    }

    private func applyCookie(to request: inout URLRequest) {
        guard !cookie.isEmpty else { return }
        request.setValue(cookie, forHTTPHeaderField: "Cookie")
    }

    private func jsonObject(from data: Data) throws -> Any {
        guard !data.isEmpty else { return [:] }
        do {
            return try JSONSerialization.jsonObject(with: data)
        } catch {
            throw APIError.parseError
        }
    }

    private func keyedMap(from object: Any, preferredKey: String) -> [String: [String: Any]] {
        if let dict = object as? [String: [String: Any]] {
            return dict
        }
        if let dict = object as? [String: Any],
           let nested = dict[preferredKey] as? [String: [String: Any]] {
            return nested
        }
        return [:]
    }

    private func readShortDramaList(from data: Data) throws -> [SearchResult] {
        if let direct = try? Self.decoder.decode([SearchResult].self, from: data) {
            return direct
        }
        return try readWrappedArray(from: data, keys: ["data", "list", "results", "items"]).compactMap { item in
            guard JSONSerialization.isValidJSONObject(item) else { return nil }
            let payload = try JSONSerialization.data(withJSONObject: item)
            return try? Self.decoder.decode(SearchResult.self, from: payload)
        }
    }

    private func readPlatformItems(from data: Data, source: String) throws -> [MediaPlatformItem] {
        let items = try readWrappedArray(from: data, keys: ["data", "items", "results", "videos"])
        return items.compactMap { item in
            let nestedSnippet = item["snippet"] as? [String: Any]
            let title = readString(item, keys: ["title", "name"])
                .nonEmpty ?? readString(nestedSnippet ?? [:], keys: ["title"])
            guard !title.isEmpty else { return nil }
            return MediaPlatformItem(
                id: readString(item, keys: ["id", "videoId", "bvid", "aid"]),
                title: title,
                cover: readString(item, keys: ["cover", "pic", "thumbnail", "poster"]),
                author: readString(item, keys: ["author", "owner", "channelTitle", "up"]),
                description: readString(item, keys: ["description", "desc"]) .nonEmpty ?? readString(nestedSnippet ?? [:], keys: ["description"]),
                duration: readString(item, keys: ["duration", "length"]),
                source: readString(item, keys: ["source"]).nonEmpty ?? source,
                url: readString(item, keys: ["url", "webpage_url", "link"])
            )
        }
    }

    private func readWrappedArray(from data: Data, keys: [String]) throws -> [[String: Any]] {
        let object = try jsonObject(from: data)
        if let array = object as? [[String: Any]] {
            return array
        }
        guard let dict = object as? [String: Any] else { return [] }
        for key in keys {
            if let array = dict[key] as? [[String: Any]] {
                return array
            }
            if let nested = dict[key] as? [String: Any] {
                for nestedKey in keys where nestedKey != key {
                    if let array = nested[nestedKey] as? [[String: Any]] {
                        return array
                    }
                }
            }
        }
        return []
    }

    private func unwrapObject(_ object: Any) -> Any {
        guard let dict = object as? [String: Any],
              let data = dict["data"] else {
            return object
        }
        return data
    }

    private func normalizedAdminConfig(_ config: AdminConfig) -> AdminConfig {
        var config = config
        if var youtube = config.youTubeConfig {
            if youtube.enabledRegions.isEmpty {
                youtube.enabledRegions = YouTubeAdminConfig.defaultRegions
            }
            if youtube.enabledCategories.isEmpty {
                youtube.enabledCategories = YouTubeAdminConfig.defaultCategories
            }
            config.youTubeConfig = youtube
        }
        return config
    }

    private func readString(_ dict: [String: Any], keys: [String]) -> String {
        readOptionalString(dict, keys: keys) ?? ""
    }

    private func readOptionalString(_ dict: [String: Any], keys: [String]) -> String? {
        for key in keys {
            if let value = dict[key] as? String, !value.isEmpty {
                return value
            }
            if let value = dict[key] as? NSNumber {
                return value.stringValue
            }
        }
        return nil
    }

    private func readStringArray(_ dict: [String: Any], keys: [String]) -> [String] {
        for key in keys {
            if let values = dict[key] as? [String] {
                return values
            }
            if let value = dict[key] as? String, !value.isEmpty {
                return value.split(separator: "/").map { String($0).trimmingCharacters(in: .whitespacesAndNewlines) }
            }
        }
        return []
    }

    private func featureDisabledMessage(from data: Data, statusCode: Int) -> String? {
        let fallback = "功能未启用"
        guard let object = try? jsonObject(from: data) else {
            return nil
        }
        let message: String?
        if let dict = object as? [String: Any] {
            message = readOptionalString(dict, keys: ["message", "error", "msg"])
        } else {
            message = nil
        }
        guard statusCode == 403 || (message?.contains(fallback) == true) else {
            return nil
        }
        return message ?? fallback
    }
}

private extension String {
    var nonEmpty: String? {
        isEmpty ? nil : self
    }
}

private struct LiveSourcesResponse: Decodable {
    let sources: [LiveSource]
}

private struct LiveChannelsResponse: Decodable {
    let channels: [LiveChannel]
}

private struct DataResponse<T: Decodable>: Decodable {
    let data: T
}
