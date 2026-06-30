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
