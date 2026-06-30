import Foundation
import XCTest
@testable import SeleneNative

final class LunaFeatureClientTests: XCTestCase {
    override func tearDown() {
        LunaFeatureTestURLProtocol.requestHandler = nil
        super.tearDown()
    }

    func testShortDramaSearchForwardsCookieAndQuery() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/shortdrama/search")
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(components?.queryValue("query"), "hero")
            XCTAssertEqual(components?.queryValue("page"), "2")
            XCTAssertEqual(components?.queryValue("size"), "24")
            XCTAssertEqual(request.value(forHTTPHeaderField: "Cookie"), "sid=abc")
            return Self.jsonResponse(for: request, body: #"{"data":{"list":[],"total":0}}"#)
        }

        let client = ShortDramaAPIClient(serverURL: URL(string: "http://server.test")!, cookie: "sid=abc", session: makeSession())
        _ = try await client.search(query: "hero", page: 2, pageSize: 24)
    }

    func testShortDramaCategoriesDecodeRawTypeIdArray() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/shortdrama/categories")
            return Self.jsonResponse(for: request, body: #"[{"type_id":7,"type_name":"Hot"}]"#)
        }

        let client = ShortDramaAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let categories = try await client.loadCategories()

        XCTAssertEqual(categories.first?.id, "7")
        XCTAssertEqual(categories.first?.name, "Hot")
    }

    func testShortDramaRecommendDecodesRawItemsArray() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/shortdrama/recommend")
            return Self.jsonResponse(for: request, body: #"[{"id":"s1","name":"Raw Recommend","cover":"c.jpg"}]"#)
        }

        let client = ShortDramaAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let result = try await client.loadRecommend(category: nil, size: 24)

        XCTAssertEqual(result.items.first?.id, "s1")
        XCTAssertEqual(result.total, 1)
    }

    func testShortDramaListUsesCategoryIdQuery() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/shortdrama/list")
            XCTAssertEqual(components?.queryValue("categoryId"), "12")
            XCTAssertNil(components?.queryValue("category"))
            return Self.jsonResponse(for: request, body: #"{"list":[]}"#)
        }

        let client = ShortDramaAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        _ = try await client.loadList(categoryId: "12", page: 1, pageSize: 24)
    }

    func testYouTubePopularUsesRegionAndPageToken() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/youtube/popular")
            XCTAssertEqual(components?.queryValue("regionCode"), "JP")
            XCTAssertEqual(components?.queryValue("pageToken"), "p1")
            return Self.jsonResponse(for: request, body: #"{"items":[],"nextPageToken":"n2"}"#)
        }

        let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, cookie: "sid=abc", session: makeSession())
        _ = try await client.loadYouTubePopular(regionCode: "JP", pageToken: "p1")
    }

    func testBilibiliPopularUsesPnPsAndDecodesVideosEnvelope() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/bilibili/popular")
            XCTAssertEqual(components?.queryValue("pn"), "3")
            XCTAssertEqual(components?.queryValue("ps"), "20")
            XCTAssertNil(components?.queryValue("page"))
            return Self.jsonResponse(for: request, body: #"{"videos":[{"id":"BV1","title":"Popular"}]}"#)
        }

        let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let page = try await client.loadBilibiliPopular(page: 3, pageSize: 20)

        XCTAssertEqual(page.items.first?.id, "BV1")
    }

    func testBilibiliSearchUsesQQuery() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/bilibili/search")
            XCTAssertEqual(components?.queryValue("q"), "music")
            XCTAssertNil(components?.queryValue("query"))
            return Self.jsonResponse(for: request, body: #"{"videos":[]}"#)
        }

        let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        _ = try await client.searchBilibili(query: "music")
    }

    func testYouTubeSearchUsesQAndDecodesVideosEnvelope() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/youtube/search")
            XCTAssertEqual(components?.queryValue("q"), "trailers")
            XCTAssertNil(components?.queryValue("query"))
            return Self.jsonResponse(for: request, body: #"{"videos":[{"id":"yt1","title":"Trailer"}]}"#)
        }

        let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let page = try await client.searchYouTube(query: "trailers", contentType: "all", order: "relevance", maxResults: 25)

        XCTAssertEqual(page.items.first?.id, "yt1")
    }

    func testYouTubeRegionsDecodeRegionsEnvelope() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/youtube/regions")
            return Self.jsonResponse(for: request, body: #"{"success":true,"regions":[{"code":"JP","name":"Japan"}]}"#)
        }

        let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let regions = try await client.loadYouTubeRegions()

        XCTAssertEqual(regions.first?.code, "JP")
    }

    func testDoubanCommentsUseIdPagingSortAndCookie() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/douban/comments")
            XCTAssertEqual(components?.queryValue("id"), "1292052")
            XCTAssertEqual(components?.queryValue("start"), "0")
            XCTAssertEqual(components?.queryValue("limit"), "10")
            XCTAssertEqual(components?.queryValue("sort"), "new_score")
            XCTAssertEqual(request.value(forHTTPHeaderField: "Cookie"), "sid=abc")
            return Self.jsonResponse(for: request, body: #"{"code":200,"data":{"comments":[],"start":0,"limit":10,"total":0}}"#)
        }

        let client = MetadataEnhancementAPIClient(serverURL: URL(string: "http://server.test")!, cookie: "sid=abc", session: makeSession())
        _ = try await client.loadDoubanComments(id: "1292052", start: 0, limit: 10, sort: "new_score")
    }

    func testTmdbBackdropUsesOriginalTitleAndStypeAndDecodesDataEnvelope() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(request.url?.path, "/api/tmdb/backdrop")
            XCTAssertEqual(components?.queryValue("original_title"), "Original")
            XCTAssertEqual(components?.queryValue("stype"), "movie")
            XCTAssertNil(components?.queryValue("originalTitle"))
            XCTAssertNil(components?.queryValue("sourceType"))
            return Self.jsonResponse(for: request, body: #"{"data":{"backdrop":"b.jpg","logo":"l.png","poster":"p.jpg"}}"#)
        }

        let client = MetadataEnhancementAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let backdrop = try await client.loadBackdrop(title: "Title", originalTitle: "Original", year: "2026", sourceType: "movie")

        XCTAssertEqual(backdrop?.backdropUrl, "b.jpg")
        XCTAssertEqual(backdrop?.logoUrl, "l.png")
        XCTAssertEqual(backdrop?.posterUrl, "p.jpg")
    }

    func testDoubanRecommendsDecodeListEnvelope() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/douban/recommends")
            return Self.jsonResponse(for: request, body: #"{"code":200,"message":"ok","list":[{"id":"r1","title":"Related","poster":"","year":"2026"}]}"#)
        }

        let client = MetadataEnhancementAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let recommends = try await client.loadDoubanRecommends(kind: "movie", limit: 20, start: 0)

        XCTAssertEqual(recommends.first?.id, "r1")
    }

    func testDoubanQuickInfoMapsRateAndPlotSummary() async throws {
        LunaFeatureTestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/douban/quick-info")
            return Self.jsonResponse(for: request, body: #"{"data":{"id":"1292052","title":"Title","rate":"9.7","plot_summary":"Summary"}}"#)
        }

        let client = MetadataEnhancementAPIClient(serverURL: URL(string: "http://server.test")!, session: makeSession())
        let info = try await client.loadDoubanQuickInfo(id: "1292052")

        XCTAssertEqual(info?.rating, "9.7")
        XCTAssertEqual(info?.summary, "Summary")
    }

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [LunaFeatureTestURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private static func jsonResponse(for request: URLRequest, body: String) -> (HTTPURLResponse, Data) {
        let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: ["Content-Type": "application/json"])!
        return (response, Data(body.utf8))
    }
}

private final class LunaFeatureTestURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = Self.requestHandler else {
            client?.urlProtocol(self, didFailWithError: APIError.invalidURL)
            return
        }
        do {
            let (response, data) = try handler(request)
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}
}

private extension URLComponents {
    func queryValue(_ name: String) -> String? {
        queryItems?.first { $0.name == name }?.value
    }
}
