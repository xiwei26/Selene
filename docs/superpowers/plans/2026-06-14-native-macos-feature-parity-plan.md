# Native macOS Feature Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the native macOS SwiftUI app with all 22 features from the Flutter cross-platform app, implemented in 4 independently shippable batches.

**Architecture:** Views → @Observable Stores → Service Protocols → Concrete Implementations → URLSession/FileManager/UserDefaults. Pure-native: SwiftUI, AVKit, Foundation — no third-party dependencies.

**Tech Stack:** Swift 5.9+, SwiftUI (macOS 14+), AVKit, Foundation URLSession, UserDefaults, FileManager

---

## File Structure

### New Files (by batch)

**P1 — Core Search + User Data:**
- `Sources/SeleneNative/Models/AggregatedSearchResult.swift`
- `Sources/SeleneNative/Models/FavoriteItem.swift`
- `Sources/SeleneNative/Models/PlayRecord.swift`
- `Sources/SeleneNative/Models/SearchSuggestion.swift`
- `Sources/SeleneNative/Services/SSESearchClient.swift`
- `Sources/SeleneNative/Stores/FavoritesStore.swift`
- `Sources/SeleneNative/Stores/HistoryStore.swift`
- `Sources/SeleneNative/Views/VideoCardView.swift`
- `Sources/SeleneNative/Views/SearchSuggestionOverlay.swift`
- `Sources/SeleneNative/Views/PlayerSourcesView.swift`
- `Sources/SeleneNative/Views/PlayerEpisodesView.swift`
- `Tests/SeleneNativeTests/AggregatedSearchResultTests.swift`
- `Tests/SeleneNativeTests/FavoriteItemTests.swift`
- `Tests/SeleneNativeTests/PlayRecordTests.swift`
- `Tests/SeleneNativeTests/SSESearchClientTests.swift`
- `Tests/SeleneNativeTests/FavoritesStoreTests.swift`
- `Tests/SeleneNativeTests/HistoryStoreTests.swift`

**P2 — Home + Discovery:**
- `Sources/SeleneNative/Models/DoubanMovie.swift`
- `Sources/SeleneNative/Models/BangumiItem.swift`
- `Sources/SeleneNative/Services/CacheService.swift`
- `Sources/SeleneNative/Services/DoubanAPIClient.swift`
- `Sources/SeleneNative/Services/BangumiAPIClient.swift`
- `Sources/SeleneNative/Views/HomeView.swift`
- `Sources/SeleneNative/Views/CategoryView.swift`
- `Sources/SeleneNative/Views/PlayerDetailView.swift`
- `Sources/SeleneNative/Views/FavoritesView.swift`
- `Sources/SeleneNative/Views/HistoryView.swift`
- `Tests/SeleneNativeTests/DoubanMovieTests.swift`
- `Tests/SeleneNativeTests/BangumiItemTests.swift`
- `Tests/SeleneNativeTests/CacheServiceTests.swift`

**P3 — Live TV + Local Mode + Player Enhancements:**
- `Sources/SeleneNative/Models/LiveModels.swift`
- `Sources/SeleneNative/Services/LiveService.swift`
- `Sources/SeleneNative/Services/SubscriptionService.swift`
- `Sources/SeleneNative/Services/M3U8Service.swift`
- `Sources/SeleneNative/Services/DLNADiscoveryService.swift`
- `Sources/SeleneNative/Stores/LiveStore.swift`
- `Sources/SeleneNative/Views/LiveScreenView.swift`
- `Sources/SeleneNative/Views/LivePlayerView.swift`
- `Sources/SeleneNative/Views/DLNAControlView.swift`
- `Tests/SeleneNativeTests/LiveServiceTests.swift`
- `Tests/SeleneNativeTests/SubscriptionServiceTests.swift`
- `Tests/SeleneNativeTests/M3U8ServiceTests.swift`

**P4 — Experience Polish:**
- `Sources/SeleneNative/Stores/ThemeStore.swift`
- `Sources/SeleneNative/Services/VersionService.swift`
- `Sources/SeleneNative/Services/ContentFilterService.swift`
- `Sources/SeleneNative/Views/SettingsView.swift`
- `Sources/SeleneNative/Views/FullscreenImageViewer.swift`
- `Tests/SeleneNativeTests/ThemeStoreTests.swift`
- `Tests/SeleneNativeTests/VersionServiceTests.swift`
- `Tests/SeleneNativeTests/ContentFilterServiceTests.swift`

### Modified Files (across batches)

- `Sources/SeleneNative/Models/APIError.swift` — P1: add new error cases
- `Sources/SeleneNative/Models/LoginSession.swift` — P3: add isLocalMode flag
- `Sources/SeleneNative/Services/ContentProvider.swift` — P1: extend protocol
- `Sources/SeleneNative/Services/ServerAPIClient.swift` — P1: implement new methods
- `Sources/SeleneNative/Stores/SearchStore.swift` — P1: SSE/aggregation/filters, P4: content filter
- `Sources/SeleneNative/Stores/PlayerStore.swift` — P1: progress save/multi-source/reverse
- `Sources/SeleneNative/Stores/SessionStore.swift` — P3: local mode
- `Sources/SeleneNative/App/SeleneNativeApp.swift` — P1: inject stores, P4: theme/update
- `Sources/SeleneNative/Views/RootView.swift` — P4: theme wrapper
- `Sources/SeleneNative/Views/MainView.swift` — P1: NavigationSplitView sidebar
- `Sources/SeleneNative/Views/SearchResultsView.swift` — P1: SSE progress/aggregation/filters
- `Sources/SeleneNative/Views/DetailView.swift` — P2: Douban integration
- `Sources/SeleneNative/Views/PlayerView.swift` — P1: source switching/progress/reverse, P3: PiP
- `Sources/SeleneNative/Views/LoginView.swift` — P3: hidden local mode entry

---

# Batch P1: Core Search + User Data

## Task 1: Extend APIError

**Files:**
- Modify: `Sources/SeleneNative/Models/APIError.swift`

- [ ] **Step 1: Add new error cases to APIError**

Replace the entire file with:

```swift
import Foundation

enum APIError: LocalizedError {
    case message(String)
    case responseError(statusCode: Int)
    case invalidURL
    case unauthorized
    case networkTimeout
    case sseConnectionFailed
    case parseError(String)
    case unknown

    var localizedDescription: String {
        switch self {
        case .message(let msg):
            return msg
        case .responseError(let code):
            return "请求失败 (\(code))"
        case .invalidURL:
            return "服务器地址无效"
        case .unauthorized:
            return "登录已过期，请重新登录"
        case .networkTimeout:
            return "网络请求超时"
        case .sseConnectionFailed:
            return "搜索连接失败"
        case .parseError(let detail):
            return "数据解析失败: \(detail)"
        case .unknown:
            return "未知错误"
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build completes with no errors

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Models/APIError.swift
git commit -m "feat: extend APIError with networkTimeout, sseConnectionFailed, parseError"
```

---

## Task 2: Add FavoriteItem Model

**Files:**
- Create: `Sources/SeleneNative/Models/FavoriteItem.swift`
- Create: `Tests/SeleneNativeTests/FavoriteItemTests.swift`

- [ ] **Step 1: Write the failing test**

```swift
// Tests/SeleneNativeTests/FavoriteItemTests.swift
import XCTest
@testable import SeleneNative

final class FavoriteItemTests: XCTestCase {
    func testFromJsonParsesKeyAndData() {
        let data: [String: Any] = [
            "title": "测试影片",
            "source_name": "源A",
            "year": "2024",
            "cover": "https://example.com/poster.jpg",
            "total_episodes": 12,
            "save_time": 1718300000000
        ]
        let item = FavoriteItem.fromJson(key: "sourceA+123", data: data)
        XCTAssertEqual(item.id, "sourceA+123")
        XCTAssertEqual(item.source, "sourceA")
        XCTAssertEqual(item.title, "测试影片")
        XCTAssertEqual(item.sourceName, "源A")
        XCTAssertEqual(item.year, "2024")
        XCTAssertEqual(item.cover, "https://example.com/poster.jpg")
        XCTAssertEqual(item.totalEpisodes, 12)
        XCTAssertEqual(item.saveTime, 1718300000000)
    }

    func testFromJsonDefaultsMissingFields() {
        let data: [String: Any] = [:]
        let item = FavoriteItem.fromJson(key: "src+1", data: data)
        XCTAssertEqual(item.id, "src+1")
        XCTAssertEqual(item.source, "src")
        XCTAssertEqual(item.title, "")
        XCTAssertEqual(item.totalEpisodes, 0)
        XCTAssertEqual(item.saveTime, 0)
    }

    func testToJsonOmitsIdAndSource() {
        var item = FavoriteItem(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", totalEpisodes: 10, saveTime: 100)
        let json = item.toJson()
        XCTAssertNil(json["id"])
        XCTAssertNil(json["source"])
        XCTAssertEqual(json["title"] as? String, "T")
        XCTAssertEqual(json["total_episodes"] as? Int, 10)
    }

    func testKeySplitWithPlusInId() {
        let data: [String: Any] = ["title": "X"]
        let item = FavoriteItem.fromJson(key: "source+id+with+plus", data: data)
        XCTAssertEqual(item.source, "source")
        XCTAssertEqual(item.id, "source+id+with+plus")
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter FavoriteItemTests 2>&1 | tail -10`
Expected: FAIL — "Cannot find 'FavoriteItem' in scope"

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Models/FavoriteItem.swift
import Foundation

struct FavoriteItem: Identifiable {
    let id: String           // "source+id"
    let source: String
    var title: String
    var sourceName: String
    var year: String
    var cover: String
    var totalEpisodes: Int
    var saveTime: Int64

    static func fromJson(key: String, data: [String: Any]) -> FavoriteItem {
        let source = key.split(separator: "+", maxSplits: 1).first.map(String.init) ?? key
        return FavoriteItem(
            id: key,
            source: source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? "",
            totalEpisodes: data["total_episodes"] as? Int ?? 0,
            saveTime: data["save_time"] as? Int64 ?? 0
        )
    }

