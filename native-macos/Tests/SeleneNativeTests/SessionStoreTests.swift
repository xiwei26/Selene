import XCTest
@testable import SeleneNative

final class SessionStoreTests: XCTestCase {
    func testInitialStateIsLoggedOut() {
        let store = SessionStore()
        XCTAssertNil(store.session)
        XCTAssertFalse(store.isLoggedIn)
    }

    func testLoginSetsSession() {
        let store = SessionStore()
        let session = LoginSession(
            serverURL: URL(string: "https://example.com")!,
            username: "test",
            cookie: "cookie=1"
        )
        store.login(session: session)
        XCTAssertTrue(store.isLoggedIn)
        XCTAssertEqual(store.session?.username, "test")
        XCTAssertEqual(store.session?.cookie, "cookie=1")
    }

    func testLogoutClearsSession() {
        let store = SessionStore()
        let session = LoginSession(
            serverURL: URL(string: "https://example.com")!,
            username: "test",
            cookie: "cookie=1"
        )
        store.login(session: session)
        store.logout()
        XCTAssertNil(store.session)
        XCTAssertFalse(store.isLoggedIn)
    }

    func testSetErrorUpdatesErrorMessage() {
        let store = SessionStore()
        store.setError("测试错误")
        XCTAssertEqual(store.errorMessage, "测试错误")
    }
}
