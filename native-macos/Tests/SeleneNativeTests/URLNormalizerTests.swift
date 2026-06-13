import XCTest
@testable import SeleneNative

final class URLNormalizerTests: XCTestCase {
    func testAddsHTTPScheme() {
        let result = URLNormalizer.normalize("example.com")
        XCTAssertEqual(result?.scheme, "https")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testAddsHTTPForLocalhost() {
        let result = URLNormalizer.normalize("localhost:8080")
        XCTAssertEqual(result?.scheme, "http")
        XCTAssertEqual(result?.host, "localhost")
        XCTAssertEqual(result?.port, 8080)
    }

    func testPreservesExistingHTTPS() {
        let result = URLNormalizer.normalize("https://example.com")
        XCTAssertEqual(result?.scheme, "https")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testPreservesExistingHTTP() {
        let result = URLNormalizer.normalize("http://example.com")
        XCTAssertEqual(result?.scheme, "http")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testReturnsNilForEmptyInput() {
        let result = URLNormalizer.normalize("")
        XCTAssertNil(result)
    }

    func testReturnsNilForInvalidURL() {
        let result = URLNormalizer.normalize("://invalid")
        XCTAssertNil(result)
    }
}