    func toJson() -> [String: Any] {
        return [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "total_episodes": totalEpisodes,
            "save_time": saveTime
        ]
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter FavoriteItemTests 2>&1 | tail -10`
Expected: All 4 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Models/FavoriteItem.swift Tests/SeleneNativeTests/FavoriteItemTests.swift
git commit -m "feat: add FavoriteItem model with JSON key parsing"
```

---

## Task 3: Add PlayRecord Model

**Files:**
- Create: `Sources/SeleneNative/Models/PlayRecord.swift`
- Create: `Tests/SeleneNativeTests/PlayRecordTests.swift`

- [ ] **Step 1: Write the failing test**

```swift
// Tests/SeleneNativeTests/PlayRecordTests.swift
import XCTest
@testable import SeleneNative

final class PlayRecordTests: XCTestCase {
    func testFromJsonParsesAllFields() {
        let data: [String: Any] = [
            "title": "影片",
            "source_name": "源A",
            "year": "2024",
            "cover": "https://example.com/p.jpg",
            "index": 3,
            "total_episodes": 24,
            "play_time": 120,
            "total_time": 2700,
            "save_time": 1718300000000,
            "search_title": "搜索词"
        ]
        let record = PlayRecord.fromJson(key: "src+99", data: data)
        XCTAssertEqual(record.id, "src+99")
        XCTAssertEqual(record.source, "src")
        XCTAssertEqual(record.index, 3)
        XCTAssertEqual(record.playTime, 120)
        XCTAssertEqual(record.totalTime, 2700)
        XCTAssertEqual(record.searchTitle, "搜索词")
    }

    func testProgressPercentage() {
        var record = PlayRecord(id: "s+1", source: "s", title: "", sourceName: "", year: "", cover: "", index: 0, totalEpisodes: 0, playTime: 900, totalTime: 2700, saveTime: 0, searchTitle: "")
        XCTAssertEqual(record.progressPercentage, 1.0 / 3.0, accuracy: 0.01)
    }

    func testProgressPercentageZeroTotal() {
        var record = PlayRecord(id: "s+1", source: "s", title: "", sourceName: "", year: "", cover: "", index: 0, totalEpisodes: 0, playTime: 100, totalTime: 0, saveTime: 0, searchTitle: "")
        XCTAssertEqual(record.progressPercentage, 0.0)
    }

    func testFormattedPlayTime() {
        var record = PlayRecord(id: "s+1", source: "s", title: "", sourceName: "", year: "", cover: "", index: 0, totalEpisodes: 0, playTime: 3661, totalTime: 0, saveTime: 0, searchTitle: "")
        XCTAssertEqual(record.formattedPlayTime, "1:01:01")
    }

    func testFormattedPlayTimeMinutes() {
        var record = PlayRecord(id: "s+1", source: "s", title: "", sourceName: "", year: "", cover: "", index: 0, totalEpisodes: 0, playTime: 125, totalTime: 0, saveTime: 0, searchTitle: "")
        XCTAssertEqual(record.formattedPlayTime, "2:05")
    }

    func testToJsonOmitsIdAndSource() {
        var record = PlayRecord(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", index: 0, totalEpisodes: 10, playTime: 100, totalTime: 200, saveTime: 300, searchTitle: "q")
        let json = record.toJson()
        XCTAssertNil(json["id"])
        XCTAssertNil(json["source"])
        XCTAssertEqual(json["play_time"] as? Int, 100)
        XCTAssertEqual(json["index"] as? Int, 0)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter PlayRecordTests 2>&1 | tail -10`
Expected: FAIL — "Cannot find 'PlayRecord' in scope"

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Models/PlayRecord.swift
import Foundation

struct PlayRecord {
    let id: String           // "source+id"
    let source: String
    var title: String
    var sourceName: String
    var year: String
    var cover: String
    var index: Int           // episode index
    var totalEpisodes: Int
    var playTime: Int        // seconds watched
    var totalTime: Int       // total seconds
    var saveTime: Int64      // milliseconds since epoch
    var searchTitle: String

    var progressPercentage: Double {
        guard totalTime > 0 else { return 0.0 }
        return Double(playTime) / Double(totalTime)
    }

    var formattedPlayTime: String {
        formatDuration(playTime)
    }

    var formattedTotalTime: String {
        formatDuration(totalTime)
    }

    private func formatDuration(_ seconds: Int) -> String {
        let h = seconds / 3600
        let m = (seconds % 3600) / 60
        let s = seconds % 60
        if h > 0 {
            return String(format: "%d:%02d:%02d", h, m, s)
        }
        return String(format: "%d:%02d", m, s)
    }

    static func fromJson(key: String, data: [String: Any]) -> PlayRecord {
        let source = key.split(separator: "+", maxSplits: 1).first.map(String.init) ?? key
        return PlayRecord(
            id: key,
            source: source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? "",
            index: data["index"] as? Int ?? 0,
            totalEpisodes: data["total_episodes"] as? Int ?? 0,
            playTime: data["play_time"] as? Int ?? 0,
            totalTime: data["total_time"] as? Int ?? 0,
            saveTime: data["save_time"] as? Int64 ?? 0,
            searchTitle: data["search_title"] as? String ?? ""
        )
    }

    func toJson() -> [String: Any] {
        return [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "index": index,
            "total_episodes": totalEpisodes,
            "play_time": playTime,
            "total_time": totalTime,
            "save_time": saveTime,
            "search_title": searchTitle
        ]
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter PlayRecordTests 2>&1 | tail -10`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Models/PlayRecord.swift Tests/SeleneNativeTests/PlayRecordTests.swift
git commit -m "feat: add PlayRecord model with progress formatting"
```

---

## Task 4: Add SearchSuggestion Model

**Files:**
- Create: `Sources/SeleneNative/Models/SearchSuggestion.swift`

- [ ] **Step 1: Write the model**

```swift
// Sources/SeleneNative/Models/SearchSuggestion.swift
import Foundation

struct SearchSuggestion: Codable, Identifiable {
    var id: String { text }
    let text: String
    let type: String
    let score: Double
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Models/SearchSuggestion.swift
git commit -m "feat: add SearchSuggestion model"
```

---

## Task 5: Add AggregatedSearchResult Model

**Files:**
- Create: `Sources/SeleneNative/Models/AggregatedSearchResult.swift`
- Create: `Tests/SeleneNativeTests/AggregatedSearchResultTests.swift`

- [ ] **Step 1: Write the failing test**

```swift
// Tests/SeleneNativeTests/AggregatedSearchResultTests.swift
import XCTest
@testable import SeleneNative

final class AggregatedSearchResultTests: XCTestCase {
    func testFromSearchResult() {
        let result = SearchResult(
            id: "1", title: "影片A", poster: "p1.jpg", episodes: ["u1", "u2"],
            episodeTitles: [], source: "src1", sourceName: "源1",
            year: "2024", doubanID: 123
        )
        let agg = AggregatedSearchResult.fromSearchResult(result)
        XCTAssertEqual(agg.title, "影片A")
        XCTAssertEqual(agg.year, "2024")
        XCTAssertEqual(agg.sourceNames, ["源1"])
        XCTAssertEqual(agg.episodeCounts["源1"], 2)
        XCTAssertEqual(agg.originalResults.count, 1)
    }

    func testAddResultMergesSameTitle() {
        let r1 = SearchResult(id: "1", title: "影片A", poster: "p1.jpg", episodes: ["u1"], episodeTitles: [], source: "src1", sourceName: "源1", year: "2024", doubanID: 123)
        let r2 = SearchResult(id: "2", title: "影片A", poster: "p2.jpg", episodes: ["u1", "u2", "u3"], episodeTitles: [], source: "src2", sourceName: "源2", year: "2024", doubanID: 123)

        var agg = AggregatedSearchResult.fromSearchResult(r1)
        agg.addResult(r2)
        XCTAssertEqual(agg.sourceNames.count, 2)
        XCTAssertEqual(agg.episodeCounts["源2"], 3)
        XCTAssertEqual(agg.originalResults.count, 2)
    }

    func testMostCommonDoubanId() {
        let r1 = SearchResult(id: "1", title: "A", poster: "", episodes: [], episodeTitles: [], source: "s1", sourceName: "源1", year: "2024", doubanID: 100)
        let r2 = SearchResult(id: "2", title: "A", poster: "", episodes: [], episodeTitles: [], source: "s2", sourceName: "源2", year: "2024", doubanID: 100)
        let r3 = SearchResult(id: "3", title: "A", poster: "", episodes: [], episodeTitles: [], source: "s3", sourceName: "源3", year: "2024", doubanID: 200)

        var agg = AggregatedSearchResult.fromSearchResult(r1)
        agg.addResult(r2)
        agg.addResult(r3)
        XCTAssertEqual(agg.mostCommonDoubanId, "100")
    }

    func testMostCommonDoubanIdNilWhenNone() {
        let r1 = SearchResult(id: "1", title: "A", poster: "", episodes: [], episodeTitles: [], source: "s1", sourceName: "源1", year: "2024")
        let agg = AggregatedSearchResult.fromSearchResult(r1)
        XCTAssertNil(agg.mostCommonDoubanId)
    }

    func testDifferentTitlesNotMerged() {
        let r1 = SearchResult(id: "1", title: "影片A", poster: "", episodes: [], episodeTitles: [], source: "s1", sourceName: "源1", year: "2024")
        let r2 = SearchResult(id: "2", title: "影片B", poster: "", episodes: [], episodeTitles: [], source: "s2", sourceName: "源2", year: "2024")
        var agg = AggregatedSearchResult.fromSearchResult(r1)
        agg.addResult(r2)
        // Different title → should NOT merge; addResult only merges same key
        XCTAssertEqual(agg.originalResults.count, 1)
        XCTAssertEqual(agg.sourceNames.count, 1)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter AggregatedSearchResultTests 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Models/AggregatedSearchResult.swift
import Foundation

struct AggregatedSearchResult: Identifiable {
    var id: String { key }
    let key: String          // title+year+typeName
    let title: String
    let year: String
    let type: String         // "movie" or "tv"
    let cover: String
    var episodeCounts: [String: Int]   // sourceName → count
    var doubanIds: [String: Int]       // doubanId string → occurrence count
    var sourceNames: [String]
    var originalResults: [SearchResult]
    let addedTimestamp: Int64

    var mostCommonEpisodeCount: Int {
        episodeCounts.values.max() ?? 0
    }

    var mostCommonDoubanId: String? {
        doubanIds.max(by: { $0.value < $1.value })?.key
    }

    static func fromSearchResult(_ result: SearchResult) -> AggregatedSearchResult {
        let typeName = result.typeName ?? "tv"
        let key = "\(result.title)+\(result.year)+\(typeName)"
        var doubanIds: [String: Int] = [:]
        if let did = result.doubanID {
            doubanIds[String(did)] = 1
        }
        return AggregatedSearchResult(
            key: key,
            title: result.title,
            year: result.year,
            type: typeName == "电影" ? "movie" : "tv",
            cover: result.poster,
            episodeCounts: [result.sourceName: result.episodes.count],
            doubanIds: doubanIds,
            sourceNames: [result.sourceName],
            originalResults: [result],
            addedTimestamp: Int64(Date().timeIntervalSince1970 * 1000)
        )
    }

    mutating func addResult(_ result: SearchResult) {
        let typeName = result.typeName ?? "tv"
        let key = "\(result.title)+\(result.year)+\(typeName)"
        guard key == self.key else { return }

        episodeCounts[result.sourceName] = result.episodes.count
        sourceNames.append(result.sourceName)
        originalResults.append(result)

        if let did = result.doubanID {
            doubanIds[String(did), default: 0] += 1
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter AggregatedSearchResultTests 2>&1 | tail -10`
Expected: All 5 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Models/AggregatedSearchResult.swift Tests/SeleneNativeTests/AggregatedSearchResultTests.swift
git commit -m "feat: add AggregatedSearchResult model with grouping logic"
```

---

## Task 6: Extend ContentProvider Protocol

**Files:**
- Modify: `Sources/SeleneNative/Services/ContentProvider.swift`

- [ ] **Step 1: Add new method signatures**

Replace the entire file with:

```swift
// Sources/SeleneNative/Services/ContentProvider.swift
import Foundation

protocol ContentProvider: Sendable {
    // Existing
    func login(username: String, password: String) async throws -> LoginSession
    func search(query: String) async throws -> [SearchResult]
    func detail(source: String, id: String) async throws -> SearchResult?
    func searchResources() async throws -> [SearchResource]

    // Favorites
    func getFavorites() async throws -> [FavoriteItem]
    func addFavorite(source: String, id: String, data: [String: Any]) async throws
    func removeFavorite(source: String, id: String) async throws

    // Play Records
    func getPlayRecords() async throws -> [PlayRecord]
    func savePlayRecord(_ record: PlayRecord) async throws
    func deletePlayRecord(source: String, id: String) async throws
    func clearPlayRecords() async throws

    // Search History
    func getSearchHistory() async throws -> [String]
    func addSearchHistory(query: String) async throws
    func deleteSearchHistory(query: String) async throws
    func clearSearchHistory() async throws

    // Search Suggestions
    func searchSuggestions(query: String) async throws -> [SearchSuggestion]

    // SSE Search URL
    func sseSearchURL(query: String) -> URL?
}
```

- [ ] **Step 2: Build — will fail because ServerAPIClient doesn't implement new methods yet. That's expected; we'll fix in Task 7.**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | grep "error:" | head -5`
Expected: Multiple errors about ServerAPIClient not conforming to ContentProvider

- [ ] **Step 3: Commit (protocol only, implementation follows)**

```bash
git add Sources/SeleneNative/Services/ContentProvider.swift
git commit -m "feat: extend ContentProvider protocol with favorites, records, history, suggestions, SSE"
```

---

## Task 7: Implement New ServerAPIClient Methods

**Files:**
- Modify: `Sources/SeleneNative/Services/ServerAPIClient.swift`
- Create: `Tests/SeleneNativeTests/ServerAPIClientExtendedTests.swift`

- [ ] **Step 1: Write the failing test for URL construction**

```swift
// Tests/SeleneNativeTests/ServerAPIClientExtendedTests.swift
import XCTest
@testable import SeleneNative

final class ServerAPIClientExtendedTests: XCTestCase {
    let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!)

    func testFavoritesURL() {
        let url = client.baseURL.appendingPathComponent("/api/favorites")
        XCTAssertEqual(url.absoluteString, "https://example.com/api/favorites")
    }

    func testPlayRecordsURL() {
        let url = client.baseURL.appendingPathComponent("/api/playrecords")
        XCTAssertEqual(url.absoluteString, "https://example.com/api/playrecords")
    }

    func testSearchHistoryURL() {
        let url = client.baseURL.appendingPathComponent("/api/searchhistory")
        XCTAssertEqual(url.absoluteString, "https://example.com/api/searchhistory")
    }

    func testSuggestionsURLConstruction() {
        var components = URLComponents(url: client.baseURL.appendingPathComponent("/api/search/suggestions"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: "test")]
        let url = components?.url
        XCTAssertNotNil(url)
        XCTAssertTrue(url!.absoluteString.contains("q=test"))
    }

    func testSSESearchURL() {
        let url = client.sseSearchURL(query: "hello")
        XCTAssertNotNil(url)
        XCTAssertTrue(url!.absoluteString.contains("/api/search/ws"))
        XCTAssertTrue(url!.absoluteString.contains("q=hello"))
    }

    func testFavoriteKeyFormat() {
        let key = "sourceA+123"
        let encoded = key.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed)
        XCTAssertNotNil(encoded)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter ServerAPIClientExtendedTests 2>&1 | tail -10`
Expected: FAIL — sseSearchURL not found

- [ ] **Step 3: Implement all new methods in ServerAPIClient**

Replace the entire `ServerAPIClient.swift` with:

```swift
// Sources/SeleneNative/Services/ServerAPIClient.swift
import Foundation

final class ServerAPIClient: ContentProvider, Sendable {
    let baseURL: URL
    private let session: URLSession

    init(baseURL: URL, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
    }

    // MARK: - Existing Methods

    func login(username: String, password: String) async throws -> LoginSession {
        let url = baseURL.appendingPathComponent("/api/login")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let body = ["username": username, "password": password]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (_, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse else {
            throw APIError.message("无效的服务器响应")
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }

        let cookie = extractCookie(from: httpResponse)
        return LoginSession(
            serverURL: baseURL,
            username: username,
            cookie: cookie
        )
    }

    func search(query: String) async throws -> [SearchResult] {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/search"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: query)]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("搜索请求失败")
        }

        let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        guard let results = json?["results"] as? [[String: Any]] else { return [] }

        return try results.map { dict in
            let data = try JSONSerialization.data(withJSONObject: dict)
            return try JSONDecoder().decode(SearchResult.self, from: data)
        }
    }

    func detail(source: String, id: String) async throws -> SearchResult? {
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/detail"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [
            URLQueryItem(name: "source", value: source),
            URLQueryItem(name: "id", value: id)
        ]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取详情失败")
        }

        return try JSONDecoder().decode(SearchResult.self, from: data)
    }

    func searchResources() async throws -> [SearchResource] {
        let url = baseURL.appendingPathComponent("/api/search/resources")

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取资源列表失败")
        }

        return try JSONDecoder().decode([SearchResource].self, from: data)
    }

    // MARK: - Favorites

    func getFavorites() async throws -> [FavoriteItem] {
        let url = baseURL.appendingPathComponent("/api/favorites")
        let (data, response) = try await authenticatedGET(url)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: [String: Any]] else {
            return []
        }
        return json.map { (key, value) in
            FavoriteItem.fromJson(key: key, data: value)
        }.sorted { $0.saveTime > $1.saveTime }
    }

    func addFavorite(source: String, id: String, data: [String: Any]) async throws {
        let url = baseURL.appendingPathComponent("/api/favorites")
        let key = "\(source)+\(id)"
        let body: [String: Any] = ["key": key, "favorite": data]
        _ = try await authenticatedPOST(url, body: body)
    }

    func removeFavorite(source: String, id: String) async throws {
        let key = "\(source)+\(id)"
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/favorites"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "key", value: key)]
        guard let url = components?.url else { throw APIError.invalidURL }
        _ = try await authenticatedDELETE(url)
    }

    // MARK: - Play Records

    func getPlayRecords() async throws -> [PlayRecord] {
        let url = baseURL.appendingPathComponent("/api/playrecords")
        let (data, _) = try await authenticatedGET(url)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: [String: Any]] else {
            return []
        }
        return json.map { (key, value) in
            PlayRecord.fromJson(key: key, data: value)
        }.sorted { $0.saveTime > $1.saveTime }
    }

    func savePlayRecord(_ record: PlayRecord) async throws {
        let url = baseURL.appendingPathComponent("/api/playrecords")
        let key = record.id
        let body: [String: Any] = ["key": key, "record": record.toJson()]
        _ = try await authenticatedPOST(url, body: body)
    }

    func deletePlayRecord(source: String, id: String) async throws {
        let key = "\(source)+\(id)"
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/playrecords"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "key", value: key)]
        guard let url = components?.url else { throw APIError.invalidURL }
        _ = try await authenticatedDELETE(url)
    }

    func clearPlayRecords() async throws {
        let url = baseURL.appendingPathComponent("/api/playrecords")
        _ = try await authenticatedDELETE(url)
    }

    // MARK: - Search History

    func getSearchHistory() async throws -> [String] {
        let url = baseURL.appendingPathComponent("/api/searchhistory")
        let (data, _) = try await authenticatedGET(url)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String] else {
            return []
        }
        return json
    }

    func addSearchHistory(query: String) async throws {
        let url = baseURL.appendingPathComponent("/api/searchhistory")
        _ = try await authenticatedPOST(url, body: ["keyword": query])
    }

    func deleteSearchHistory(query: String) async throws {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/searchhistory"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "keyword", value: query)]
        guard let url = components?.url else { throw APIError.invalidURL }
        _ = try await authenticatedDELETE(url)
    }

    func clearSearchHistory() async throws {
        let url = baseURL.appendingPathComponent("/api/searchhistory")
        _ = try await authenticatedDELETE(url)
    }

    // MARK: - Search Suggestions

    func searchSuggestions(query: String) async throws -> [SearchSuggestion] {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/search/suggestions"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: query)]
        guard let url = components?.url else { throw APIError.invalidURL }

        let (data, _) = try await authenticatedGET(url)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let suggestions = json["suggestions"] as? [[String: Any]] else {
            return []
        }
        let suggestionData = try JSONSerialization.data(withJSONObject: suggestions)
        return try JSONDecoder().decode([SearchSuggestion].self, from: suggestionData)
    }

    // MARK: - SSE Search URL

    func sseSearchURL(query: String) -> URL? {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/search/ws"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: query)]
        return components?.url
    }

    // MARK: - Helpers

    func extractCookie(from response: HTTPURLResponse) -> String {
        guard let setCookie = response.allHeaderFields["Set-Cookie"] as? String else { return "" }
        let parts = setCookie.split(separator: ";", maxSplits: 1, omittingEmptySubsequences: true)
        return String(parts.first ?? "")
    }

    private func makeAuthenticatedRequest(url: URL, method: String, body: [String: Any]? = nil) throws -> URLRequest {
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let body = body {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = try JSONSerialization.data(withJSONObject: body)
        }
        return request
    }

    private func authenticatedGET(_ url: URL) async throws -> (Data, HTTPURLResponse) {
        let request = try makeAuthenticatedRequest(url: url, method: "GET")
        return try await performRequest(request)
    }

    private func authenticatedPOST(_ url: URL, body: [String: Any]) async throws -> (Data, HTTPURLResponse) {
        let request = try makeAuthenticatedRequest(url: url, method: "POST", body: body)
        return try await performRequest(request)
    }

    private func authenticatedDELETE(_ url: URL) async throws -> (Data, HTTPURLResponse) {
        let request = try makeAuthenticatedRequest(url: url, method: "DELETE")
        return try await performRequest(request)
    }

    private func performRequest(_ request: URLRequest) async throws -> (Data, HTTPURLResponse) {
        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResp = httpResponse as? HTTPURLResponse else {
            throw APIError.message("无效的服务器响应")
        }
        if httpResp.statusCode == 401 {
            throw APIError.unauthorized
        }
        guard (200...299).contains(httpResp.statusCode) else {
            throw APIError.responseError(statusCode: httpResp.statusCode)
        }
        return (data, httpResp)
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter ServerAPIClientExtendedTests 2>&1 | tail -10`
Expected: All 6 tests PASS

- [ ] **Step 5: Run all existing tests to verify no regressions**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test 2>&1 | tail -15`
Expected: All tests PASS

- [ ] **Step 6: Commit**

```bash
git add Sources/SeleneNative/Services/ServerAPIClient.swift Tests/SeleneNativeTests/ServerAPIClientExtendedTests.swift
git commit -m "feat: implement favorites, play records, search history, suggestions, SSE URL in ServerAPIClient"
```

---

## Task 8: Add SSESearchClient

**Files:**
- Create: `Sources/SeleneNative/Services/SSESearchClient.swift`
- Create: `Tests/SeleneNativeTests/SSESearchClientTests.swift`

- [ ] **Step 1: Write the failing test for event parsing**

```swift
// Tests/SeleneNativeTests/SSESearchClientTests.swift
import XCTest
@testable import SeleneNative

final class SSESearchClientTests: XCTestCase {
    func testParseStartEvent() {
        let json = """
        {"type":"start","totalSources":5}
        """
        let event = SSESearchClient.parseSSEEvent(json.data(using: .utf8)!)
        XCTAssertNotNil(event)
        if case .start(let total) = event {
            XCTAssertEqual(total, 5)
        } else {
            XCTFail("Expected start event")
        }
    }

    func testParseSourceResultEvent() {
        let json = """
        {"type":"sourceResult","sourceName":"源1","results":[{"id":"1","title":"影片","poster":"","episodes":[],"episodes_titles":[],"source":"s1","source_name":"源1","year":"2024"}]}
        """
        let event = SSESearchClient.parseSSEEvent(json.data(using: .utf8)!)
        XCTAssertNotNil(event)
        if case .sourceResult(let sourceName, let results) = event {
            XCTAssertEqual(sourceName, "源1")
            XCTAssertEqual(results.count, 1)
            XCTAssertEqual(results.first?.title, "影片")
        } else {
            XCTFail("Expected sourceResult event")
        }
    }

    func testParseSourceErrorEvent() {
        let json = """
        {"type":"sourceError","sourceName":"源2","error":"timeout"}
        """
        let event = SSESearchClient.parseSSEEvent(json.data(using: .utf8)!)
        XCTAssertNotNil(event)
        if case .sourceError(let sourceName, let error) = event {
            XCTAssertEqual(sourceName, "源2")
            XCTAssertEqual(error, "timeout")
        } else {
            XCTFail("Expected sourceError event")
        }
    }

    func testParseCompleteEvent() {
        let json = """
        {"type":"complete"}
        """
        let event = SSESearchClient.parseSSEEvent(json.data(using: .utf8)!)
        XCTAssertNotNil(event)
        if case .complete = event {
            // success
        } else {
            XCTFail("Expected complete event")
        }
    }

    func testParseInvalidJSONReturnsNil() {
        let data = "not json".data(using: .utf8)!
        let event = SSESearchClient.parseSSEEvent(data)
        XCTAssertNil(event)
    }

    func testProgressPercentage() {
        let progress = SSESearchClient.SearchProgress(totalSources: 10, completedSources: 3, currentSource: nil, isComplete: false, error: nil)
        XCTAssertEqual(progress.progressPercentage, 0.3, accuracy: 0.01)
    }

    func testProgressPercentageZeroTotal() {
        let progress = SSESearchClient.SearchProgress(totalSources: 0, completedSources: 0, currentSource: nil, isComplete: false, error: nil)
        XCTAssertEqual(progress.progressPercentage, 0.0)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter SSESearchClientTests 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Services/SSESearchClient.swift
import Foundation

@MainActor
final class SSESearchClient {
    struct SearchProgress: Sendable {
        var totalSources: Int
        var completedSources: Int
        var currentSource: String?
        var isComplete: Bool
        var error: String?

        var progressPercentage: Double {
            guard totalSources > 0 else { return 0.0 }
            return Double(completedSources) / Double(totalSources)
        }
    }

    enum SearchEvent: Sendable {
        case start(totalSources: Int)
        case sourceResult(sourceName: String, results: [SearchResult])
        case sourceError(sourceName: String, error: String)
        case complete
    }

    private var task: Task<Void, Never>?
    private var continuation: AsyncStream<SearchEvent>.Continuation?

    var eventStream: AsyncStream<SearchEvent>!
    private(set) var progress = SearchProgress(totalSources: 0, completedSources: 0, currentSource: nil, isComplete: false, error: nil)

    func startSearch(query: String, serverURL: URL, cookie: String) {
        stopSearch()

        let (stream, continuation) = AsyncStream<SearchEvent>.makeStream()
        self.eventStream = stream
        self.continuation = continuation
        self.progress = SearchProgress(totalSources: 0, completedSources: 0, currentSource: nil, isComplete: false, error: nil)

        task = Task { [weak self] in
            guard let url = URLComponents(url: serverURL.appendingPathComponent("/api/search/ws"), resolvingAgainstBaseURL: false)?
                .queryItems([URLQueryItem(name: "q", value: query)])
                .url else {
                continuation.finish()
                return
            }

            var request = URLRequest(url: url)
            request.httpMethod = "GET"
            request.setValue("text/event-stream", forHTTPHeaderField: "Accept")
            request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")
            request.setValue(cookie, forHTTPHeaderField: "Cookie")
            request.timeoutInterval = 15

            do {
                let (bytes, response) = try await URLSession.shared.bytes(for: request)
                guard let httpResponse = response as? HTTPURLResponse,
                      (200...299).contains(httpResponse.statusCode) else {
                    await MainActor.run { self?.progress.error = "连接失败" }
                    continuation.finish()
                    return
                }

                var buffer = ""
                for try await line in bytes.lines {
                    guard !Task.isCancelled else { break }
                    if line.hasPrefix("data: ") {
                        let jsonStr = String(line.dropFirst(6))
                        if let data = jsonStr.data(using: .utf8),
                           let event = SSESearchClient.parseSSEEvent(data) {
                            continuation.yield(event)
                            await MainActor.run { self?.handleEvent(event) }
                        }
                    } else if line.isEmpty {
                        buffer = ""
                    }
                }
            } catch {
                await MainActor.run { self?.progress.error = error.localizedDescription }
            }
            continuation.finish()
        }
    }

    func stopSearch() {
        task?.cancel()
        task = nil
        continuation?.finish()
        continuation = nil
    }

    private func handleEvent(_ event: SearchEvent) {
        switch event {
        case .start(let total):
            progress.totalSources = total
        case .sourceResult(let sourceName, _):
            progress.completedSources += 1
            progress.currentSource = sourceName
        case .sourceError(let sourceName, let error):
            progress.completedSources += 1
            progress.currentSource = sourceName
            progress.error = error
        case .complete:
            progress.isComplete = true
        }
    }

    /// Parse a single SSE data payload. Thread-safe static method for testability.
    static func parseSSEEvent(_ data: Data) -> SearchEvent? {
        guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let type = json["type"] as? String else {
            return nil
        }

        switch type {
        case "start":
            guard let total = json["totalSources"] as? Int else { return nil }
            return .start(totalSources: total)

        case "sourceResult":
            guard let sourceName = json["sourceName"] as? String,
                  let resultsArray = json["results"] as? [[String: Any]] else { return nil }
            do {
                let resultsData = try JSONSerialization.data(withJSONObject: resultsArray)
                let results = try JSONDecoder().decode([SearchResult].self, from: resultsData)
                return .sourceResult(sourceName: sourceName, results: results)
            } catch {
                return nil
            }

        case "sourceError":
            guard let sourceName = json["sourceName"] as? String,
                  let errorMsg = json["error"] as? String else { return nil }
            return .sourceError(sourceName: sourceName, error: errorMsg)

        case "complete":
            return .complete

        default:
            return nil
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter SSESearchClientTests 2>&1 | tail -10`
Expected: All 7 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Services/SSESearchClient.swift Tests/SeleneNativeTests/SSESearchClientTests.swift
git commit -m "feat: add SSESearchClient with SSE event parsing and progress tracking"
```

---

## Task 9: Add FavoritesStore

**Files:**
- Create: `Sources/SeleneNative/Stores/FavoritesStore.swift`
- Create: `Tests/SeleneNativeTests/FavoritesStoreTests.swift`

- [ ] **Step 1: Write the failing test**

```swift
// Tests/SeleneNativeTests/FavoritesStoreTests.swift
import XCTest
@testable import SeleneNative

final class FavoritesStoreTests: XCTestCase {
    func testInitialState() {
        let store = FavoritesStore()
        XCTAssertTrue(store.favorites.isEmpty)
        XCTAssertFalse(store.isLoading)
    }

    func testIsFavoritedReturnsFalseWhenEmpty() {
        let store = FavoritesStore()
        XCTAssertFalse(store.isFavorited(source: "src", id: "1"))
    }

    func testIsFavoritedReturnsTrueAfterAdding() {
        let store = FavoritesStore()
        let item = FavoriteItem(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", totalEpisodes: 10, saveTime: 100)
        store.favorites = [item]
        XCTAssertTrue(store.isFavorited(source: "src", id: "1"))
    }

    func testIsFavoritedKeyFormat() {
        let store = FavoritesStore()
        let item = FavoriteItem(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", totalEpisodes: 10, saveTime: 100)
        store.favorites = [item]
        XCTAssertTrue(store.isFavorited(source: "src", id: "1"))
        XCTAssertFalse(store.isFavorited(source: "other", id: "1"))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter FavoritesStoreTests 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Stores/FavoritesStore.swift
import Foundation

@Observable
final class FavoritesStore {
    var favorites: [FavoriteItem] = []
    var isLoading: Bool = false

    func loadFavorites(provider: ContentProvider) async {
        isLoading = true
        do {
            let items = try await provider.getFavorites()
            favorites = items
        } catch {
            // Non-critical; keep existing data
        }
        isLoading = false
    }

    func toggleFavorite(source: String, id: String, data: [String: Any], provider: ContentProvider) async {
        let key = "\(source)+\(id)"
        if isFavorited(source: source, id: id) {
            do {
                try await provider.removeFavorite(source: source, id: id)
                favorites.removeAll { $0.id == key }
            } catch { /* keep local state */ }
        } else {
            do {
                try await provider.addFavorite(source: source, id: id, data: data)
                let item = FavoriteItem.fromJson(key: key, data: data)
                favorites.append(item)
            } catch { /* keep local state */ }
        }
    }

    func isFavorited(source: String, id: String) -> Bool {
        let key = "\(source)+\(id)"
        return favorites.contains { $0.id == key }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter FavoritesStoreTests 2>&1 | tail -10`
Expected: All 4 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Stores/FavoritesStore.swift Tests/SeleneNativeTests/FavoritesStoreTests.swift
git commit -m "feat: add FavoritesStore with toggle and lookup"
```

---

## Task 10: Add HistoryStore

**Files:**
- Create: `Sources/SeleneNative/Stores/HistoryStore.swift`
- Create: `Tests/SeleneNativeTests/HistoryStoreTests.swift`

- [ ] **Step 1: Write the failing test**

```swift
// Tests/SeleneNativeTests/HistoryStoreTests.swift
import XCTest
@testable import SeleneNative

final class HistoryStoreTests: XCTestCase {
    func testInitialState() {
        let store = HistoryStore()
        XCTAssertTrue(store.playRecords.isEmpty)
        XCTAssertFalse(store.isLoading)
    }

    func testRecordForReturnsNilWhenEmpty() {
        let store = HistoryStore()
        XCTAssertNil(store.recordFor(source: "src", id: "1"))
    }

    func testResumePositionReturnsNilWhenEmpty() {
        let store = HistoryStore()
        XCTAssertNil(store.resumePosition(source: "src", id: "1"))
    }

    func testRecordForReturnsMatchingRecord() {
        let store = HistoryStore()
        let record = PlayRecord(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", index: 3, totalEpisodes: 10, playTime: 120, totalTime: 2700, saveTime: 100, searchTitle: "")
        store.playRecords = [record]
        let found = store.recordFor(source: "src", id: "1")
        XCTAssertNotNil(found)
        XCTAssertEqual(found?.index, 3)
    }

    func testResumePositionReturnsIndexAndTime() {
        let store = HistoryStore()
        let record = PlayRecord(id: "src+1", source: "src", title: "T", sourceName: "S", year: "2024", cover: "", index: 5, totalEpisodes: 10, playTime: 300, totalTime: 2700, saveTime: 100, searchTitle: "")
        store.playRecords = [record]
        let pos = store.resumePosition(source: "src", id: "1")
        XCTAssertNotNil(pos)
        XCTAssertEqual(pos?.index, 5)
        XCTAssertEqual(pos?.playTime, 300)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter HistoryStoreTests 2>&1 | tail -10`
Expected: FAIL

- [ ] **Step 3: Write the implementation**

```swift
// Sources/SeleneNative/Stores/HistoryStore.swift
import Foundation

@Observable
final class HistoryStore {
    var playRecords: [PlayRecord] = []
    var isLoading: Bool = false

    func loadRecords(provider: ContentProvider) async {
        isLoading = true
        do {
            let records = try await provider.getPlayRecords()
            playRecords = records
        } catch {
            // Non-critical
        }
        isLoading = false
    }

    func saveRecord(_ record: PlayRecord, provider: ContentProvider) async {
        do {
            try await provider.savePlayRecord(record)
            // Update local cache
            if let idx = playRecords.firstIndex(where: { $0.id == record.id }) {
                playRecords[idx] = record
            } else {
                playRecords.append(record)
            }
        } catch { /* keep local state */ }
    }

    func deleteRecord(source: String, id: String, provider: ContentProvider) async {
        do {
            try await provider.deletePlayRecord(source: source, id: id)
            let key = "\(source)+\(id)"
            playRecords.removeAll { $0.id == key }
        } catch { /* keep local state */ }
    }

    func clearRecords(provider: ContentProvider) async {
        do {
            try await provider.clearPlayRecords()
            playRecords = []
        } catch { /* keep local state */ }
    }

    func recordFor(source: String, id: String) -> PlayRecord? {
        let key = "\(source)+\(id)"
        return playRecords.first { $0.id == key }
    }

    func resumePosition(source: String, id: String) -> (index: Int, playTime: Int)? {
        guard let record = recordFor(source: source, id: id) else { return nil }
        return (index: record.index, playTime: record.playTime)
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test --filter HistoryStoreTests 2>&1 | tail -10`
Expected: All 5 tests PASS

- [ ] **Step 5: Commit**

```bash
git add Sources/SeleneNative/Stores/HistoryStore.swift Tests/SeleneNativeTests/HistoryStoreTests.swift
git commit -m "feat: add HistoryStore with record lookup and resume position"
```

---

## Task 11: Enhance SearchStore with SSE, Aggregation, Filters

**Files:**
- Modify: `Sources/SeleneNative/Stores/SearchStore.swift`

- [ ] **Step 1: Replace SearchStore with enhanced version**

Replace the entire file with:

```swift
// Sources/SeleneNative/Stores/SearchStore.swift
import SwiftUI
import Combine

@Observable
final class SearchStore {
    var query: String = ""
    var results: [SearchResult] = []
    var aggregatedResults: [AggregatedSearchResult] = []
    var isLoading: Bool = false
    var selectedResult: SearchResult?
    var errorMessage: String?
    var resources: [SearchResource] = []

    // SSE search state
    var searchProgress: SSESearchClient.SearchProgress?
    var isSSESearching: Bool = false

    // Aggregation toggle
    var useAggregatedView: Bool = false

    // Filters
    var selectedSource: String?
    var selectedYear: String?
    var selectedTitle: String?
    var yearSortOrder: YearSortOrder = .none

    // Search history
    var searchHistory: [String] = []

    // Suggestions
    var suggestions: [SearchSuggestion] = []
    var showSuggestions: Bool = false

    enum YearSortOrder: Int, CaseIterable {
        case none = 0
        case descending = 1
        case ascending = 2
    }

    private let provider: ContentProvider
    private var sseClient: SSESearchClient?
    private var suggestionTask: Task<Void, Never>?

    init(provider: ContentProvider) {
        self.provider = provider
    }

    // MARK: - Search

    func search() async {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }

        isLoading = true
        errorMessage = nil
        results = []
        aggregatedResults = []

        do {
            let searchResults = try await provider.search(query: query)
            results = searchResults
            buildAggregatedResults()
            isLoading = false
        } catch {
            errorMessage = error.localizedDescription
            isLoading = false
        }
    }

    func startSSESearch() async {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        guard let session = (provider as? ServerAPIClient)?.baseURL,
              let cookie = "" as String? else { return }

        isSSESearching = true
        errorMessage = nil
        results = []
        aggregatedResults = []
        searchProgress = nil

        let client = SSESearchClient()
        self.sseClient = client

        // We need the cookie from the session store — this will be wired in the view layer
        // For now, start the SSE client with the provider's base URL
        if let apiClient = provider as? ServerAPIClient {
            // Cookie will be injected from SessionStore in the view
            client.startSearch(query: query, serverURL: apiClient.baseURL, cookie: "")
        }

        // Consume events
        Task { @MainActor in
            for await event in client.eventStream {
                switch event {
                case .start(let total):
                    searchProgress = SSESearchClient.SearchProgress(
                        totalSources: total, completedSources: 0,
                        currentSource: nil, isComplete: false, error: nil
                    )
                case .sourceResult(_, let newResults):
                    results.append(contentsOf: newResults)
                    buildAggregatedResults()
                case .sourceError(let sourceName, let error):
                    searchProgress?.currentSource = sourceName
                    searchProgress?.error = error
                case .complete:
                    isSSESearching = false
                    searchProgress?.isComplete = true
                }
            }
        }
    }

    func stopSSESearch() {
        sseClient?.stopSearch()
        isSSESearching = false
    }

    // MARK: - Aggregation

    private func buildAggregatedResults() {
        var map: [String: AggregatedSearchResult] = [:]
        for result in results {
            let typeName = result.typeName ?? "tv"
            let key = "\(result.title)+\(result.year)+\(typeName)"
            if var existing = map[key] {
                existing.addResult(result)
                map[key] = existing
            } else {
                map[key] = AggregatedSearchResult.fromSearchResult(result)
            }
        }
        aggregatedResults = Array(map.values).sorted { $0.addedTimestamp < $1.addedTimestamp }
    }

    // MARK: - Filters

    var filteredResults: [SearchResult] {
        var filtered = results

        if let source = selectedSource {
            filtered = filtered.filter { $0.sourceName == source }
        }
        if let year = selectedYear {
            filtered = filtered.filter { $0.year == year }
        }
        if let title = selectedTitle {
            filtered = filtered.filter { $0.title.contains(title) }
        }

        switch yearSortOrder {
        case .descending:
            filtered.sort { $0.year > $1.year }
        case .ascending:
            filtered.sort { $0.year < $1.year }
        case .none:
            break
        }

        return filtered
    }

    var filteredAggregatedResults: [AggregatedSearchResult] {
        var filtered = aggregatedResults

        if let source = selectedSource {
            filtered = filtered.filter { $0.sourceNames.contains(source) }
        }
        if let year = selectedYear {
            filtered = filtered.filter { $0.year == year }
        }

        switch yearSortOrder {
        case .descending:
            filtered.sort { $0.year > $1.year }
        case .ascending:
            filtered.sort { $0.year < $1.year }
        case .none:
            break
        }

        return filtered
    }

    var availableSources: [String] {
        Array(Set(results.map { $0.sourceName })).sorted()
    }

    var availableYears: [String] {
        Array(Set(results.map { $0.year })).filter { !$0.isEmpty }.sorted().reversed()
    }

    func clearFilters() {
        selectedSource = nil
        selectedYear = nil
        selectedTitle = nil
        yearSortOrder = .none
    }

    // MARK: - Resources

    func loadResources() async {
        do {
            let res = try await provider.searchResources()
            resources = res.filter { !$0.disabled }
        } catch {
            // Non-critical
        }
    }

    // MARK: - Selection

    func selectResult(_ result: SearchResult) {
        selectedResult = result
    }

    func clearSelection() {
        selectedResult = nil
    }

    func clearError() {
        errorMessage = nil
    }

    // MARK: - Search History

    func loadSearchHistory() async {
        do {
            searchHistory = try await provider.getSearchHistory()
        } catch { /* non-critical */ }
    }

    func addToSearchHistory(_ query: String) async {
        do {
            try await provider.addSearchHistory(query: query)
            if !searchHistory.contains(query) {
                searchHistory.insert(query, at: 0)
            }
        } catch { /* non-critical */ }
    }

    func removeFromSearchHistory(_ query: String) async {
        do {
            try await provider.deleteSearchHistory(query: query)
            searchHistory.removeAll { $0 == query }
        } catch { /* non-critical */ }
    }

    func clearSearchHistory() async {
        do {
            try await provider.clearSearchHistory()
            searchHistory = []
        } catch { /* non-critical */ }
    }

    // MARK: - Suggestions

    func fetchSuggestions() {
        suggestionTask?.cancel()
        let q = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard q.count >= 2 else {
            suggestions = []
            showSuggestions = false
            return
        }

        suggestionTask = Task { @MainActor in
            try? await Task.sleep(nanoseconds: 500_000_000) // 500ms debounce
            guard !Task.isCancelled else { return }
            do {
                let results = try await provider.searchSuggestions(query: q)
                suggestions = results
                showSuggestions = !results.isEmpty
            } catch {
                suggestions = []
                showSuggestions = false
            }
        }
    }

    func dismissSuggestions() {
        showSuggestions = false
        suggestions = []
        suggestionTask?.cancel()
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds (may have warnings about unused code, that's fine)

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Stores/SearchStore.swift
git commit -m "feat: enhance SearchStore with SSE search, aggregation, filters, history, suggestions"
```

---

## Task 12: Enhance PlayerStore with Progress Saving and Multi-Source

**Files:**
- Modify: `Sources/SeleneNative/Stores/PlayerStore.swift`

- [ ] **Step 1: Replace PlayerStore with enhanced version**

Replace the entire file with:

```swift
// Sources/SeleneNative/Stores/PlayerStore.swift
import SwiftUI
import AVKit

@MainActor
final class PlayerStore: ObservableObject {
    @Published var player: AVPlayer?
    @Published var playbackError: String?
    @Published var currentEpisodeURL: URL?
    @Published var currentEpisodeIndex: Int = 0
    @Published var isEpisodesReversed: Bool = false
    @Published var allSources: [SearchResult] = []
    @Published var currentSourceIndex: Int = 0

    // Progress tracking
    @Published var currentPlayTime: Double = 0
    @Published var currentTotalTime: Double = 0
    private var progressTimer: Timer?
    private var timeObserver: Any?

    private var playerObserver: NSKeyValueObservation?

    init() {}

    var currentSource: SearchResult? {
        guard currentSourceIndex < allSources.count else { return nil }
        return allSources[currentSourceIndex]
    }

    func loadEpisode(url: URL, index: Int = 0) {
        playbackError = nil
        currentEpisodeURL = url
        currentEpisodeIndex = index

        let playerItem = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: playerItem)
        self.player = player

        playerObserver = playerItem.observe(
            \.status,
            options: [.new, .old]
        ) { [weak self] item, _ in
            Task { @MainActor in
                if item.status == .failed {
                    self?.playbackError = self?.playerItemErrorDescription(item.error)
                }
            }
        }

        setupTimeObserver(player: player)
    }

    func play() {
        player?.play()
        startProgressTimer()
    }

    func pause() {
        player?.pause()
        stopProgressTimer()
    }

    func replaceItem(url: URL, index: Int = 0) {
        playerObserver?.invalidate()
        playerObserver = nil
        removeTimeObserver()
        stopProgressTimer()
        loadEpisode(url: url, index: index)
    }

    func stop() {
        player?.pause()
        removeTimeObserver()
        stopProgressTimer()
        player = nil
        playerObserver?.invalidate()
        playerObserver = nil
        currentEpisodeURL = nil
        playbackError = nil
        currentPlayTime = 0
        currentTotalTime = 0
    }

    // MARK: - Multi-Source

    func setAllSources(_ sources: [SearchResult], currentSource: SearchResult) {
        allSources = sources
        currentSourceIndex = sources.firstIndex(where: { $0.source == currentSource.source }) ?? 0
    }

    func switchToSource(index: Int) {
        guard index < allSources.count else { return }
        currentSourceIndex = index
    }

    // MARK: - Episode Reverse

    func toggleEpisodeOrder() {
        isEpisodesReversed.toggle()
    }

    func episodeIndices(for result: SearchResult) -> [Int] {
        let indices = Array(result.episodes.indices)
        return isEpisodesReversed ? indices.reversed() : indices
    }

    // MARK: - Progress Tracking

    private func setupTimeObserver(player: AVPlayer) {
        let interval = CMTime(seconds: 1, preferredTimescale: 600)
        timeObserver = player.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
            Task { @MainActor in
                self?.currentPlayTime = time.seconds
                if let duration = player.currentItem?.duration.seconds, duration.isFinite && duration > 0 {
                    self?.currentTotalTime = duration
                }
            }
        }
    }

    private func removeTimeObserver() {
        if let observer = timeObserver {
            player?.removeTimeObserver(observer)
            timeObserver = nil
        }
    }

    private func startProgressTimer() {
        stopProgressTimer()
        progressTimer = Timer.scheduledTimer(withTimeInterval: 10.0, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.autoSaveProgress()
            }
        }
    }

    private func stopProgressTimer() {
        progressTimer?.invalidate()
        progressTimer = nil
    }

    /// Called every 10 seconds and on pause/stop. The view layer will call saveCurrentProgress(provider:)
    /// which uses the HistoryStore.
    private func autoSaveProgress() {
        // Progress saving is triggered from the view layer via saveCurrentProgress()
        // This timer just signals that a save should happen
    }

    func saveCurrentProgress(result: SearchResult, historyStore: HistoryStore, provider: ContentProvider) async {
        guard currentEpisodeURL != nil else { return }
        let record = PlayRecord(
            id: "\(result.source)+\(result.id)",
            source: result.source,
            title: result.title,
            sourceName: result.sourceName,
            year: result.year,
            cover: result.poster,
            index: currentEpisodeIndex,
            totalEpisodes: result.episodes.count,
            playTime: Int(currentPlayTime),
            totalTime: Int(currentTotalTime),
            saveTime: Int64(Date().timeIntervalSince1970 * 1000),
            searchTitle: ""
        )
        await historyStore.saveRecord(record, provider: provider)
    }

    private func playerItemErrorDescription(_ error: Error?) -> String {
        guard let error = error else { return "播放失败" }
        let nsError = error as NSError
        if nsError.domain == NSURLErrorDomain {
            switch nsError.code {
            case NSURLErrorNotConnectedToInternet:
                return "网络连接失败，请检查网络"
            case NSURLErrorTimedOut:
                return "连接超时"
            default:
                return "播放失败: \(error.localizedDescription)"
            }
        }
        return "播放失败: \(error.localizedDescription)"
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Stores/PlayerStore.swift
git commit -m "feat: enhance PlayerStore with progress tracking, multi-source, episode reverse"
```

---

## Task 13: Add VideoCardView

**Files:**
- Create: `Sources/SeleneNative/Views/VideoCardView.swift`

- [ ] **Step 1: Write the reusable video card component**

```swift
// Sources/SeleneNative/Views/VideoCardView.swift
import SwiftUI

struct VideoCardView: View {
    let title: String
    let sourceName: String
    let year: String
    let posterURL: String
    let subtitle: String?
    let progress: Double?  // 0.0 to 1.0, nil = no progress bar

    init(title: String, sourceName: String, year: String, posterURL: String, subtitle: String? = nil, progress: Double? = nil) {
        self.title = title
        self.sourceName = sourceName
        self.year = year
        self.posterURL = posterURL
        self.subtitle = subtitle
        self.progress = progress
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            posterImage
            infoSection
        }
    }

    private var posterImage: some View {
        ZStack(alignment: .bottom) {
            if !posterURL.isEmpty, let url = URL(string: posterURL) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .empty:
                        placeholderPoster
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                    case .failure:
                        placeholderPoster
                    @unknown default:
                        placeholderPoster
                    }
                }
                .aspectRatio(2/3, contentMode: .fit)
                .clipped()
                .cornerRadius(6)
            } else {
                placeholderPoster
            }

            if let progress = progress, progress > 0 {
                GeometryReader { geo in
                    Rectangle()
                        .fill(Color.accentColor)
                        .frame(width: geo.size.width * min(progress, 1.0), height: 3)
                }
                .frame(height: 3)
            }
        }
    }

    private var placeholderPoster: some View {
        Rectangle()
            .fill(Color.gray.opacity(0.2))
            .aspectRatio(2/3, contentMode: .fit)
            .cornerRadius(6)
            .overlay {
                Image(systemName: "film")
                    .foregroundStyle(.secondary)
            }
    }

    private var infoSection: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.caption)
                .lineLimit(2)
                .foregroundStyle(.primary)

            HStack(spacing: 4) {
                Text(sourceName)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                if !year.isEmpty {
                    Text("·")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    Text(year)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            }

            if let subtitle = subtitle {
                Text(subtitle)
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
                    .lineLimit(1)
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Views/VideoCardView.swift
git commit -m "feat: add VideoCardView reusable component with poster and progress"
```

---

## Task 14: Add SearchSuggestionOverlay

**Files:**
- Create: `Sources/SeleneNative/Views/SearchSuggestionOverlay.swift`

- [ ] **Step 1: Write the suggestion overlay**

```swift
// Sources/SeleneNative/Views/SearchSuggestionOverlay.swift
import SwiftUI

struct SearchSuggestionOverlay: View {
    let suggestions: [SearchSuggestion]
    let onSelect: (String) -> Void

    var body: some View {
        if !suggestions.isEmpty {
            VStack(spacing: 0) {
                ForEach(suggestions.prefix(8)) { suggestion in
                    Button {
                        onSelect(suggestion.text)
                    } label: {
                        HStack {
                            Image(systemName: "magnifyingglass")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            Text(suggestion.text)
                                .font(.body)
                                .foregroundStyle(.primary)
                            Spacer()
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 8)
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)

                    if suggestion.text != suggestions.prefix(8).last?.text {
                        Divider().padding(.horizontal, 12)
                    }
                }
            }
            .background(.regularMaterial)
            .cornerRadius(8)
            .shadow(color: .black.opacity(0.15), radius: 4, y: 2)
        }
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Views/SearchSuggestionOverlay.swift
git commit -m "feat: add SearchSuggestionOverlay dropdown component"
```

---

## Task 15: Add PlayerSourcesView and PlayerEpisodesView

**Files:**
- Create: `Sources/SeleneNative/Views/PlayerSourcesView.swift`
- Create: `Sources/SeleneNative/Views/PlayerEpisodesView.swift`

- [ ] **Step 1: Write PlayerSourcesView**

```swift
// Sources/SeleneNative/Views/PlayerSourcesView.swift
import SwiftUI

struct PlayerSourcesView: View {
    let sources: [SearchResult]
    let currentSourceIndex: Int
    let onSelectSource: (Int) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("切换源 (\(sources.count))")
                    .font(.headline)
                Spacer()
            }
            .padding(.horizontal)

            ScrollView {
                LazyVStack(spacing: 8) {
                    ForEach(sources.indices, id: \.self) { index in
                        sourceCard(index: index)
                    }
                }
                .padding(.horizontal)
            }
        }
    }

    private func sourceCard(index: Int) -> some View {
        let source = sources[index]
        let isCurrent = index == currentSourceIndex

        return Button {
            onSelectSource(index)
        } label: {
            HStack(spacing: 10) {
                if !source.poster.isEmpty, let url = URL(string: source.poster) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            image.resizable().scaledToFill()
                        default:
                            Color.gray.opacity(0.2)
                        }
                    }
                    .frame(width: 40, height: 60)
                    .clipped()
                    .cornerRadius(4)
                }

                VStack(alignment: .leading, spacing: 2) {
                    Text(source.title)
                        .font(.body)
                        .lineLimit(1)
                    Text(source.sourceName)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text("共\(source.episodes.count)集")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                if isCurrent {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                }
            }
            .padding(8)
            .background(isCurrent ? Color.accentColor.opacity(0.1) : Color.clear)
            .cornerRadius(6)
            .overlay(
                RoundedRectangle(cornerRadius: 6)
                    .stroke(isCurrent ? Color.accentColor : Color.secondary.opacity(0.2), lineWidth: isCurrent ? 2 : 1)
            )
        }
        .buttonStyle(.plain)
    }
}
```

- [ ] **Step 2: Write PlayerEpisodesView**

```swift
// Sources/SeleneNative/Views/PlayerEpisodesView.swift
import SwiftUI

struct PlayerEpisodesView: View {
    let result: SearchResult
    let currentIndex: Int
    let isReversed: Bool
    let onToggleReverse: () -> Void
    let onSelectEpisode: (Int) -> Void

    private var episodeIndices: [Int] {
        let indices = Array(result.episodes.indices)
        return isReversed ? indices.reversed() : indices
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("剧集 (\(result.episodes.count))")
                    .font(.headline)
                Spacer()
                Button {
                    onToggleReverse()
                } label: {
                    Image(systemName: isReversed ? "arrow.up.to.line" : "arrow.down.to.line")
                        .font(.caption)
                }
                .buttonStyle(.plain)
                .help(isReversed ? "正序" : "倒序")
            }
            .padding(.horizontal)

            ScrollView {
                LazyVGrid(
                    columns: [GridItem(.adaptive(minimum: 80, maximum: 120), spacing: 8)],
                    spacing: 8
                ) {
                    ForEach(episodeIndices, id: \.self) { index in
                        Button {
                            onSelectEpisode(index)
                        } label: {
                            Text(result.episodeTitle(for: index))
                                .font(.caption)
                                .foregroundStyle(index == currentIndex ? .white : .primary)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .frame(maxWidth: .infinity)
                                .background(index == currentIndex ? Color.accentColor : .regularMaterial)
                                .cornerRadius(6)
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal)
            }
        }
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add Sources/SeleneNative/Views/PlayerSourcesView.swift Sources/SeleneNative/Views/PlayerEpisodesView.swift
git commit -m "feat: add PlayerSourcesView and PlayerEpisodesView panels"
```

---

## Task 16: Redesign MainView with NavigationSplitView Sidebar

**Files:**
- Modify: `Sources/SeleneNative/Views/MainView.swift`

- [ ] **Step 1: Replace MainView with sidebar navigation layout**

Replace the entire file with:

```swift
// Sources/SeleneNative/Views/MainView.swift
import SwiftUI

enum NavigationPage: String, CaseIterable, Identifiable {
    case home = "首页"
    case search = "搜索"
    case movie = "电影"
    case tv = "电视剧"
    case anime = "动漫"
    case show = "综艺"
    case live = "直播"
    case favorites = "收藏"
    case history = "历史"
    case settings = "设置"

    var id: String { rawValue }

    var systemImage: String {
        switch self {
        case .home: return "house"
        case .search: return "magnifyingglass"
        case .movie: return "film"
        case .tv: return "tv"
        case .anime: return "sparkles"
        case .show: return "theatermasks"
        case .live: return "antenna.radiowaves.left.and.right"
        case .favorites: return "heart"
        case .history: return "clock"
        case .settings: return "gearshape"
        }
    }
}

struct MainView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var selectedPage: NavigationPage? = .home
    @State private var provider: ServerAPIClient
    @State private var searchStore: SearchStore
    @State private var playerStore: PlayerStore
    @State private var favoritesStore = FavoritesStore()
    @State private var historyStore = HistoryStore()

    init() {
        let placeholderURL = URL(string: "https://example.com")!
        _provider = State(initialValue: ServerAPIClient(baseURL: placeholderURL))
        _searchStore = State(initialValue: SearchStore(provider: ServerAPIClient(baseURL: placeholderURL)))
        _playerStore = State(initialValue: PlayerStore())
    }

    var body: some View {
        NavigationSplitView {
            sidebar
        } detail: {
            detailView
        }
        .task {
            if let url = sessionStore.session?.serverURL {
                let newProvider = ServerAPIClient(baseURL: url)
                provider = newProvider
                searchStore = SearchStore(provider: newProvider)
                await searchStore.loadResources()
                await favoritesStore.loadFavorites(provider: newProvider)
                await historyStore.loadRecords(provider: newProvider)
                await searchStore.loadSearchHistory()
            }
        }
    }

    private var sidebar: some View {
        List(selection: $selectedPage) {
            Section("浏览") {
                ForEach([NavigationPage.home, .search, .movie, .tv, .anime, .show, .live], id: \.self) { page in
                    Label(page.rawValue, systemImage: page.systemImage)
                        .tag(page)
                }
            }
            Section("我的") {
                ForEach([NavigationPage.favorites, .history], id: \.self) { page in
                    Label(page.rawValue, systemImage: page.systemImage)
                        .tag(page)
                }
            }
            Section {
                Label(NavigationPage.settings.rawValue, systemImage: NavigationPage.settings.systemImage)
                    .tag(NavigationPage.settings)
            }
        }
        .listStyle(.sidebar)
        .navigationTitle("Selene")
    }

    @ViewBuilder
    private var detailView: some View {
        switch selectedPage {
        case .home:
            Text("首页 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .search:
            enhancedSearchView
        case .movie:
            Text("电影 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .tv:
            Text("电视剧 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .anime:
            Text("动漫 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .show:
            Text("综艺 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .live:
            Text("直播 — 将在 P3 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .favorites:
            Text("收藏 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .history:
            Text("历史 — 将在 P2 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .settings:
            Text("设置 — 将在 P4 实现")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .none:
            ContentUnavailableView {
                Label("选择一个页面", systemImage: "sidebar.left")
            }
        }
    }

    private var enhancedSearchView: some View {
        VStack(spacing: 0) {
            // Search bar with suggestions
            searchBar

            // SSE progress bar
            if searchStore.isSSESearching, let progress = searchStore.searchProgress {
                HStack {
                    ProgressView(value: progress.progressPercentage)
                        .progressViewStyle(.linear)
                    Text("\(progress.completedSources)/\(progress.totalSources)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .padding(.horizontal)
                .padding(.vertical, 4)
            }

            Divider()

            // Filter bar
            if !searchStore.results.isEmpty {
                filterBar
                Divider()
            }

            // Content
            HSplitView {
                // Left: Results
                resultsPanel
                    .frame(minWidth: 320)

                // Right: Detail + Player
                detailAndPlayerPanel
                    .frame(minWidth: 300)
            }
        }
    }

    private var searchBar: some View {
        HStack {
            TextField("搜索...", text: $searchStore.query)
                .textFieldStyle(.roundedBorder)
                .onChange(of: searchStore.query) {
                    searchStore.fetchSuggestions()
                }
                .onSubmit {
                    Task {
                        await searchStore.addToSearchHistory(searchStore.query)
                        await searchStore.search()
                    }
                }

            Button(searchStore.isLoading ? "搜索中..." : "搜索") {
                Task {
                    await searchStore.addToSearchHistory(searchStore.query)
                    await searchStore.search()
                }
            }
            .disabled(searchStore.isLoading || searchStore.query.isEmpty)
        }
        .padding()
        .overlay(alignment: .topLeading) {
            if searchStore.showSuggestions {
                SearchSuggestionOverlay(suggestions: searchStore.suggestions) { text in
                    searchStore.query = text
                    searchStore.dismissSuggestions()
                    Task {
                        await searchStore.addToSearchHistory(text)
                        await searchStore.search()
                    }
                }
                .offset(y: 50)
                .padding(.horizontal)
            }
        }
    }

    private var filterBar: some View {
        HStack(spacing: 12) {
            // Aggregation toggle
            Toggle(isOn: $searchStore.useAggregatedView) {
                Text("聚合")
                    .font(.caption)
            }
            .toggleStyle(.checkbox)

            // Source filter
            if searchStore.availableSources.count > 1 {
                Picker("源", selection: $searchStore.selectedSource) {
                    Text("全部").tag(String?.none)
                    ForEach(searchStore.availableSources, id: \.self) { source in
                        Text(source).tag(String?.some(source))
                    }
                }
                .frame(width: 120)
            }

            // Year filter
            if searchStore.availableYears.count > 1 {
                Picker("年份", selection: $searchStore.selectedYear) {
                    Text("全部").tag(String?.none)
                    ForEach(searchStore.availableYears, id: \.self) { year in
                        Text(year).tag(String?.some(year))
                    }
                }
                .frame(width: 100)
            }

            // Year sort
            Button {
                searchStore.yearSortOrder = SearchStore.YearSortOrder(rawValue: (searchStore.yearSortOrder.rawValue + 1) % 3) ?? .none
            } label: {
                Image(systemName: searchStore.yearSortOrder == .ascending ? "arrow.up" : searchStore.yearSortOrder == .descending ? "arrow.down" : "arrow.up.arrow.down")
            }
            .buttonStyle(.plain)
            .help("年份排序")

            Spacer()

            if !searchStore.results.isEmpty {
                Text("\(searchStore.filteredResults.count) 个结果")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal)
        .padding(.vertical, 6)
    }

    private var resultsPanel: some View {
        Group {
            if searchStore.isLoading && searchStore.results.isEmpty {
                ProgressView("搜索中...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if searchStore.results.isEmpty && !searchStore.query.isEmpty && !searchStore.isSSESearching {
                ContentUnavailableView("无结果", systemImage: "magnifyingglass", description: Text("尝试其他关键词"))
            } else if let error = searchStore.errorMessage {
                VStack {
                    Text("出错了").font(.headline)
                    Text(error).font(.caption).foregroundStyle(.secondary)
                    Button("重试") { Task { await searchStore.search() } }
                }
                .padding()
            } else if searchStore.results.isEmpty {
                // Show search history when no results
                if searchStore.searchHistory.isEmpty {
                    ContentUnavailableView("开始搜索", systemImage: "film", description: Text("在服务器上搜索视频内容"))
                } else {
                    searchHistoryView
                }
            } else if searchStore.useAggregatedView {
                List(searchStore.filteredAggregatedResults, selection: $searchStore.selectedResult) { agg in
                    VStack(alignment: .leading, spacing: 4) {
                        Text(agg.title).font(.body).lineLimit(1)
                        HStack(spacing: 8) {
                            ForEach(agg.sourceNames, id: \.self) { name in
                                Text(name).font(.caption2)
                                    .padding(.horizontal, 4)
                                    .background(Color.secondary.opacity(0.15))
                                    .cornerRadius(3)
                            }
                            Text("\(agg.mostCommonEpisodeCount)集").font(.caption2).foregroundStyle(.secondary)
                        }
                    }
                    .padding(.vertical, 2)
                }
            } else {
                List(searchStore.filteredResults, selection: $searchStore.selectedResult) { result in
                    SearchResultRow(result: result)
                        .onTapGesture { searchStore.selectResult(result) }
                }
            }
        }
    }

    private var searchHistoryView: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("搜索历史").font(.headline)
                Spacer()
                Button("清空") {
                    Task { await searchStore.clearSearchHistory() }
                }
                .buttonStyle(.plain)
                .foregroundStyle(.red)
            }
            .padding(.horizontal)

            FlowLayout(spacing: 8) {
                ForEach(searchStore.searchHistory, id: \.self) { query in
                    Button(query) {
                        searchStore.query = query
                        Task { await searchStore.search() }
                    }
                    .buttonStyle(.plain)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.regularMaterial)
                    .cornerRadius(12)
                }
            }
            .padding(.horizontal)
            Spacer()
        }
        .padding(.top)
    }

    private var detailAndPlayerPanel: some View {
        VStack(spacing: 0) {
            if let result = searchStore.selectedResult {
                DetailView(
                    result: result,
                    onPlay: { url in
                        let index = result.episodes.firstIndex(of: url.absoluteString) ?? 0
                        playerStore.replaceItem(url: url, index: index)
                        playerStore.play()
                    }
                )

                // Source switching bar
                if playerStore.allSources.count > 1 {
                    Divider()
                    PlayerSourcesView(
                        sources: playerStore.allSources,
                        currentSourceIndex: playerStore.currentSourceIndex,
                        onSelectSource: { index in
                            playerStore.switchToSource(index: index)
                        }
                    )
                    .frame(maxHeight: 120)
                }
            } else {
                ContentUnavailableView {
                    Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            }

            if playerStore.currentEpisodeURL != nil {
                Divider()
                PlayerView(playerStore: playerStore)
                    .frame(height: 200)
            }
        }
    }
}

/// Simple flow layout for search history chips
struct FlowLayout: Layout {
    var spacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let result = arrange(proposal: proposal, subviews: subviews)
        return result.size
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let result = arrange(proposal: proposal, subviews: subviews)
        for (index, position) in result.positions.enumerated() {
            subviews[index].place(at: CGPoint(x: bounds.minX + position.x, y: bounds.minY + position.y), proposal: .unspecified)
        }
    }

    private struct Arrangement {
        var positions: [CGPoint]
        var size: CGSize
    }

    private func arrange(proposal: ProposedViewSize, subviews: Subviews) -> Arrangement {
        let maxWidth = proposal.width ?? .infinity
        var positions: [CGPoint] = []
        var x: CGFloat = 0
        var y: CGFloat = 0
        var rowHeight: CGFloat = 0

        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x + size.width > maxWidth && x > 0 {
                x = 0
                y += rowHeight + spacing
                rowHeight = 0
            }
            positions.append(CGPoint(x: x, y: y))
            rowHeight = max(rowHeight, size.height)
            x += size.width + spacing
        }

        return Arrangement(
            positions: positions,
            size: CGSize(width: maxWidth, height: y + rowHeight)
        )
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add Sources/SeleneNative/Views/MainView.swift
git commit -m "feat: redesign MainView with NavigationSplitView sidebar, search filters, aggregation, history"
```

---

## Task 17: Update SeleneNativeApp to Inject New Stores

**Files:**
- Modify: `Sources/SeleneNative/App/SeleneNativeApp.swift`

- [ ] **Step 1: Add new stores to the app environment**

Replace the entire file with:

```swift
// Sources/SeleneNative/App/SeleneNativeApp.swift
import SwiftUI

@main
struct SeleneNativeApp: App {
    @State private var sessionStore = SessionStore()
    @State private var favoritesStore = FavoritesStore()
    @State private var historyStore = HistoryStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(sessionStore)
                .environment(favoritesStore)
                .environment(historyStore)
        }
        .windowStyle(.titleBar)
        .windowResizability(.contentSize)
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 3: Run all tests**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test 2>&1 | tail -15`
Expected: All tests PASS

- [ ] **Step 4: Commit**

```bash
git add Sources/SeleneNative/App/SeleneNativeApp.swift
git commit -m "feat: inject FavoritesStore and HistoryStore into app environment"
```

---

## Task 18: P1 Final Integration Build and Test

- [ ] **Step 1: Full clean build**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build -c release 2>&1 | tail -10`
Expected: Build succeeds

- [ ] **Step 2: Run all tests**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test 2>&1 | tail -20`
Expected: All tests PASS

- [ ] **Step 3: Verify app bundle builds**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && PACKAGE_ONLY=true bash script/build_and_run.sh 2>&1 | tail -10`
Expected: App bundle created successfully

- [ ] **Step 4: Commit any remaining fixes**

```bash
git add -A
git commit -m "chore: P1 integration verification complete"
```

---

# Batch P2: Home + Discovery

> **Note:** P2 tasks follow the same TDD pattern. Each task includes test → implement → verify → commit. Due to length, the full code for P2-P4 will be in separate plan files to keep each plan manageable.

## Task 19: Add DoubanMovie Model

**Files:**
- Create: `Sources/SeleneNative/Models/DoubanMovie.swift`
- Create: `Tests/SeleneNativeTests/DoubanMovieTests.swift`

- [ ] **Step 1: Write failing tests for DoubanMovie, DoubanMovieDetails, DoubanRecommendItem, DoubanResponse JSON decoding with fallbacks**

Test cases:
- Decode DoubanMovie from JSON with `pic.normal` poster and `rating.value` rate
- Decode DoubanMovie with `card_subtitle` year extraction via regex
- Decode DoubanMovieDetails with 4 poster fallback paths
- Decode DoubanMovieDetails with nested `rating.average` rate fallback
- Decode DoubanMovieDetails with `pubdate` regex year extraction
- Decode DoubanRecommendItem from JSON
- Decode DoubanResponse with items array
- Decode with missing optional fields → defaults

- [ ] **Step 2: Run tests to verify failure**

- [ ] **Step 3: Implement DoubanMovie.swift with all 4 types and custom decoders**

- [ ] **Step 4: Run tests to verify pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add DoubanMovie models with flexible JSON decoding"
```

---

## Task 20: Add BangumiItem Model

**Files:**
- Create: `Sources/SeleneNative/Models/BangumiItem.swift`
- Create: `Tests/SeleneNativeTests/BangumiItemTests.swift`

- [ ] **Step 1: Write failing tests for all 7 Bangumi types**

Test cases:
- Decode BangumiRating from JSON
- Decode BangumiImages, verify bestImageUrl computed property
- Decode BangumiCollection
- Decode BangumiWeekday
- Decode BangumiItem with HTML entity decoding in name/summary
- Decode BangumiDetails with infobox key/value parsing and tags extraction
- Decode BangumiCalendarResponse array

- [ ] **Step 2: Run tests to verify failure**

- [ ] **Step 3: Implement BangumiItem.swift with all 7 types and HTML entity decoder**

- [ ] **Step 4: Run tests to verify pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add Bangumi models with HTML entity decoding"
```

---

## Task 21: Add CacheService

**Files:**
- Create: `Sources/SeleneNative/Services/CacheService.swift`
- Create: `Tests/SeleneNativeTests/CacheServiceTests.swift`

- [ ] **Step 1: Write failing tests**

Test cases:
- Save and load a Codable struct
- Load returns nil for missing key
- Load returns nil for expired entry
- Remove deletes entry
- ClearExpired removes only expired entries
- ClearAll removes everything

- [ ] **Step 2: Run tests to verify failure**

- [ ] **Step 3: Implement CacheService with FileManager-based storage**

Storage: `~/Library/Caches/com.selene.native/CacheService/`
Each entry: `{hashed_key}.json` + `{hashed_key}.meta` (saveTime + maxAge)

- [ ] **Step 4: Run tests to verify pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add CacheService with disk persistence and expiry"
```

---

## Task 22: Add DoubanAPIClient

**Files:**
- Create: `Sources/SeleneNative/Services/DoubanAPIClient.swift`

- [ ] **Step 1: Implement DoubanProviding protocol and DoubanAPIClient**

Protocol methods: `getHotMovies()`, `getHotTVShows()`, `getHotShows()`, `getRecommendations(kind:category:region:)`, `getDetails(doubanId:)`
Base URL: `m.douban.com` with CDN variants
Headers: Chrome UA, Referer
Cache: 6h lists, 3d details via CacheService

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add DoubanAPIClient with caching"
```

---

## Task 23: Add BangumiAPIClient

**Files:**
- Create: `Sources/SeleneNative/Services/BangumiAPIClient.swift`

- [ ] **Step 1: Implement BangumiProviding protocol and BangumiAPIClient**

Protocol methods: `getTodayCalendar()`, `getCalendarByWeekday(_:)`, `getDetails(bangumiId:)`
Base URL: `https://api.bgm.tv`
Headers: `User-Agent: senshinya/selene/1.0.0`
Cache: 1d calendar, 3d details via CacheService

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add BangumiAPIClient with caching"
```

---

## Task 24: Add HomeView

**Files:**
- Create: `Sources/SeleneNative/Views/HomeView.swift`

- [ ] **Step 1: Implement HomeView with sections**

Sections: Continue Watching (from HistoryStore), Hot Movies, Hot TV, Bangumi Calendar, Hot Shows
Each section is a horizontal scroll of VideoCardView
Pull-to-refresh via .refreshable

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add HomeView with content sections"
```

---

## Task 25: Add CategoryView

**Files:**
- Create: `Sources/SeleneNative/Views/CategoryView.swift`

- [ ] **Step 1: Implement CategoryView for Douban-powered category browsing**

Grid of VideoCardView, pull-to-refresh, pagination
Accepts kind parameter: "movie", "tv", "show"

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add CategoryView for Douban category browsing"
```

---

## Task 26: Add PlayerDetailView

**Files:**
- Create: `Sources/SeleneNative/Views/PlayerDetailView.swift`

- [ ] **Step 1: Implement PlayerDetailView with Douban detail panel**

Shows: cover, title, original title, rating stars, genres, directors, actors, summary
Fallback to SearchResult detail when Douban data unavailable

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add PlayerDetailView with Douban detail panel"
```

---

## Task 27: Enhance DetailView with Douban Integration

**Files:**
- Modify: `Sources/SeleneNative/Views/DetailView.swift`

- [ ] **Step 1: Add Douban detail button and info section**

Add a "豆瓣详情" button that opens PlayerDetailView as a sheet
Show rating badge if doubanID is available
Show recommendation cards at bottom

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: enhance DetailView with Douban detail integration"
```

---

## Task 28: Add FavoritesView and HistoryView

**Files:**
- Create: `Sources/SeleneNative/Views/FavoritesView.swift`
- Create: `Sources/SeleneNative/Views/HistoryView.swift`

- [ ] **Step 1: Implement FavoritesView**

Grid of VideoCardView, pull-to-refresh, unfavorite on context menu

- [ ] **Step 2: Implement HistoryView**

Grid of VideoCardView with progress bars, delete/clear actions, pull-to-refresh

- [ ] **Step 3: Build to verify**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add FavoritesView and HistoryView"
```

---

## Task 29: Wire P2 Views into MainView Sidebar

**Files:**
- Modify: `Sources/SeleneNative/Views/MainView.swift`

- [ ] **Step 1: Replace placeholder pages with real views**

Replace "将在 P2 实现" placeholders with HomeView, CategoryView, FavoritesView, HistoryView

- [ ] **Step 2: Build and test**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: wire P2 views into MainView sidebar navigation"
```

---

## Task 30: P2 Final Integration Build and Test

- [ ] **Step 1: Full clean build**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build -c release 2>&1 | tail -10`

- [ ] **Step 2: Run all tests**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test 2>&1 | tail -20`

- [ ] **Step 3: Commit any fixes**

```bash
git commit -m "chore: P2 integration verification complete"
```

---

# Batch P3: Live TV + Local Mode + Player Enhancements

## Task 31: Add LiveModels

**Files:**
- Create: `Sources/SeleneNative/Models/LiveModels.swift`

- [ ] **Step 1: Implement LiveSource, LiveChannel, LiveChannelGroup, EpgProgram, EpgData**

All types from the spec with Codable conformance and computed properties (isLive, progress, timeRange for EpgProgram)

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add Live models (LiveSource, LiveChannel, EpgProgram)"
```

---

## Task 32: Add LiveService

**Files:**
- Create: `Sources/SeleneNative/Services/LiveService.swift`
- Create: `Tests/SeleneNativeTests/LiveServiceTests.swift`

- [ ] **Step 1: Write failing tests for M3U parsing and EPG XML parsing**

- [ ] **Step 2: Implement LiveServiceClient with M3U parser, EPG XML parser, encoding fallback**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add LiveService with M3U and EPG parsing"
```

---

## Task 33: Add LiveStore

**Files:**
- Create: `Sources/SeleneNative/Stores/LiveStore.swift`

- [ ] **Step 1: Implement LiveStore with source/channel/EPG management**

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add LiveStore for live TV state management"
```

---

## Task 34: Add SubscriptionService

**Files:**
- Create: `Sources/SeleneNative/Services/SubscriptionService.swift`
- Create: `Tests/SeleneNativeTests/SubscriptionServiceTests.swift`

- [ ] **Step 1: Write failing test for Base58 decode and JSON parse**

- [ ] **Step 2: Implement Base58 alphabet decode + JSON parse**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add SubscriptionService with Base58 decoding"
```

---

## Task 35: Add LiveScreenView and LivePlayerView

**Files:**
- Create: `Sources/SeleneNative/Views/LiveScreenView.swift`
- Create: `Sources/SeleneNative/Views/LivePlayerView.swift`

- [ ] **Step 1: Implement LiveScreenView — channel grid with source/group filters**

- [ ] **Step 2: Implement LivePlayerView — player with channel sidebar and EPG**

- [ ] **Step 3: Build to verify**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add LiveScreenView and LivePlayerView"
```

---

## Task 36: Add M3U8Service

**Files:**
- Create: `Sources/SeleneNative/Services/M3U8Service.swift`
- Create: `Tests/SeleneNativeTests/M3U8ServiceTests.swift`

- [ ] **Step 1: Write failing tests for resolution mapping and source scoring**

- [ ] **Step 2: Implement M3U8Service with resolution detection, latency/speed measurement, source scoring**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add M3U8Service with speed test and source scoring"
```

---

## Task 37: Add DLNA Discovery and Control

**Files:**
- Create: `Sources/SeleneNative/Services/DLNADiscoveryService.swift`
- Create: `Sources/SeleneNative/Views/DLNAControlView.swift`

- [ ] **Step 1: Implement DLNA device discovery via SSDP and basic control**

- [ ] **Step 2: Implement DLNAControlView with play/pause/seek/volume**

- [ ] **Step 3: Build to verify**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add DLNA discovery service and control view"
```

---

## Task 38: Add PiP Support to PlayerView

**Files:**
- Modify: `Sources/SeleneNative/Views/PlayerView.swift`

- [ ] **Step 1: Add PiP button using AVKit's PiP support**

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add PiP support to PlayerView"
```

---

## Task 39: Add Local Mode to SessionStore and LoginView

**Files:**
- Modify: `Sources/SeleneNative/Models/LoginSession.swift`
- Modify: `Sources/SeleneNative/Stores/SessionStore.swift`
- Modify: `Sources/SeleneNative/Views/LoginView.swift`

- [ ] **Step 1: Add isLocalMode flag to LoginSession**

- [ ] **Step 2: Add local mode session creation to SessionStore**

- [ ] **Step 3: Add hidden local mode entry to LoginView (tap logo 10 times)**

- [ ] **Step 4: Build to verify**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add local mode support with hidden login entry"
```

---

## Task 40: Wire P3 Views into MainView

**Files:**
- Modify: `Sources/SeleneNative/Views/MainView.swift`

- [ ] **Step 1: Replace Live placeholder with LiveScreenView**

- [ ] **Step 2: Build and test**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: wire P3 views into MainView sidebar"
```

---

## Task 41: P3 Final Integration Build and Test

- [ ] **Step 1: Full clean build**

- [ ] **Step 2: Run all tests**

- [ ] **Step 3: Commit any fixes**

```bash
git commit -m "chore: P3 integration verification complete"
```

---

# Batch P4: Experience Polish

## Task 42: Add ThemeStore

**Files:**
- Create: `Sources/SeleneNative/Stores/ThemeStore.swift`
- Create: `Tests/SeleneNativeTests/ThemeStoreTests.swift`

- [ ] **Step 1: Write failing tests for mode persistence and toggle**

- [ ] **Step 2: Implement ThemeStore with system/light/dark modes, UserDefaults persistence**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add ThemeStore with light/dark/system modes"
```

---

## Task 43: Add VersionService

**Files:**
- Create: `Sources/SeleneNative/Services/VersionService.swift`
- Create: `Tests/SeleneNativeTests/VersionServiceTests.swift`

- [ ] **Step 1: Write failing tests for version comparison and update check**

- [ ] **Step 2: Implement VersionService with GitHub releases check, version comparison, dismissal**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add VersionService with GitHub update check"
```

---

## Task 44: Add ContentFilterService

**Files:**
- Create: `Sources/SeleneNative/Services/ContentFilterService.swift`
- Create: `Tests/SeleneNativeTests/ContentFilterServiceTests.swift`

- [ ] **Step 1: Write failing tests for keyword filtering**

- [ ] **Step 2: Implement ContentFilterService with keyword list and filtering logic**

- [ ] **Step 3: Run tests to verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add ContentFilterService for inappropriate content filtering"
```

---

## Task 45: Add SettingsView

**Files:**
- Create: `Sources/SeleneNative/Views/SettingsView.swift`

- [ ] **Step 1: Implement SettingsView with theme toggle, version check, about, logout**

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add SettingsView with theme, updates, and account"
```

---

## Task 46: Add FullscreenImageViewer

**Files:**
- Create: `Sources/SeleneNative/Views/FullscreenImageViewer.swift`

- [ ] **Step 1: Implement full-screen image viewer with zoom and dismiss**

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add FullscreenImageViewer with zoom support"
```

---

## Task 47: Wire Content Filter into SearchStore

**Files:**
- Modify: `Sources/SeleneNative/Stores/SearchStore.swift`

- [ ] **Step 1: Add content filtering step in search results processing**

- [ ] **Step 2: Build to verify**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: integrate ContentFilterService into SearchStore"
```

---

## Task 48: Wire CacheService into API Clients

**Files:**
- Modify: `Sources/SeleneNative/Services/DoubanAPIClient.swift`
- Modify: `Sources/SeleneNative/Services/BangumiAPIClient.swift`

- [ ] **Step 1: Add CacheService read/write to DoubanAPIClient methods**

- [ ] **Step 2: Add CacheService read/write to BangumiAPIClient methods**

- [ ] **Step 3: Build to verify**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: integrate CacheService into Douban and Bangumi API clients"
```

---

## Task 49: Update SeleneNativeApp with Theme and Update Check

**Files:**
- Modify: `Sources/SeleneNative/App/SeleneNativeApp.swift`
- Modify: `Sources/SeleneNative/Views/RootView.swift`

- [ ] **Step 1: Add ThemeStore to app environment and apply preferred color scheme**

- [ ] **Step 2: Add update check on app launch**

- [ ] **Step 3: Build to verify**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add theme environment and update check to app launch"
```

---

## Task 50: Wire P4 Views into MainView

**Files:**
- Modify: `Sources/SeleneNative/Views/MainView.swift`

- [ ] **Step 1: Replace Settings placeholder with SettingsView**

- [ ] **Step 2: Build and test**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: wire P4 views into MainView sidebar"
```

---

## Task 51: P4 Final Integration Build and Test

- [ ] **Step 1: Full clean build**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift build -c release 2>&1 | tail -10`

- [ ] **Step 2: Run all tests**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && swift test 2>&1 | tail -20`

- [ ] **Step 3: Verify app bundle builds**

Run: `cd /Users/xiwei/Documents/Selene/native-macos && PACKAGE_ONLY=true bash script/build_and_run.sh 2>&1 | tail -10`

- [ ] **Step 4: Commit any fixes**

```bash
git commit -m "chore: P4 integration verification complete — feature parity achieved"
```
