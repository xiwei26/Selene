import Foundation

final class ServerAPIClient: ContentProvider, Sendable {
    let baseURL: URL
    private let session: URLSession

    init(baseURL: URL, session: URLSession = .shared) {
        self.baseURL = baseURL
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
        _ = try await performDataRequest(
            path: "/api/favorites",
            method: "POST",
            body: ["key": key, "favorite": data]
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
            body: ["key": record.id, "record": record.toJson()]
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
        return try JSONDecoder().decode(EpgData.self, from: data)
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
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }
        return data
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
}

private struct LiveSourcesResponse: Decodable {
    let sources: [LiveSource]
}

private struct LiveChannelsResponse: Decodable {
    let channels: [LiveChannel]
}
