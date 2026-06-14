import XCTest
@testable import SeleneNative

@MainActor
final class FeatureParityP4Tests: XCTestCase {
    func testThemeStorePersistsMode() {
        let defaults = UserDefaults(suiteName: "FeatureParityP4Tests-\(UUID().uuidString)")!
        let store = ThemeStore(userDefaults: defaults)

        store.mode = .dark
        let reloaded = ThemeStore(userDefaults: defaults)

        XCTAssertEqual(reloaded.mode, .dark)
        defaults.removePersistentDomain(forName: defaultsSuiteName(defaults))
    }

    func testVersionServiceComparesSemanticVersions() {
        XCTAssertEqual(VersionService.compare("1.2.0", "1.2.0"), .orderedSame)
        XCTAssertEqual(VersionService.compare("1.3.0", "1.2.9"), .orderedDescending)
        XCTAssertEqual(VersionService.compare("1.2.0", "1.10.0"), .orderedAscending)
        XCTAssertTrue(VersionService.isRemoteVersion("1.2.1", newerThan: "1.2.0"))
    }

    func testContentFilterMatchesTitleAndDescription() {
        let filter = ContentFilterService(blockedKeywords: ["广告", "cam"])
        let clean = SearchResult(
            id: "1", title: "正常电影", poster: "", episodes: [], episodeTitles: [],
            source: "s", sourceName: "S", year: "2024", description: "剧情"
        )
        let blockedByTitle = SearchResult(
            id: "2", title: "CAM 资源", poster: "", episodes: [], episodeTitles: [],
            source: "s", sourceName: "S", year: "2024"
        )
        let blockedByDescription = SearchResult(
            id: "3", title: "电影", poster: "", episodes: [], episodeTitles: [],
            source: "s", sourceName: "S", year: "2024", description: "包含广告"
        )

        XCTAssertFalse(filter.shouldHide(clean))
        XCTAssertTrue(filter.shouldHide(blockedByTitle))
        XCTAssertTrue(filter.shouldHide(blockedByDescription))
        XCTAssertEqual(filter.filter([clean, blockedByTitle, blockedByDescription]), [clean])
    }

    private func defaultsSuiteName(_ defaults: UserDefaults) -> String {
        defaults.dictionaryRepresentation()["NSArgumentDomain"] as? String ?? ""
    }
}
