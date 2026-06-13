# Native macOS Feature Parity Design

## Summary

Align the native macOS SwiftUI app with all 22 features present in the Flutter cross-platform app. The MVP already covers login, basic search, detail, and AVKit playback. This spec covers everything else, implemented in 4 prioritized batches under a unified architecture.

## Goals

- Feature parity with the Flutter app for all user-facing functionality.
- Maintain the existing `Views → Stores → Services` architecture.
- Keep the app pure-native: SwiftUI, AVKit, Foundation — no third-party dependencies.
- Each batch is independently shippable.

## Non-Goals

- Pixel-perfect visual matching with the Flutter app.
- Android/iOS/Windows/Linux/web support.
- Replacing the Flutter app.

---

## Architecture

### Layer Diagram

```
SwiftUI Views
  → @Observable Stores
    → Service Protocols
      → Concrete Implementations
        → URLSession / FileManager / UserDefaults
```

### Existing Files (unchanged or extended)

| File | Change |
|------|--------|
| `App/SeleneNativeApp.swift` | Add ThemeStore, FavoritesStore, HistoryStore to environment |
| `Services/ContentProvider.swift` | Add new method signatures for favorites, history, suggestions, live |
| `Services/ServerAPIClient.swift` | Implement new ContentProvider methods |
| `Stores/SearchStore.swift` | Add SSE search, aggregation, filters, suggestions, history |
| `Stores/PlayerStore.swift` | Add progress saving, multi-source, episode reverse |
| `Stores/SessionStore.swift` | Add local mode support |
| `Views/MainView.swift` | Redesign as sidebar + content layout with navigation |
| `Views/DetailView.swift` | Add Douban detail integration |
| `Views/PlayerView.swift` | Add source switching, progress, PiP |
| `Models/SearchResult.swift` | No change |
| `Models/SearchResource.swift` | No change |
| `Models/LoginSession.swift` | Add isLocalMode flag |
| `Support/URLNormalizer.swift` | No change |

### New Models (7 files)

#### `Models/AggregatedSearchResult.swift`

```swift
struct AggregatedSearchResult: Identifiable {
    var id: String { key }
    let key: String          // title+year+type
    let title: String
    let year: String
    let type: String         // "movie" or "tv"
    let cover: String
    var episodeCounts: [String: Int]   // sourceName → count
    var doubanIds: [String: Int]       // doubanId string → occurrence count
    var sourceNames: [String]
    var originalResults: [SearchResult]
    let addedTimestamp: Int64

    // Computed
    var mostCommonEpisodeCount: Int
    var mostCommonDoubanId: String?

    // Factory
    static func fromSearchResult(_ result: SearchResult) -> AggregatedSearchResult
    mutating func addResult(_ result: SearchResult)
}
```

#### `Models/FavoriteItem.swift`

```swift
struct FavoriteItem: Identifiable, Codable {
    let id: String           // "source+id"
    let source: String
    var title: String
    var sourceName: String
    var year: String
    var cover: String
    var totalEpisodes: Int
    var saveTime: Int64

    // Key format: "source+id" — source and id parsed from key, not stored in JSON body
    static func fromJson(key: String, data: [String: Any]) -> FavoriteItem
    func toJson() -> [String: Any]  // omits id and source
}
```

#### `Models/PlayRecord.swift`

```swift
struct PlayRecord: Identifiable, Codable {
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

    // Computed
    var progressPercentage: Double
    var formattedPlayTime: String
    var formattedTotalTime: String

    static func fromJson(key: String, data: [String: Any]) -> PlayRecord
    func toJson() -> [String: Any]  // omits id and source
}
```

#### `Models/DoubanMovie.swift`

Contains 4 types:

