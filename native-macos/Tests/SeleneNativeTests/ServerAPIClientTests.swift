import XCTest
@testable import SeleneNative

final class ServerAPIClientTests: XCTestCase {
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
}
