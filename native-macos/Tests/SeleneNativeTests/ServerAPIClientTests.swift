import XCTest
@testable import SeleneNative

final class ServerAPIClientTests: XCTestCase {
    override func tearDown() {
        ServerTestURLProtocol.handler = nil
        super.tearDown()
    }

    func testLoginURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        let client = ServerAPIClient(baseURL: baseURL)
        XCTAssertEqual(client.baseURL, baseURL)
    }

    func testSearchURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/search"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [URLQueryItem(name: "q", value: "test")]
        XCTAssertEqual(components?.url?.absoluteString, "https://example.com/api/search?q=test")
    }

    func testDetailURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/detail"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [
            URLQueryItem(name: "source", value: "src1"),
            URLQueryItem(name: "id", value: "id1")
        ]
        XCTAssertEqual(
            components?.url?.absoluteString,
            "https://example.com/api/detail?source=src1&id=id1"
        )
    }

    func testCookieExtraction() {
        let response = HTTPURLResponse(
            url: URL(string: "https://example.com")!,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Set-Cookie": "session=abc123; Path=/; HttpOnly"]
        )!

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!)
        let cookie = client.extractCookie(from: response)
        XCTAssertEqual(cookie, "session=abc123")
    }

    func testEmptyCookieExtraction() {
        let response = HTTPURLResponse(
            url: URL(string: "https://example.com")!,
            statusCode: 200,
            httpVersion: nil,
            headerFields: [:]
        )!

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!)
        let cookie = client.extractCookie(from: response)
        XCTAssertEqual(cookie, "")
    }

    func testYouTubePopularSendsRegionCodeAndReadsSnippetTitle() async throws {
        let expectation = XCTestExpectation(description: "request")
        ServerTestURLProtocol.handler = { request in
            XCTAssertEqual(request.url?.path, "/api/youtube/popular")
            let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
            XCTAssertEqual(components?.queryItems?.first { $0.name == "regionCode" }?.value, "JP")
            expectation.fulfill()
            return Self.jsonResponse(
                request: request,
                statusCode: 200,
                body: #"{"data":[{"id":"v1","snippet":{"title":"Snippet Title","description":"Desc"},"channelTitle":"Author","url":"https://youtu.be/v1"}]}"#
            )
        }

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!, session: makeSession())
        let items = try await client.getYouTubePopular(regionCode: "JP")

        await fulfillment(of: [expectation], timeout: 1)
        XCTAssertEqual(items.first?.title, "Snippet Title")
        XCTAssertEqual(items.first?.author, "Author")
    }

    func testFeatureDisabledErrorIsRecognized() async throws {
        ServerTestURLProtocol.handler = { request in
            Self.jsonResponse(
                request: request,
                statusCode: 403,
                body: #"{"message":"YouTube 功能未启用"}"#
            )
        }

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!, session: makeSession())

        do {
            _ = try await client.getYouTubePopular(regionCode: "US")
            XCTFail("Expected feature disabled error")
        } catch let error as APIError {
            XCTAssertTrue(error.isFeatureDisabled)
            XCTAssertEqual(error.localizedDescription, "YouTube 功能未启用")
        }
    }

    func testAdminConfigPreservesDefaultYouTubeRegionsAndCategories() async throws {
        ServerTestURLProtocol.handler = { request in
            XCTAssertEqual(request.url?.path, "/api/admin/config")
            return Self.jsonResponse(
                request: request,
                statusCode: 200,
                body: #"{"YouTubeConfig":{"enabled":true,"apiKey":"","enableDemo":true,"maxResults":25,"enabledRegions":[],"enabledCategories":[]},"BilibiliConfig":{"enabled":false}}"#
            )
        }

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!, session: makeSession())
        let config = try await client.getAdminConfig()

        XCTAssertEqual(config?.youTubeConfig?.enabledRegions, YouTubeAdminConfig.defaultRegions)
        XCTAssertEqual(config?.youTubeConfig?.enabledCategories, YouTubeAdminConfig.defaultCategories)
    }

    private func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ServerTestURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private static func jsonResponse(request: URLRequest, statusCode: Int, body: String) -> (HTTPURLResponse, Data) {
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: statusCode,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        )!
        return (response, Data(body.utf8))
    }
}

private final class ServerTestURLProtocol: URLProtocol {
    static var handler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let handler = Self.handler else {
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