```swift
struct DoubanRecommendItem: Identifiable, Codable {
    var id: String
    var title: String
    var poster: String
    var rate: String?
}

struct DoubanMovieDetails: Codable {
    var id: String
    var title: String
    var poster: String       // 4 fallback sources in decoder
    var rate: String?        // nested rating.average fallback
    var year: String         // regex extract from pubdate
    var summary: String?
    var genres: [String]
    var directors: [String]
    var screenwriters: [String]
    var actors: [String]
    var duration: String?
    var countries: [String]
    var languages: [String]
    var releaseDate: String?
    var originalTitle: String?
    var imdbId: String?
    var totalEpisodes: Int?
    var recommends: [DoubanRecommendItem]
}

struct DoubanMovie: Identifiable, Codable {
    var id: String
    var title: String
    var poster: String       // pic.normal / pic.large
    var rate: String?        // rating.value
    var year: String         // regex from card_subtitle
}

struct DoubanResponse: Codable {
    var items: [DoubanMovie]
}
```

#### `Models/BangumiItem.swift`

Contains 7 types:

```swift
struct BangumiRating: Codable {
    var total: Int
    var count: [String: Int]
    var score: Double
}

struct BangumiImages: Codable {
    var large: String
    var common: String
    var medium: String
    var small: String
    var grid: String
    var bestImageUrl: String { [large, common, medium, small, grid].first { !$0.isEmpty } ?? "" }
}

struct BangumiCollection: Codable {
    var doing: Int
    var onHold: Int
    var dropped: Int
    var wish: Int
    var collect: Int
}

struct BangumiWeekday: Codable {
    var en: String
    var cn: String
    var ja: String
    var id: Int  // 1=Mon ... 7=Sun
}

struct BangumiItem: Identifiable, Codable {
    var id: Int
    var url: String
    var type: Int
    var name: String         // HTML-decoded
    var nameCn: String?      // HTML-decoded
    var summary: String      // HTML-decoded
    var airDate: String
    var airWeekday: Int
    var rating: BangumiRating
    var rank: Int
    var images: BangumiImages
    var collection: BangumiCollection
}

struct BangumiDetails: Codable {
    var id: Int
    var type: Int
    var name: String
    var nameCn: String?
    var summary: String
    var nsfw: Bool
    var locked: Bool
    var date: String?
    var platform: String?
    var images: BangumiImages
    var infobox: [String]
    var eps: Int
    var totalEpisodes: Int
    var rating: BangumiRating
    var collection: BangumiCollection
    var tags: [String]
    var metaTags: [String]
    var series: Bool
}

struct BangumiCalendarResponse: Codable {
    var weekday: BangumiWeekday
    var items: [BangumiItem]
}
```

#### `Models/LiveModels.swift`

Contains 4 types:

```swift
struct LiveSource: Identifiable, Codable {
    var id: String { key }
    var key: String
    var name: String
    var url: String
    var ua: String
    var epg: String
    var from: String
    var disabled: Bool
}

struct LiveChannel: Identifiable, Codable {
    var id: String
    var tvgId: String
    var name: String
    var logo: String
    var group: String
    var url: String
    var isFavorite: Bool
}

struct LiveChannelGroup: Identifiable {
    var id: String { name }
    let name: String
    let channels: [LiveChannel]
}

struct EpgProgram: Identifiable {
    var id: String { "\(channelId)-\(startTime.timeIntervalSince1970)" }
    var channelId: String
    var title: String
    var startTime: Date
    var endTime: Date
    var description: String?

    // Computed
    var isLive: Bool
    var progress: Double
    var timeRange: String
}

struct EpgData: Codable {
    var tvgId: String
    var source: String
    var epgUrl: String
    var programs: [EpgProgram]
}
```

#### `Models/SearchSuggestion.swift`

```swift
struct SearchSuggestion: Codable {
    var text: String
    var type: String
    var score: Double
}
```

### New Services (6 files)

#### `Services/SSESearchClient.swift`

SSE streaming search client.

```swift
@MainActor
class SSESearchClient {
    struct SearchProgress {
        var totalSources: Int
        var completedSources: Int
        var currentSource: String?
        var isComplete: Bool
        var error: String?
        var progressPercentage: Double
    }

    // Async stream of incremental results
    var incrementalResults: AsyncStream<[SearchResult]>
    // Progress updates
    var progress: AsyncStream<SearchProgress>
    // Error stream
    var errors: AsyncStream<String>

    func startSearch(query: String, serverURL: URL, cookie: String) async
    func stopSearch()
}
```

