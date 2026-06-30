import Foundation
import XCTest
@testable import SeleneNative

final class DoubanAPIClientTests: XCTestCase {
    override func tearDown() {
        TestURLProtocol.requestHandler = nil
        super.tearDown()
    }

    func testBackendHotMoviesUseLunaCategoriesEndpoint() async throws {
        let expectation = XCTestExpectation(description: "request")
        TestURLProtocol.requestHandler = { request in
            XCTAssertEqual(request.url?.path, "/api/douban/categories")
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(components?.queryValue("kind"), "movie")
            XCTAssertEqual(components?.queryValue("category"), "热门")
            XCTAssertEqual(components?.queryValue("type"), "全部")
            XCTAssertEqual(components?.queryValue("limit"), "20")
            XCTAssertEqual(components?.queryValue("start"), "0")
            XCTAssertEqual(request.value(forHTTPHeaderField: "Cookie"), "sid=abc")
            expectation.fulfill()
            return Self.jsonResponse(for: request, body: #"{"code":200,"message":"ok","list":[{"id":"m1","title":"Luna Movie","poster":"https://img.example/m1.jpg","rate":"8.8","year":"2026"}]}"#)
        }

        let client = DoubanAPIClient(
            backendBaseURL: URL(string: "http://server.test")!,
            backendCookie: "sid=abc",
            session: makeSession(),
            cache: makeCache()
        )

        let movies = try await client.getHotMovies()

        await fulfillment(of: [expectation], timeout: 1)
        XCTAssertEqual(movies.first?.title, "Luna Movie")
    }

    func testBackendHotTVShowsUseLunaTvCategoryParameters() async throws {
        let expectation = XCTestExpectation(description: "request")
        TestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(components?.queryValue("kind"), "tv")
            XCTAssertEqual(components?.queryValue("category"), "tv")
            XCTAssertEqual(components?.queryValue("type"), "tv")
            expectation.fulfill()
            return Self.jsonResponse(for: request, body: #"{"code":200,"message":"ok","list":[]}"#)
        }

        let client = DoubanAPIClient(
            backendBaseURL: URL(string: "http://server.test")!,
            session: makeSession(),
            cache: makeCache()
        )

        _ = try await client.getHotTVShows()

        await fulfillment(of: [expectation], timeout: 1)
    }

    func testBackendHotShowsUseLunaShowCategoryParameters() async throws {
        let expectation = XCTestExpectation(description: "request")
        TestURLProtocol.requestHandler = { request in
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(components?.queryValue("kind"), "tv")
            XCTAssertEqual(components?.queryValue("category"), "show")
            XCTAssertEqual(components?.queryValue("type"), "show")
            expectation.fulfill()
            return Self.jsonResponse(for: request, body: #"{"code":200,"message":"ok","list":[]}"#)
        }

        let client = DoubanAPIClient(
            backendBaseURL: URL(string: "http://server.test")!,
            session: makeSession(),
            cache: makeCache()
        )

        _ = try await client.getHotShows()

        await fulfillment(of: [expectation], timeout: 1)
    }

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TestURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private func makeCache() -> CacheService {
        CacheService(namespace: "DoubanAPIClientTests-\(UUID().uuidString)")
    }

    private static func jsonResponse(for request: URLRequest, body: String) -> (HTTPURLResponse, Data) {
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        )!
        return (response, Data(body.utf8))
    }
}

private final class TestURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

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
