import Foundation

protocol DoubanProviding: Sendable {
    func getHotMovies() async throws -> [DoubanMovie]
    func getHotTVShows() async throws -> [DoubanMovie]
    func getHotShows() async throws -> [DoubanMovie]
    func getRecommendations(kind: String, category: String?, region: String?, type: String?) async throws -> [DoubanMovie]
    func getDetails(doubanId: String) async throws -> DoubanMovieDetails
}

final class DoubanAPIClient: DoubanProviding, Sendable {
    private let baseURL: URL
    private let backendBaseURL: URL?
    private let backendCookie: String
    private let session: URLSession
    private let cache: CacheService

    init(
        baseURL: URL = URL(string: "https://m.douban.com")!,
        backendBaseURL: URL? = nil,
        backendCookie: String = "",
        session: URLSession = .shared,
        cache: CacheService = .shared
    ) {
        self.baseURL = baseURL
        self.backendBaseURL = backendBaseURL
        self.backendCookie = backendCookie
        self.session = session
        self.cache = cache
    }

    func getHotMovies() async throws -> [DoubanMovie] {
        try await getRecommendations(kind: "movie", category: "热门", region: nil, type: "全部")
    }

    func getHotTVShows() async throws -> [DoubanMovie] {
        if backendBaseURL != nil {
            try await getRecommendations(kind: "tv", category: "tv", region: nil, type: "tv")
        } else {
            try await getRecommendations(kind: "tv", category: "最近热门", region: nil, type: "tv")
        }
    }

    func getHotShows() async throws -> [DoubanMovie] {
        try await getRecommendations(kind: "tv", category: "show", region: nil, type: "show")
    }

    func getRecommendations(kind: String, category: String?, region: String?, type: String? = nil) async throws -> [DoubanMovie] {
        let key = "douban-list-\(cacheScope)-\(kind)-\(category ?? "all")-\(region ?? "all")-\(type ?? "all")"
        if let cached: [DoubanMovie] = cache.load(key: key, maxAge: 6 * 60 * 60) {
            return cached
        }

        let response: [DoubanMovie]
        if backendBaseURL != nil {
            response = try await getBackendCategories(kind: kind, category: category, type: type ?? region)
        } else {
            response = try await getDirectRecommendations(kind: kind, category: category, region: region, type: type)
        }

        try? cache.save(key: key, data: response, maxAge: 6 * 60 * 60)
        return response
    }

    private func getDirectRecommendations(kind: String, category: String?, region: String?, type: String?) async throws -> [DoubanMovie] {
        var components = URLComponents(url: baseURL.appendingPathComponent("/rexxar/api/v2/subject/recent_hot/\(kind)"), resolvingAgainstBaseURL: false)
        var queryItems = [
            URLQueryItem(name: "start", value: "0"),
            URLQueryItem(name: "limit", value: "20")
        ]
        if let category { queryItems.append(URLQueryItem(name: "category", value: category)) }
        if let region { queryItems.append(URLQueryItem(name: "region", value: region)) }
        if let type { queryItems.append(URLQueryItem(name: "type", value: type)) }
        components?.queryItems = queryItems

        let response: DoubanResponse = try await getJSON(url: components?.url)
        return response.items
    }

    private func getBackendCategories(kind: String, category: String?, type: String?) async throws -> [DoubanMovie] {
        guard let url = backendURL(path: "/api/douban/categories") else { throw APIError.invalidURL }
        var components = URLComponents(url: url, resolvingAgainstBaseURL: false)
        components?.queryItems = [
            URLQueryItem(name: "kind", value: kind),
            URLQueryItem(name: "category", value: category ?? ""),
            URLQueryItem(name: "type", value: type ?? ""),
            URLQueryItem(name: "limit", value: "20"),
            URLQueryItem(name: "start", value: "0")
        ]

        let response: DoubanCategoryResponse = try await getJSON(url: components?.url, includeBackendCookie: true)
        return response.list
    }

    func getDetails(doubanId: String) async throws -> DoubanMovieDetails {
        let key = "douban-detail-\(cacheScope)-\(doubanId)"
        if let cached: DoubanMovieDetails = cache.load(key: key, maxAge: 3 * 24 * 60 * 60) {
            return cached
        }

        let details: DoubanMovieDetails
        if backendBaseURL != nil {
            guard let url = backendURL(path: "/api/douban/details") else { throw APIError.invalidURL }
            var components = URLComponents(url: url, resolvingAgainstBaseURL: false)
            components?.queryItems = [URLQueryItem(name: "id", value: doubanId)]
            let response: DoubanDetailResponse = try await getJSON(url: components?.url, includeBackendCookie: true)
            details = response.data
        } else {
            let url = baseURL.appendingPathComponent("/rexxar/api/v2/movie/\(doubanId)")
            details = try await getJSON(url: url)
        }
        try? cache.save(key: key, data: details, maxAge: 3 * 24 * 60 * 60)
        return details
    }

    private func getJSON<T: Decodable>(url: URL?, includeBackendCookie: Bool = false) async throws -> T {
        guard let url else { throw APIError.invalidURL }
        var request = URLRequest(url: url, timeoutInterval: 15)
        request.setValue("Mozilla/5.0 SeleneNative/1.0", forHTTPHeaderField: "User-Agent")
        request.setValue("https://movie.douban.com/", forHTTPHeaderField: "Referer")
        if includeBackendCookie, !backendCookie.isEmpty {
            request.setValue(backendCookie, forHTTPHeaderField: "Cookie")
        }
        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            throw APIError.responseError(statusCode: (response as? HTTPURLResponse)?.statusCode ?? -1)
        }
        do {
            return try JSONDecoder().decode(T.self, from: data)
        } catch {
            throw APIError.parseError
        }
    }

    private var cacheScope: String {
        backendBaseURL?.absoluteString ?? "direct"
    }

    private func backendURL(path: String) -> URL? {
        backendBaseURL?.appendingPathComponent(path)
    }
}

private struct DoubanCategoryResponse: Decodable {
    let list: [DoubanMovie]
}

private struct DoubanDetailResponse: Decodable {
    let data: DoubanMovieDetails
}
