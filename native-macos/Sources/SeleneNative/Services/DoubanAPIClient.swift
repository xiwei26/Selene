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
    private let session: URLSession
    private let cache: CacheService

    init(
        baseURL: URL = URL(string: "https://m.douban.com")!,
        session: URLSession = .shared,
        cache: CacheService = .shared
    ) {
        self.baseURL = baseURL
        self.session = session
        self.cache = cache
    }

    func getHotMovies() async throws -> [DoubanMovie] {
        try await getRecommendations(kind: "movie", category: "热门", region: nil, type: nil)
    }

    func getHotTVShows() async throws -> [DoubanMovie] {
        try await getRecommendations(kind: "tv", category: "最近热门", region: nil, type: "tv")
    }

    func getHotShows() async throws -> [DoubanMovie] {
        try await getRecommendations(kind: "tv", category: "show", region: nil, type: "show")
    }

    func getRecommendations(kind: String, category: String?, region: String?, type: String? = nil) async throws -> [DoubanMovie] {
        let key = "douban-list-\(kind)-\(category ?? "all")-\(region ?? "all")-\(type ?? "all")"
        if let cached: [DoubanMovie] = cache.load(key: key, maxAge: 6 * 60 * 60) {
            return cached
        }

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
        try? cache.save(key: key, data: response.items, maxAge: 6 * 60 * 60)
        return response.items
    }

    func getDetails(doubanId: String) async throws -> DoubanMovieDetails {
        let key = "douban-detail-\(doubanId)"
        if let cached: DoubanMovieDetails = cache.load(key: key, maxAge: 3 * 24 * 60 * 60) {
            return cached
        }

        let url = baseURL.appendingPathComponent("/rexxar/api/v2/movie/\(doubanId)")
        let details: DoubanMovieDetails = try await getJSON(url: url)
        try? cache.save(key: key, data: details, maxAge: 3 * 24 * 60 * 60)
        return details
    }

    private func getJSON<T: Decodable>(url: URL?) async throws -> T {
        guard let url else { throw APIError.invalidURL }
        var request = URLRequest(url: url, timeoutInterval: 15)
        request.setValue("Mozilla/5.0 SeleneNative/1.0", forHTTPHeaderField: "User-Agent")
        request.setValue("https://movie.douban.com/", forHTTPHeaderField: "Referer")
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
}