**SSE Protocol:**
- URL: `{baseURL}/api/search/ws?q={query}`
- Headers: `Accept: text/event-stream`, `Cache-Control: no-cache`, `Cookie: {auth}`
- Event types: `start` (totalSources), `sourceResult` (results + sourceName), `sourceError` (error + sourceName), `complete`
- Line-by-line `data: {json}` parsing with UTF-8 buffer for incomplete chunks
- 15-second connection timeout

#### `Services/DoubanAPIClient.swift`

```swift
protocol DoubanProviding: Sendable {
    func getHotMovies() async throws -> [DoubanMovie]
    func getHotTVShows() async throws -> [DoubanMovie]
    func getHotShows() async throws -> [DoubanMovie]
    func getRecommendations(kind: String, category: String?, region: String?) async throws -> [DoubanMovie]
    func getDetails(doubanId: String) async throws -> DoubanMovieDetails
}

class DoubanAPIClient: DoubanProviding {
    // Data source: direct / cdn_tencent / cdn_aliyun / cors_proxy
    // Base URL: m.douban.com (or CDN variant)
    // Headers: Chrome UA, Referer: https://movie.douban.com/
    // Cache: 6h for lists, 3d for details (via CacheService)
}
```

#### `Services/BangumiAPIClient.swift`

```swift
protocol BangumiProviding: Sendable {
    func getTodayCalendar() async throws -> [BangumiItem]
    func getCalendarByWeekday(_ weekday: Int) async throws -> [BangumiItem]
    func getDetails(bangumiId: Int) async throws -> BangumiDetails
}

class BangumiAPIClient: BangumiProviding {
    // Base URL: https://api.bgm.tv
    // Headers: User-Agent: senshinya/selene/1.0.0
    // Cache: 1d for calendar, 3d for details (via CacheService)
}
```

#### `Services/LiveService.swift`

```swift
protocol LiveProviding: Sendable {
    func getLiveSources() async throws -> [LiveSource]
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel]
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData?
}

class LiveServiceClient: LiveProviding {
    // Server mode: /api/live/sources, /api/live/channels?source=, /api/live/epg?tvgId=&source=
    // M3U parsing: #EXTM3U, #EXTINF, tvg-id, tvg-logo, group-title
    // EPG XML parsing: <programme> elements with channel, start, stop, <title>
    // Encoding: UTF-8 → GBK → Latin-1 fallback
    // Cache: 2h for sources/channels/EPG
}
```

#### `Services/SubscriptionService.swift`

```swift
class SubscriptionService {
    struct SubscriptionContent {
        var searchResources: [SearchResource]?
        var liveSources: [LiveSource]?
    }

    static func parseSubscriptionContent(_ content: String) -> SubscriptionContent?
}
```

Base58 decoding: decode content → UTF-8 string → JSON parse → extract `api_site` and `lives`.

#### `Services/CacheService.swift`

```swift
class CacheService {
    static let shared = CacheService()

    func save<T: Codable>(key: String, data: T, maxAge: TimeInterval) throws
    func load<T: Codable>(key: String, maxAge: TimeInterval) -> T?
    func remove(key: String)
    func clearExpired()
    func clearAll()

    // Storage: ~/Library/Caches/com.selene.native/CacheService/
    // Each entry: {key}.json + {key}.meta (contains saveTime and maxAge)
}
```

### Extended ContentProvider Protocol

Add to `Services/ContentProvider.swift`:

```swift
protocol ContentProvider: Sendable {
    // Existing
    func login(username: String, password: String) async throws -> LoginSession
    func search(query: String) async throws -> [SearchResult]
    func detail(source: String, id: String) async throws -> SearchResult?
    func searchResources() async throws -> [SearchResource]

    // New — Favorites
    func getFavorites() async throws -> [FavoriteItem]
    func addFavorite(source: String, id: String, data: [String: Any]) async throws
    func removeFavorite(source: String, id: String) async throws

    // New — Play Records
    func savePlayRecord(_ record: PlayRecord) async throws
    func deletePlayRecord(source: String, id: String) async throws
    func clearPlayRecords() async throws
    func getPlayRecords() async throws -> [PlayRecord]

    // New — Search History
    func getSearchHistory() async throws -> [String]
    func addSearchHistory(query: String) async throws
    func deleteSearchHistory(query: String) async throws
    func clearSearchHistory() async throws

    // New — Search Suggestions
    func searchSuggestions(query: String) async throws -> [SearchSuggestion]

    // New — Live
    func getLiveSources() async throws -> [LiveSource]
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel]
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData?

    // New — SSE Search
    func sseSearchURL(query: String) -> URL?
}
```

