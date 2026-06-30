import Foundation

protocol ShortDramaProviding: Sendable {
    func loadCategories() async throws -> [ShortDramaCategory]
    func loadRecommend(category: String?, size: Int) async throws -> ShortDramaListResult
    func loadList(categoryId: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult
    func search(query: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult
    func loadDetail(id: String, name: String?) async throws -> ShortDramaDetail?
    func parse(id: String, episode: Int, name: String?) async throws -> ShortDramaParseResult?
}

final class ShortDramaAPIClient: ShortDramaProviding, Sendable {
    private let request: LunaFeatureRequest

    init(serverURL: URL, cookie: String = "", session: URLSession = .shared) {
        self.request = LunaFeatureRequest(serverURL: serverURL, cookie: cookie, session: session)
    }

    func loadCategories() async throws -> [ShortDramaCategory] {
        if let wrapped = try? await request.getJSON(path: "/api/shortdrama/categories", as: LunaDataResponse<[ShortDramaCategory]>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: "/api/shortdrama/categories", as: [ShortDramaCategory].self)
    }

    func loadRecommend(category: String? = nil, size: Int = 24) async throws -> ShortDramaListResult {
        var queryItems = [URLQueryItem(name: "size", value: "\(size)")]
        if let category { queryItems.append(URLQueryItem(name: "category", value: category)) }
        return try await loadListResponse(path: "/api/shortdrama/recommend", queryItems: queryItems)
    }

    func loadList(categoryId: String, page: Int = 1, pageSize: Int = 24) async throws -> ShortDramaListResult {
        try await loadListResponse(path: "/api/shortdrama/list", queryItems: [
            URLQueryItem(name: "category", value: categoryId),
            URLQueryItem(name: "page", value: "\(page)"),
            URLQueryItem(name: "size", value: "\(pageSize)")
        ])
    }

    func search(query: String, page: Int = 1, pageSize: Int = 24) async throws -> ShortDramaListResult {
        try await loadListResponse(path: "/api/shortdrama/search", queryItems: [
            URLQueryItem(name: "query", value: query),
            URLQueryItem(name: "page", value: "\(page)"),
            URLQueryItem(name: "size", value: "\(pageSize)")
        ])
    }

    func loadDetail(id: String, name: String? = nil) async throws -> ShortDramaDetail? {
        var queryItems = [URLQueryItem(name: "id", value: id)]
        if let name { queryItems.append(URLQueryItem(name: "name", value: name)) }
        if let wrapped = try? await request.getJSON(path: "/api/shortdrama/detail", queryItems: queryItems, as: LunaDataResponse<ShortDramaDetail>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: "/api/shortdrama/detail", queryItems: queryItems, as: ShortDramaDetail.self)
    }

    func parse(id: String, episode: Int, name: String? = nil) async throws -> ShortDramaParseResult? {
        var queryItems = [URLQueryItem(name: "id", value: id), URLQueryItem(name: "episode", value: "\(episode)")]
        if let name { queryItems.append(URLQueryItem(name: "name", value: name)) }
        if let wrapped = try? await request.getJSON(path: "/api/shortdrama/parse", queryItems: queryItems, as: LunaDataResponse<ShortDramaParseResult>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: "/api/shortdrama/parse", queryItems: queryItems, as: ShortDramaParseResult.self)
    }

    private func loadListResponse(path: String, queryItems: [URLQueryItem]) async throws -> ShortDramaListResult {
        if let wrapped = try? await request.getJSON(path: path, queryItems: queryItems, as: LunaDataResponse<ShortDramaListResult>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: path, queryItems: queryItems, as: ShortDramaListResult.self)
    }
}
