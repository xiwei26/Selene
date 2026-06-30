import Foundation

protocol VideoPlatformProviding: Sendable {
    func loadBilibiliPopular(page: Int, pageSize: Int) async throws -> VideoPlatformPage
    func searchBilibili(query: String) async throws -> VideoPlatformPage
    func loadYouTubePopular(regionCode: String, pageToken: String?) async throws -> VideoPlatformPage
    func searchYouTube(query: String, contentType: String, order: String, maxResults: Int) async throws -> VideoPlatformPage
    func loadYouTubeRegions() async throws -> [YouTubeRegion]
}

final class VideoPlatformAPIClient: VideoPlatformProviding, Sendable {
    private let request: LunaFeatureRequest

    init(serverURL: URL, cookie: String = "", session: URLSession = .shared) {
        self.request = LunaFeatureRequest(serverURL: serverURL, cookie: cookie, session: session)
    }

    func loadBilibiliPopular(page: Int = 1, pageSize: Int = 20) async throws -> VideoPlatformPage {
        try await loadPage(path: "/api/bilibili/popular", queryItems: [
            URLQueryItem(name: "pn", value: "\(page)"),
            URLQueryItem(name: "ps", value: "\(pageSize)")
        ])
    }

    func searchBilibili(query: String) async throws -> VideoPlatformPage {
        try await loadPage(path: "/api/bilibili/search", queryItems: [URLQueryItem(name: "q", value: query)])
    }

    func loadYouTubePopular(regionCode: String = "US", pageToken: String? = nil) async throws -> VideoPlatformPage {
        var queryItems = [URLQueryItem(name: "regionCode", value: regionCode)]
        if let pageToken { queryItems.append(URLQueryItem(name: "pageToken", value: pageToken)) }
        return try await loadPage(path: "/api/youtube/popular", queryItems: queryItems)
    }

    func searchYouTube(query: String, contentType: String = "all", order: String = "relevance", maxResults: Int = 25) async throws -> VideoPlatformPage {
        try await loadPage(path: "/api/youtube/search", queryItems: [
            URLQueryItem(name: "q", value: query),
            URLQueryItem(name: "contentType", value: contentType),
            URLQueryItem(name: "order", value: order),
            URLQueryItem(name: "maxResults", value: "\(maxResults)")
        ])
    }

    func loadYouTubeRegions() async throws -> [YouTubeRegion] {
        if let enveloped = try? await request.getJSON(path: "/api/youtube/regions", as: YouTubeRegionsResponse.self) {
            return enveloped.regions
        }
        if let wrapped = try? await request.getJSON(path: "/api/youtube/regions", as: LunaDataResponse<[YouTubeRegion]>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: "/api/youtube/regions", as: [YouTubeRegion].self)
    }

    private func loadPage(path: String, queryItems: [URLQueryItem]) async throws -> VideoPlatformPage {
        if let wrapped = try? await request.getJSON(path: path, queryItems: queryItems, as: LunaDataResponse<VideoPlatformPage>.self) {
            return wrapped.data
        }
        return try await request.getJSON(path: path, queryItems: queryItems, as: VideoPlatformPage.self)
    }
}

private struct YouTubeRegionsResponse: Decodable {
    let regions: [YouTubeRegion]
}