### New Stores (4 files)

#### `Stores/FavoritesStore.swift`

```swift
@Observable
class FavoritesStore {
    var favorites: [FavoriteItem] = []
    var isLoading: Bool = false

    func loadFavorites(provider: ContentProvider) async
    func toggleFavorite(source: String, id: String, data: [String: Any], provider: ContentProvider) async
    func isFavorited(source: String, id: String) -> Bool
}
```

#### `Stores/HistoryStore.swift`

```swift
@Observable
class HistoryStore {
    var playRecords: [PlayRecord] = []
    var isLoading: Bool = false

    func loadRecords(provider: ContentProvider) async
    func saveRecord(_ record: PlayRecord, provider: ContentProvider) async
    func deleteRecord(source: String, id: String, provider: ContentProvider) async
    func clearRecords(provider: ContentProvider) async
    func recordFor(source: String, id: String) -> PlayRecord?
    func resumePosition(source: String, id: String) -> (index: Int, playTime: Int)?
}
```

#### `Stores/LiveStore.swift`

```swift
@Observable
class LiveStore {
    var sources: [LiveSource] = []
    var channels: [LiveChannel] = []
    var channelGroups: [LiveChannelGroup] = []
    var currentSource: LiveSource?
    var currentChannel: LiveChannel?
    var currentEPG: EpgData?
    var selectedGroup: String?
    var isLoading: Bool = false

    func loadSources(provider: LiveProviding) async
    func loadChannels(sourceKey: String, provider: LiveProviding) async
    func loadEPG(tvgId: String, sourceKey: String, provider: LiveProviding) async
    func selectChannel(_ channel: LiveChannel)
    func filterByGroup(_ group: String?)
}
```

#### `Stores/ThemeStore.swift`

```swift
@Observable
class ThemeStore {
    enum ThemeMode: String, Codable {
        case system, light, dark
    }

    var mode: ThemeMode {
        didSet { persistMode() }
    }

    var currentAppearance: NSAppearance {
        // Resolves .system to NSApp.effectiveAppearance
    }

    func toggleMode()
    private func persistMode()  // UserDefaults
    private func loadMode()     // UserDefaults, default .system
}
```

### New Views (12 files)

#### `Views/HomeView.swift`

Home tab content: Continue Watching section, Hot Movies, Hot TV, Bangumi Calendar, Hot Shows. Scrollable with sections.

#### `Views/CategoryView.swift`

Category browsing (Movie/TV/Anime/Show) powered by Douban API data. Grid layout with pull-to-refresh.

#### `Views/LiveScreenView.swift`

Live TV channel grid. Source selector, group filter, channel cards with logos.

#### `Views/LivePlayerView.swift`

Live player with channel list sidebar, EPG display, channel switching.

#### `Views/FavoritesView.swift`

Favorites grid with pull-to-refresh, unfavorite action.

#### `Views/HistoryView.swift`

Play records grid with progress bars, delete/clear actions.

#### `Views/PlayerDetailView.swift`

Sliding detail panel showing Douban info: cover, rating, genres, directors, actors, summary.

#### `Views/PlayerEpisodesView.swift`

Episode list panel with reverse toggle, current episode highlight.

#### `Views/PlayerSourcesView.swift`

Source switching panel with speed/quality info, refresh button.

#### `Views/SearchSuggestionOverlay.swift`

Dropdown overlay below search field showing suggestions from API.

#### `Views/VideoCardView.swift`

Reusable video card: poster, title, source name, year, progress bar (for history items).

