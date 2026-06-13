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

    func extractCookie(from response: HTTPURLResponse) -> String {
        guard let setCookie = response.allHeaderFields["Set-Cookie"] as? String else { return "" }
        let parts = setCookie.split(separator: ";", maxSplits: 1, omittingEmptySubsequences: true)
        return String(parts.first ?? "")
    }
}
