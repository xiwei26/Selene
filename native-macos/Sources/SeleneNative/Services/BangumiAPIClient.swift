import Foundation

protocol BangumiProviding: Sendable {
    func getTodayCalendar() async throws -> [BangumiItem]
    func getCalendarByWeekday(_ weekday: Int) async throws -> [BangumiItem]
    func getDetails(bangumiId: Int) async throws -> BangumiDetails
}

final class BangumiAPIClient: BangumiProviding, Sendable {
    private let baseURL: URL
    private let session: URLSession
    private let cache: CacheService

    init(
        baseURL: URL = URL(string: "https://api.bgm.tv")!,
        session: URLSession = .shared,
        cache: CacheService = .shared
    ) {
        self.baseURL = baseURL
        self.session = session
        self.cache = cache
    }

    func getTodayCalendar() async throws -> [BangumiItem] {
        let weekday = Calendar.current.component(.weekday, from: Date())
        let normalized = weekday == 1 ? 7 : weekday - 1
        return try await getCalendarByWeekday(normalized)
    }

    func getCalendarByWeekday(_ weekday: Int) async throws -> [BangumiItem] {
        let key = "bangumi-calendar-\(weekday)"
        if let cached: [BangumiItem] = cache.load(key: key, maxAge: 24 * 60 * 60) {
            return cached
        }

        let responses: [BangumiCalendarResponse] = try await getJSON(url: baseURL.appendingPathComponent("/calendar"))
        let items = responses.first { $0.weekday.id == weekday }?.items ?? []
        try? cache.save(key: key, data: items, maxAge: 24 * 60 * 60)
        return items
    }

    func getDetails(bangumiId: Int) async throws -> BangumiDetails {
        let key = "bangumi-detail-\(bangumiId)"
        if let cached: BangumiDetails = cache.load(key: key, maxAge: 3 * 24 * 60 * 60) {
            return cached
        }

        let details: BangumiDetails = try await getJSON(url: baseURL.appendingPathComponent("/v0/subjects/\(bangumiId)"))
        try? cache.save(key: key, data: details, maxAge: 3 * 24 * 60 * 60)
        return details
    }

    private func getJSON<T: Decodable>(url: URL?) async throws -> T {
        guard let url else { throw APIError.invalidURL }
        var request = URLRequest(url: url, timeoutInterval: 15)
        request.setValue("senshinya/selene/1.0.0", forHTTPHeaderField: "User-Agent")
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