#### `Views/SettingsView.swift`

Settings: theme toggle, version check, about, logout.

### Modified Views

#### `Views/MainView.swift` — Redesign

Replace current HSplitView with a NavigationSplitView sidebar layout:

```
┌─────────────┬──────────────────────────────────────┐
│  Sidebar     │  Content Area                        │
│              │                                      │
│  🏠 首页     │  (varies by navigation selection)    │
│  🔍 搜索     │                                      │
│  🎬 电影     │                                      │
│  📺 电视剧   │                                      │
│  🎌 动漫     │                                      │
│  🎪 综艺     │                                      │
│  📡 直播     │                                      │
│  ───────     │                                      │
│  ❤️ 收藏     │                                      │
│  🕐 历史     │                                      │
│  ⚙️ 设置     │                                      │
└─────────────┴──────────────────────────────────────┘
```

#### `Views/SearchResultsView.swift` — Enhance

Add: SSE progress bar, aggregation toggle, filter pills (source/year/title), year sort, search history chips, suggestion overlay.

#### `Views/DetailView.swift` — Enhance

Add: Douban detail button, Douban info section (rating, genres, summary), recommendation cards.

#### `Views/PlayerView.swift` — Enhance

Add: source switching bar, episode reverse toggle, progress auto-save (10s interval), resume prompt, favorite toggle, PiP button.

---

## Server API Endpoints (New)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/favorites` | GET | List favorites (keyed map) |
| `/api/favorites` | POST | Add favorite `{key, favorite}` |
| `/api/favorites?key=` | DELETE | Remove favorite |
| `/api/playrecords` | GET | List play records (keyed map) |
| `/api/playrecords` | POST | Save record `{key, record}` |
| `/api/playrecords?key=` | DELETE | Delete single record |
| `/api/playrecords` | DELETE | Clear all records |
| `/api/searchhistory` | GET | List search history strings |
| `/api/searchhistory` | POST | Add `{keyword}` |
| `/api/searchhistory?keyword=` | DELETE | Delete single history item |
| `/api/searchhistory` | DELETE | Clear all history |
| `/api/search/suggestions?q=` | GET | Search suggestions `{suggestions: [...]}` |
| `/api/search/ws?q=` | GET (SSE) | SSE streaming search |
| `/api/live/sources` | GET | Live sources list |
| `/api/live/channels?source=` | GET | Channel list for source |
| `/api/live/epg?tvgId=&source=` | GET | EPG data |

---

## Implementation Batches

### P1: Core Search + User Data (~20 new/modified files)

**Goal:** Search experience matches Flutter; user data (favorites, history, records) works end-to-end.

| Task | Files |
|------|-------|
| Add new models | `AggregatedSearchResult.swift`, `FavoriteItem.swift`, `PlayRecord.swift`, `SearchSuggestion.swift` |
| SSE search client | `SSESearchClient.swift` |
| Extend ContentProvider + ServerAPIClient | Add 15 new methods |
| Enhance SearchStore | SSE search, aggregation, filters, suggestions, history |
| Add FavoritesStore | `FavoritesStore.swift` |
| Add HistoryStore | `HistoryStore.swift` |
| Enhance SearchResultsView | Progress bar, aggregation toggle, filters, history chips |
| Add SearchSuggestionOverlay | `SearchSuggestionOverlay.swift` |
| Add VideoCardView | `VideoCardView.swift` |
| Enhance PlayerView | Source switching, progress save, episode reverse |
| Add PlayerSourcesView | `PlayerSourcesView.swift` |
| Add PlayerEpisodesView | `PlayerEpisodesView.swift` |
| Update MainView | NavigationSplitView sidebar |
| Update SeleneNativeApp | Inject new stores |

### P2: Home + Discovery (~12 new/modified files)

**Goal:** Home page with content sections; category browsing; Douban and Bangumi integration.

