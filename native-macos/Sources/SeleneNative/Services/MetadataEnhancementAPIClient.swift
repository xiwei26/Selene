import Foundation

protocol MetadataEnhancementProviding: Sendable {
    func loadBackdrop(title: String, originalTitle: String?, year: String?, sourceType: String?) async throws -> TmdbBackdropResult?
    func loadActor(actor: String, type: String, limit: Int) async throws -> TmdbActorResult?
    func loadDoubanComments(id: String, start: Int, limit: Int, sort: String) async throws -> [DoubanComment]
    func loadDoubanRecommends(kind: String, limit: Int, start: Int) async throws -> [DoubanMovie]
    func loadDoubanQuickInfo(id: String) async throws -> DoubanQuickInfo?
    func suggestDouban(query: String) async throws -> [DoubanSuggestItem]
    func loadCelebrityWorks(name: String, limit: Int, mode: String) async throws -> [DoubanCelebrityWork]
    func refreshTrailer(id: String, force: Bool) async throws -> TrailerRefreshResult?
}

final class MetadataEnhancementAPIClient: MetadataEnhancementProviding, Sendable {
    private let request: LunaFeatureRequest

    init(serverURL: URL, cookie: String = "", session: URLSession = .shared) {
        self.request = LunaFeatureRequest(serverURL: serverURL, cookie: cookie, session: session)
    }

    func loadBackdrop(title: String, originalTitle: String? = nil, year: String? = nil, sourceType: String? = nil) async throws -> TmdbBackdropResult? {
        var queryItems = [URLQueryItem(name: "title", value: title)]
        if let originalTitle { queryItems.append(URLQueryItem(name: "originalTitle", value: originalTitle)) }
        if let year { queryItems.append(URLQueryItem(name: "year", value: year)) }
        if let sourceType { queryItems.append(URLQueryItem(name: "sourceType", value: sourceType)) }
        return try await optional(path: "/api/tmdb/backdrop", queryItems: queryItems, as: TmdbBackdropResult.self)
    }

    func loadActor(actor: String, type: String = "movie", limit: Int = 20) async throws -> TmdbActorResult? {
        try await optional(path: "/api/tmdb/actor", queryItems: [
            URLQueryItem(name: "actor", value: actor),
            URLQueryItem(name: "type", value: type),
            URLQueryItem(name: "limit", value: "\(limit)")
        ], as: TmdbActorResult.self)
    }

    func loadDoubanComments(id: String, start: Int = 0, limit: Int = 10, sort: String = "new_score") async throws -> [DoubanComment] {
        let queryItems = [
            URLQueryItem(name: "id", value: id),
            URLQueryItem(name: "start", value: "\(start)"),
            URLQueryItem(name: "limit", value: "\(limit)"),
            URLQueryItem(name: "sort", value: sort)
        ]
        if let wrapped = try? await request.getJSON(path: "/api/douban/comments", queryItems: queryItems, as: LunaDataResponse<DoubanCommentsPayload>.self) {
            return wrapped.data.comments
        }
        if let payload = try? await request.getJSON(path: "/api/douban/comments", queryItems: queryItems, as: DoubanCommentsPayload.self) {
            return payload.comments
        }
        return try await request.getJSON(path: "/api/douban/comments", queryItems: queryItems, as: [DoubanComment].self)
    }

    func loadDoubanRecommends(kind: String, limit: Int = 20, start: Int = 0) async throws -> [DoubanMovie] {
        try await array(path: "/api/douban/recommends", queryItems: [
            URLQueryItem(name: "kind", value: kind),
            URLQueryItem(name: "limit", value: "\(limit)"),
            URLQueryItem(name: "start", value: "\(start)")
        ])
    }

    func loadDoubanQuickInfo(id: String) async throws -> DoubanQuickInfo? {
        try await optional(path: "/api/douban/quick-info", queryItems: [URLQueryItem(name: "id", value: id)], as: DoubanQuickInfo.self)
    }

    func suggestDouban(query: String) async throws -> [DoubanSuggestItem] {
        try await array(path: "/api/douban/suggest", queryItems: [URLQueryItem(name: "query", value: query)])
    }

    func loadCelebrityWorks(name: String, limit: Int = 20, mode: String = "search") async throws -> [DoubanCelebrityWork] {
        try await array(path: "/api/douban/celebrity-works", queryItems: [
            URLQueryItem(name: "name", value: name),
            URLQueryItem(name: "limit", value: "\(limit)"),
            URLQueryItem(name: "mode", value: mode)
        ])
    }

    func refreshTrailer(id: String, force: Bool = false) async throws -> TrailerRefreshResult? {
        try await optional(path: "/api/douban/refresh-trailer", queryItems: [
            URLQueryItem(name: "id", value: id),
            URLQueryItem(name: "force", value: force ? "true" : "false")
        ], as: TrailerRefreshResult.self)
    }

    private func optional<T: Decodable>(path: String, queryItems: [URLQueryItem], as type: T.Type) async throws -> T? {
        if let wrapped = try? await request.getJSON(path: path, queryItems: queryItems, as: LunaDataResponse<T>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: path, queryItems: queryItems, as: T.self)
    }

    private func array<T: Decodable>(path: String, queryItems: [URLQueryItem]) async throws -> [T] {
        if let wrapped = try? await request.getJSON(path: path, queryItems: queryItems, as: LunaDataResponse<[T]>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: path, queryItems: queryItems, as: [T].self)
    }
}

private struct DoubanCommentsPayload: Decodable {
    let comments: [DoubanComment]
}