| Task | Files |
|------|-------|
| Add Douban models | `DoubanMovie.swift` |
| Add Bangumi models | `BangumiItem.swift` |
| Add DoubanAPIClient | `DoubanAPIClient.swift` |
| Add BangumiAPIClient | `BangumiAPIClient.swift` |
| Add CacheService | `CacheService.swift` |
| Add HomeView | `HomeView.swift` with sections |
| Add CategoryView | `CategoryView.swift` |
| Add PlayerDetailView | `PlayerDetailView.swift` |
| Enhance DetailView | Douban info integration |
| Add FavoritesView | `FavoritesView.swift` |
| Add HistoryView | `HistoryView.swift` |

### P3: Live TV + Local Mode + Player Enhancements (~12 new/modified files)

**Goal:** Live TV works; local subscription mode; M3U8 speed test; DLNA casting; PiP.

| Task | Files |
|------|-------|
| Add Live models | `LiveModels.swift` |
| Add LiveService | `LiveService.swift` |
| Add LiveStore | `LiveStore.swift` |
| Add SubscriptionService | `SubscriptionService.swift` |
| Add LiveScreenView | `LiveScreenView.swift` |
| Add LivePlayerView | `LivePlayerView.swift` |
| M3U8 speed test | Add to `PlayerStore` or new `M3U8Service.swift` |
| DLNA casting | `DLNADiscoveryService.swift` + `DLNAControlView.swift` |
| PiP support | Enhance `PlayerView` with AVKit PiP |
| Enhance SessionStore | Local mode flag, subscription parsing |
| Enhance LoginView | Hidden local mode entry (tap logo 10x) |

### P4: Experience Polish (~8 new/modified files)

**Goal:** Theme, updates, content filtering, image viewer, caching.

| Task | Files |
|------|-------|
| Add ThemeStore | `ThemeStore.swift` |
| Add SettingsView | `SettingsView.swift` |
| Version update check | `VersionService.swift` |
| Content filter | `ContentFilterService.swift` |
| Fullscreen image viewer | `FullscreenImageViewer.swift` |
| Enhance SearchStore | Content filtering |
| Cache integration | Wire CacheService into all API clients |
| Update SeleneNativeApp | Theme environment, update check on launch |

---

## Persistence

| Data | Storage | Key/Path |
|------|---------|----------|
| Session (URL, username, cookie) | UserDefaults | `selene_session_data` |
| Theme mode | UserDefaults | `selene_theme_mode` |
| Douban/Bangumi cache | FileManager (Caches) | `~/Library/Caches/com.selene.native/CacheService/` |
| Search cache | FileManager (Caches) | Same |
| Dismissed update version | UserDefaults | `selene_dismissed_version` |
| Last version check time | UserDefaults | `selene_last_version_check` |

---

## Error Handling

All new features follow the existing `APIError` pattern. Additional cases to add:

- `.networkTimeout` — request exceeded timeout
- `.sseConnectionFailed` — SSE connection dropped
- `.parseError` — JSON/M3U/XML decode failure
- `.noResults` — search returned empty (informational, not a crash)

401 handling: All new API methods must check for 401 and trigger logout via SessionStore, consistent with existing behavior.

---

## Testing

### Unit Tests (per batch)

**P1:**
- SSESearchClient: event parsing, progress calculation
- AggregatedSearchResult: grouping logic, mostCommonDoubanId
- FavoriteItem/PlayRecord: JSON round-trip with key format
- FavoritesStore/HistoryStore: state transitions
- SearchStore: filter/sort logic

**P2:**
- DoubanMovie/DoubanMovieDetails: JSON decode with fallbacks
- BangumiItem: JSON decode, HTML entity decoding
- CacheService: save/load/expiry/clear

**P3:**
- LiveService: M3U parsing, EPG XML parsing
- SubscriptionService: Base58 decode
- M3U8Service: resolution mapping, source scoring

**P4:**
- ThemeStore: mode persistence
- VersionService: version comparison
- ContentFilterService: keyword matching

### Manual Verification

Each batch should be manually verified against a real Selene server before moving to the next batch.

---

## Future Work (Post-Parity)

- Keychain-backed credential storage for auto-login
- mpv/libmpv integration if AVKit compatibility is insufficient
- Sparkle framework for native auto-update
- Keyboard shortcuts for common actions
- Touch Bar support
