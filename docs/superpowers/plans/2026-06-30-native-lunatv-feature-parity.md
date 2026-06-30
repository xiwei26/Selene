# Native LunaTV Feature Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add native Windows and native macOS parity for LunaTV short drama, Bilibili, YouTube, TMDB detail enhancements, and extended Douban detail data.

**Architecture:** Both clients remain thin native clients over the configured LunaTV server. Each platform gets focused API clients, tolerant models, state owners, navigation entries, and detail/playback integration using the active server URL and cookie. Existing search, playback, history, favorites, live TV, and local mode behavior must remain intact.

**Tech Stack:** Windows: C#/.NET 8, WinUI, xUnit. macOS: Swift 5.9, SwiftUI, XCTest, SwiftPM. Backend contract source: LunaTV Next.js API routes.

## Global Constraints

- Native clients must prefer LunaTV server APIs when a user session has a server URL.
- Do not reimplement LunaTV server-side scraping, parsing, or anti-bot logic in native clients.
- All new server-backed features require an active server session.
- All authenticated LunaTV server calls must forward the session cookie.
- Model decoding must tolerate missing optional fields and server-side shape drift.
- Existing local mode must remain unaffected.
- New content source entries: Short Drama, Bilibili, YouTube.
- Short drama must support category/recommend/list/search/detail/parse playback.
- Bilibili must support popular and search browsing, and playback only when backend data provides a usable URL.
- YouTube must support region-aware popular browsing and search, and playback only when backend data provides a usable URL.
- Existing detail views must show TMDB and extended Douban sections when data is available.
- After modifying native Windows code, run tests and the Windows packaging flow so `native-windows/publish/win-x64` reflects the latest source.
- Run macOS `swift test`, `swift build -c release`, and `PACKAGE_ONLY=true bash script/build_and_run.sh` where a macOS Swift toolchain is available; if unavailable, report the exact blocker.

---

## File Structure

### Windows Files

- Create `native-windows/src/SeleneNative.Core/Models/ExtendedContentModels.cs`: shared tolerant models for short drama, video platforms, TMDB, and Douban extended sections.
- Create `native-windows/src/SeleneNative.Core/Services/LunaFeatureClientBase.cs`: shared URL/query/cookie helper for new feature clients.
- Create `native-windows/src/SeleneNative.Core/Services/ShortDramaClient.cs`: short drama API methods and parse handoff.
- Create `native-windows/src/SeleneNative.Core/Services/VideoPlatformClient.cs`: Bilibili and YouTube popular/search/regions API methods.
- Create `native-windows/src/SeleneNative.Core/Services/MetadataEnhancementClient.cs`: TMDB and Douban extended API methods.
- Create `native-windows/src/SeleneNative.Core/ViewModels/ShortDramaViewModel.cs`: short drama page state.
- Create `native-windows/src/SeleneNative.Core/ViewModels/VideoPlatformViewModel.cs`: Bilibili/YouTube page state.
- Modify `native-windows/src/SeleneNative.Core/ViewModels/DetailViewModel.cs`: load optional TMDB/Douban enhancement data.
- Create `native-windows/src/SeleneNative/Views/ShortDramaPage.xaml` and `.xaml.cs`: short drama UI.
- Create `native-windows/src/SeleneNative/Views/VideoPlatformPage.xaml` and `.xaml.cs`: reusable Bilibili/YouTube UI.
- Modify `native-windows/src/SeleneNative/Views/DetailPage.xaml.cs`: render enhancement sections.
- Modify `native-windows/src/SeleneNative/MainWindow.xaml.cs`: register navigation and service wiring.
- Modify `native-windows/src/SeleneNative/App.xaml.cs` or DI registration location if needed.
- Add tests under:
  - `native-windows/tests/SeleneNative.Tests/ExtendedContent/LunaFeatureClientTests.cs`
  - `native-windows/tests/SeleneNative.Tests/ExtendedContent/ShortDramaViewModelTests.cs`
  - `native-windows/tests/SeleneNative.Tests/ExtendedContent/VideoPlatformViewModelTests.cs`
  - `native-windows/tests/SeleneNative.Tests/Detail/MetadataEnhancementTests.cs`

### macOS Files

- Create `native-macos/Sources/SeleneNative/Models/ExtendedContentModels.swift`: shared models for short drama, video platforms, TMDB, and Douban extended sections.
- Create `native-macos/Sources/SeleneNative/Services/LunaFeatureRequest.swift`: URL/query/cookie request helper.
- Create `native-macos/Sources/SeleneNative/Services/ShortDramaAPIClient.swift`: short drama API methods and parse handoff.
- Create `native-macos/Sources/SeleneNative/Services/VideoPlatformAPIClient.swift`: Bilibili and YouTube popular/search/regions API methods.
- Create `native-macos/Sources/SeleneNative/Services/MetadataEnhancementAPIClient.swift`: TMDB and Douban extended API methods.
- Create `native-macos/Sources/SeleneNative/Stores/ShortDramaStore.swift`: short drama page state.
- Create `native-macos/Sources/SeleneNative/Stores/VideoPlatformStore.swift`: Bilibili/YouTube page state.
- Create `native-macos/Sources/SeleneNative/Stores/DetailEnhancementStore.swift`: optional metadata state for detail views.
- Create `native-macos/Sources/SeleneNative/Views/ShortDramaView.swift`: short drama UI.
- Create `native-macos/Sources/SeleneNative/Views/VideoPlatformView.swift`: reusable Bilibili/YouTube UI.
- Create `native-macos/Sources/SeleneNative/Views/DetailEnhancementsView.swift`: TMDB/Douban optional sections.
- Modify `native-macos/Sources/SeleneNative/Views/DetailView.swift`: attach enhancement UI.
- Modify `native-macos/Sources/SeleneNative/Views/MainView.swift`: add navigation entries and session-backed services/stores.
- Add tests under:
  - `native-macos/Tests/SeleneNativeTests/LunaFeatureClientTests.swift`
  - `native-macos/Tests/SeleneNativeTests/ShortDramaStoreTests.swift`
  - `native-macos/Tests/SeleneNativeTests/VideoPlatformStoreTests.swift`
  - `native-macos/Tests/SeleneNativeTests/DetailEnhancementStoreTests.swift`

---

## Task 1: Windows Shared Feature Clients and Models

**Files:**
- Create: `native-windows/src/SeleneNative.Core/Models/ExtendedContentModels.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/LunaFeatureClientBase.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/ShortDramaClient.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/VideoPlatformClient.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/MetadataEnhancementClient.cs`
- Test: `native-windows/tests/SeleneNative.Tests/ExtendedContent/LunaFeatureClientTests.cs`

**Interfaces:**
- Produces: `ShortDramaClient`, `VideoPlatformClient`, `MetadataEnhancementClient`.
- Produces: `ShortDramaItem`, `ShortDramaCategory`, `ShortDramaDetail`, `ShortDramaParseResult`, `VideoPlatformItem`, `YouTubeRegion`, `TmdbBackdropResult`, `TmdbActorResult`, `DoubanComment`, `DoubanExtendedBundle`.
- Consumes: active server base URL and cookie strings from the existing login session.

- [ ] **Step 1: Write failing tests for URL construction and cookie forwarding**

Create `native-windows/tests/SeleneNative.Tests/ExtendedContent/LunaFeatureClientTests.cs` with tests that use a fake `HttpMessageHandler` like existing `ServerApiClientTests.cs`. Cover:

```csharp
[Fact]
public async Task ShortDramaClient_SearchAsync_RequestsExpectedPathAndCookie()
{
    using var handler = new RecordingHandler("""{"data":{"list":[],"total":0}}""");
    var client = new ShortDramaClient("http://server.test", "sid=abc", new HttpClient(handler));

    await client.SearchAsync("hero", page: 2, pageSize: 24);

    Assert.Equal("http://server.test/api/shortdrama/search?query=hero&page=2&size=24", handler.Request!.RequestUri!.ToString());
    Assert.Equal("sid=abc", handler.Request.Headers.GetValues("Cookie").Single());
}

[Fact]
public async Task VideoPlatformClient_LoadYouTubePopularAsync_RequestsRegionAndToken()
{
    using var handler = new RecordingHandler("""{"items":[],"nextPageToken":"n2"}""");
    var client = new VideoPlatformClient("http://server.test", "sid=abc", new HttpClient(handler));

    await client.LoadYouTubePopularAsync("JP", "p1");

    Assert.Equal("http://server.test/api/youtube/popular?regionCode=JP&pageToken=p1", handler.Request!.RequestUri!.ToString());
}

[Fact]
public async Task MetadataEnhancementClient_LoadDoubanCommentsAsync_RequestsIdStartLimitSort()
{
    using var handler = new RecordingHandler("""{"code":200,"data":{"comments":[],"start":0,"limit":10,"total":0}}""");
    var client = new MetadataEnhancementClient("http://server.test", "sid=abc", new HttpClient(handler));

    await client.LoadDoubanCommentsAsync("1292052", start: 0, limit: 10, sort: "new_score");

    Assert.Equal("http://server.test/api/douban/comments?id=1292052&start=0&limit=10&sort=new_score", handler.Request!.RequestUri!.ToString());
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter ExtendedContent`

Expected: FAIL because the new clients and models do not exist.

- [ ] **Step 3: Implement models and shared request helper**

Implement `LunaFeatureClientBase` with:

```csharp
protected HttpRequestMessage CreateGetRequest(string path, IReadOnlyList<KeyValuePair<string, string?>> query);
protected Task<T?> GetJsonAsync<T>(string path, IReadOnlyList<KeyValuePair<string, string?>> query, CancellationToken cancellationToken = default);
```

Behavior:

- Normalize base URL with a trailing slash.
- Build query with `Uri.EscapeDataString`.
- Skip null query values but keep empty strings.
- Add `Accept: application/json`.
- Add `Cookie` only when non-empty.
- Use `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true`.

Implement tolerant models using nullable properties and `JsonPropertyName` for snake_case fields.

- [ ] **Step 4: Implement feature clients**

Implement methods:

```csharp
Task<IReadOnlyList<ShortDramaCategory>> LoadShortDramaCategoriesAsync(CancellationToken cancellationToken = default);
Task<ShortDramaListResult> LoadShortDramaRecommendAsync(string? category = null, int size = 24, CancellationToken cancellationToken = default);
Task<ShortDramaListResult> LoadShortDramaListAsync(string categoryId, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default);
Task<ShortDramaListResult> SearchAsync(string query, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default);
Task<ShortDramaDetail?> LoadDetailAsync(string id, string? name = null, CancellationToken cancellationToken = default);
Task<ShortDramaParseResult?> ParseAsync(string id, int episode, string? name = null, CancellationToken cancellationToken = default);
Task<VideoPlatformPage> LoadBilibiliPopularAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
Task<VideoPlatformPage> SearchBilibiliAsync(string query, CancellationToken cancellationToken = default);
Task<VideoPlatformPage> LoadYouTubePopularAsync(string regionCode = "US", string? pageToken = null, CancellationToken cancellationToken = default);
Task<VideoPlatformPage> SearchYouTubeAsync(string query, string contentType = "all", string order = "relevance", int maxResults = 25, CancellationToken cancellationToken = default);
Task<IReadOnlyList<YouTubeRegion>> LoadYouTubeRegionsAsync(CancellationToken cancellationToken = default);
Task<TmdbBackdropResult?> LoadBackdropAsync(string title, string? originalTitle, string? year, string? sourceType, CancellationToken cancellationToken = default);
Task<TmdbActorResult?> LoadActorAsync(string actor, string type = "movie", int limit = 20, CancellationToken cancellationToken = default);
Task<IReadOnlyList<DoubanComment>> LoadDoubanCommentsAsync(string id, int start = 0, int limit = 10, string sort = "new_score", CancellationToken cancellationToken = default);
Task<IReadOnlyList<DoubanMovie>> LoadDoubanRecommendsAsync(string kind, int limit = 20, int start = 0, CancellationToken cancellationToken = default);
Task<DoubanQuickInfo?> LoadDoubanQuickInfoAsync(string id, CancellationToken cancellationToken = default);
Task<IReadOnlyList<DoubanSuggestItem>> SuggestDoubanAsync(string query, CancellationToken cancellationToken = default);
Task<IReadOnlyList<DoubanCelebrityWork>> LoadCelebrityWorksAsync(string name, int limit = 20, string mode = "search", CancellationToken cancellationToken = default);
Task<TrailerRefreshResult?> RefreshTrailerAsync(string id, bool force = false, CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter ExtendedContent`

Expected: PASS for new client tests.

- [ ] **Step 6: Commit**

```powershell
git add native-windows/src/SeleneNative.Core/Models/ExtendedContentModels.cs `
  native-windows/src/SeleneNative.Core/Services/LunaFeatureClientBase.cs `
  native-windows/src/SeleneNative.Core/Services/ShortDramaClient.cs `
  native-windows/src/SeleneNative.Core/Services/VideoPlatformClient.cs `
  native-windows/src/SeleneNative.Core/Services/MetadataEnhancementClient.cs `
  native-windows/tests/SeleneNative.Tests/ExtendedContent/LunaFeatureClientTests.cs
git commit -m "feat: add Windows Luna feature clients"
```

---

## Task 2: macOS Shared Feature Clients and Models

**Files:**
- Create: `native-macos/Sources/SeleneNative/Models/ExtendedContentModels.swift`
- Create: `native-macos/Sources/SeleneNative/Services/LunaFeatureRequest.swift`
- Create: `native-macos/Sources/SeleneNative/Services/ShortDramaAPIClient.swift`
- Create: `native-macos/Sources/SeleneNative/Services/VideoPlatformAPIClient.swift`
- Create: `native-macos/Sources/SeleneNative/Services/MetadataEnhancementAPIClient.swift`
- Test: `native-macos/Tests/SeleneNativeTests/LunaFeatureClientTests.swift`

**Interfaces:**
- Produces Swift equivalents of Task 1 clients and models.
- Consumes `URL serverURL`, `String cookie`, injectable `URLSession`, and optional `CacheService`.

- [ ] **Step 1: Write failing XCTest coverage**

Create `LunaFeatureClientTests.swift` using `URLProtocol` interception like `DoubanAPIClientTests.swift`. Cover:

```swift
func testShortDramaSearchForwardsCookieAndQuery() async throws {
    TestURLProtocol.requestHandler = { request in
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
    TestURLProtocol.requestHandler = { request in
        let components = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)
        XCTAssertEqual(request.url?.path, "/api/youtube/popular")
        XCTAssertEqual(components?.queryValue("regionCode"), "JP")
        XCTAssertEqual(components?.queryValue("pageToken"), "p1")
        return Self.jsonResponse(for: request, body: #"{"items":[],"nextPageToken":"n2"}"#)
    }

    let client = VideoPlatformAPIClient(serverURL: URL(string: "http://server.test")!, cookie: "sid=abc", session: makeSession())
    _ = try await client.loadYouTubePopular(regionCode: "JP", pageToken: "p1")
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `swift test --filter LunaFeatureClientTests`

Expected on macOS: FAIL because the clients do not exist. On the current Windows machine this may fail with `swift` not found; record that exact blocker.

- [ ] **Step 3: Implement Swift request helper**

Implement `LunaFeatureRequest` with:

```swift
struct LunaFeatureRequest: Sendable {
    let serverURL: URL
    let cookie: String
    let session: URLSession

    func getJSON<T: Decodable>(
        path: String,
        queryItems: [URLQueryItem] = [],
        as type: T.Type
    ) async throws -> T
}
```

Behavior:

- Build URLs with `serverURL.appendingPathComponent(path)`.
- Set query items exactly as provided.
- Add `Accept: application/json`.
- Add `Cookie` only when non-empty.
- Throw `APIError.invalidURL`, `APIError.responseError`, or `APIError.parseError`.

- [ ] **Step 4: Implement Swift models and clients**

Implement Codable models mirroring Task 1. Use optional properties for fields that may be absent.

Implement methods equivalent to Task 1 with Swift naming:

```swift
func loadCategories() async throws -> [ShortDramaCategory]
func loadRecommend(category: String?, size: Int) async throws -> ShortDramaListResult
func loadList(categoryId: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult
func search(query: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult
func loadDetail(id: String, name: String?) async throws -> ShortDramaDetail?
func parse(id: String, episode: Int, name: String?) async throws -> ShortDramaParseResult?
func loadBilibiliPopular(page: Int, pageSize: Int) async throws -> VideoPlatformPage
func searchBilibili(query: String) async throws -> VideoPlatformPage
func loadYouTubePopular(regionCode: String, pageToken: String?) async throws -> VideoPlatformPage
func searchYouTube(query: String, contentType: String, order: String, maxResults: Int) async throws -> VideoPlatformPage
func loadYouTubeRegions() async throws -> [YouTubeRegion]
```

- [ ] **Step 5: Run tests to verify GREEN**

Run: `swift test --filter LunaFeatureClientTests`

Expected on macOS: PASS. If unavailable in this environment, record exact blocker.

- [ ] **Step 6: Commit**

```bash
git add native-macos/Sources/SeleneNative/Models/ExtendedContentModels.swift \
  native-macos/Sources/SeleneNative/Services/LunaFeatureRequest.swift \
  native-macos/Sources/SeleneNative/Services/ShortDramaAPIClient.swift \
  native-macos/Sources/SeleneNative/Services/VideoPlatformAPIClient.swift \
  native-macos/Sources/SeleneNative/Services/MetadataEnhancementAPIClient.swift \
  native-macos/Tests/SeleneNativeTests/LunaFeatureClientTests.swift
git commit -m "feat: add macOS Luna feature clients"
```

---

## Task 3: Windows Short Drama UI and Playback

**Files:**
- Create: `native-windows/src/SeleneNative.Core/ViewModels/ShortDramaViewModel.cs`
- Create: `native-windows/src/SeleneNative/Views/ShortDramaPage.xaml`
- Create: `native-windows/src/SeleneNative/Views/ShortDramaPage.xaml.cs`
- Modify: `native-windows/src/SeleneNative/MainWindow.xaml.cs`
- Test: `native-windows/tests/SeleneNative.Tests/ExtendedContent/ShortDramaViewModelTests.cs`

**Interfaces:**
- Consumes `ShortDramaClient` from Task 1.
- Produces page-level events that call existing `PlayerViewModel.ReplaceItem(url, result, index)` or equivalent existing playback path.

- [ ] **Step 1: Write failing ViewModel tests**

Create tests:

```csharp
[Fact]
public async Task LoadInitialAsync_LoadsCategoriesAndRecommendedItems()
{
    var client = new FakeShortDramaClient
    {
        Categories = [new ShortDramaCategory { Id = "1", Name = "都市" }],
        Recommended = new ShortDramaListResult { Items = [new ShortDramaItem { Id = "s1", Name = "短剧一", Cover = "c.jpg" }] }
    };
    var vm = new ShortDramaViewModel(client);

    await vm.LoadInitialAsync();

    Assert.False(vm.IsLoading);
    Assert.Null(vm.ErrorMessage);
    Assert.Single(vm.Categories);
    Assert.Single(vm.Items);
}

[Fact]
public async Task PlayEpisodeAsync_ParsesSelectedEpisodeAndRaisesPlayRequested()
{
    var client = new FakeShortDramaClient
    {
        ParseResult = new ShortDramaParseResult { ParsedUrl = "https://video.example/1.m3u8" }
    };
    var vm = new ShortDramaViewModel(client);
    string? playedUrl = null;
    vm.PlayRequested += url => playedUrl = url;

    await vm.PlayEpisodeAsync(new ShortDramaItem { Id = "s1", Name = "短剧一" }, episode: 1);

    Assert.Equal("https://video.example/1.m3u8", playedUrl);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter ShortDramaViewModel`

Expected: FAIL because ViewModel/page do not exist.

- [ ] **Step 3: Implement ViewModel**

Expose:

```csharp
ObservableCollection<ShortDramaCategory> Categories { get; }
ObservableCollection<ShortDramaItem> Items { get; }
string SearchQuery { get; set; }
ShortDramaCategory? SelectedCategory { get; set; }
bool IsLoading { get; private set; }
string? ErrorMessage { get; private set; }
event Action<string>? PlayRequested;
Task LoadInitialAsync(CancellationToken cancellationToken = default);
Task SearchAsync(CancellationToken cancellationToken = default);
Task LoadCategoryAsync(ShortDramaCategory category, CancellationToken cancellationToken = default);
Task LoadMoreAsync(CancellationToken cancellationToken = default);
Task PlayEpisodeAsync(ShortDramaItem item, int episode, CancellationToken cancellationToken = default);
```

Parsing URL precedence:

1. `ParsedUrl`
2. `ProxyUrl`
3. `Url`

If no URL exists, set `ErrorMessage = "短剧播放地址不可用"` and do not raise `PlayRequested`.

- [ ] **Step 4: Implement page and navigation**

Add a WinUI page with search box, category row, item list, loading ring, error bar, and play button. In `MainWindow.xaml.cs`, add a Short Drama navigation item and build the page only when `Login.Session` has a server URL. If no server session exists, show the existing login-required style placeholder.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter ShortDramaViewModel`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add native-windows/src/SeleneNative.Core/ViewModels/ShortDramaViewModel.cs `
  native-windows/src/SeleneNative/Views/ShortDramaPage.xaml `
  native-windows/src/SeleneNative/Views/ShortDramaPage.xaml.cs `
  native-windows/src/SeleneNative/MainWindow.xaml.cs `
  native-windows/tests/SeleneNative.Tests/ExtendedContent/ShortDramaViewModelTests.cs
git commit -m "feat: add Windows short drama browsing"
```

---

## Task 4: macOS Short Drama UI and Playback

**Files:**
- Create: `native-macos/Sources/SeleneNative/Stores/ShortDramaStore.swift`
- Create: `native-macos/Sources/SeleneNative/Views/ShortDramaView.swift`
- Modify: `native-macos/Sources/SeleneNative/Views/MainView.swift`
- Test: `native-macos/Tests/SeleneNativeTests/ShortDramaStoreTests.swift`

**Interfaces:**
- Consumes `ShortDramaAPIClient` from Task 2.
- Produces `onPlayURL: (URL) -> Void` callback for existing `PlayerStore`.

- [ ] **Step 1: Write failing store tests**

Create tests:

```swift
@MainActor
func testLoadInitialLoadsCategoriesAndRecommendedItems() async {
    let provider = FakeShortDramaProvider(
        categories: [ShortDramaCategory(id: "1", name: "都市")],
        recommended: ShortDramaListResult(items: [ShortDramaItem(id: "s1", name: "短剧一", cover: "c.jpg")], total: 1)
    )
    let store = ShortDramaStore(provider: provider)

    await store.loadInitial()

    XCTAssertFalse(store.isLoading)
    XCTAssertNil(store.errorMessage)
    XCTAssertEqual(store.categories.count, 1)
    XCTAssertEqual(store.items.count, 1)
}

@MainActor
func testPlayEpisodeParsesSelectedEpisode() async {
    let provider = FakeShortDramaProvider(parseResult: ShortDramaParseResult(parsedUrl: "https://video.example/1.m3u8"))
    let store = ShortDramaStore(provider: provider)

    let url = await store.playURL(for: ShortDramaItem(id: "s1", name: "短剧一", cover: ""), episode: 1)

    XCTAssertEqual(url?.absoluteString, "https://video.example/1.m3u8")
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `swift test --filter ShortDramaStoreTests`

Expected on macOS: FAIL because store/view do not exist. Record blocker if `swift` is unavailable.

- [ ] **Step 3: Implement provider protocol and store**

Expose:

```swift
@MainActor
@Observable
final class ShortDramaStore {
    var categories: [ShortDramaCategory] = []
    var items: [ShortDramaItem] = []
    var searchQuery = ""
    var selectedCategory: ShortDramaCategory?
    var isLoading = false
    var errorMessage: String?

    func loadInitial() async
    func search() async
    func load(category: ShortDramaCategory) async
    func loadMore() async
    func playURL(for item: ShortDramaItem, episode: Int) async -> URL?
}
```

URL precedence: `parsedUrl`, `proxyUrl`, `url`. If missing, set `errorMessage = "短剧播放地址不可用"`.

- [ ] **Step 4: Implement SwiftUI view and navigation**

Add sidebar entry `.shortDrama`. `ShortDramaView` shows search, categories, grid/list cards, loading, error, retry, and play. On play, call existing player flow with parsed URL.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `swift test --filter ShortDramaStoreTests`

Expected on macOS: PASS, or record exact toolchain blocker.

- [ ] **Step 6: Commit**

```bash
git add native-macos/Sources/SeleneNative/Stores/ShortDramaStore.swift \
  native-macos/Sources/SeleneNative/Views/ShortDramaView.swift \
  native-macos/Sources/SeleneNative/Views/MainView.swift \
  native-macos/Tests/SeleneNativeTests/ShortDramaStoreTests.swift
git commit -m "feat: add macOS short drama browsing"
```

---

## Task 5: Windows Bilibili and YouTube UI

**Files:**
- Create: `native-windows/src/SeleneNative.Core/ViewModels/VideoPlatformViewModel.cs`
- Create: `native-windows/src/SeleneNative/Views/VideoPlatformPage.xaml`
- Create: `native-windows/src/SeleneNative/Views/VideoPlatformPage.xaml.cs`
- Modify: `native-windows/src/SeleneNative/MainWindow.xaml.cs`
- Test: `native-windows/tests/SeleneNative.Tests/ExtendedContent/VideoPlatformViewModelTests.cs`

**Interfaces:**
- Consumes `VideoPlatformClient` from Task 1.
- Produces two configured pages: Bilibili and YouTube.

- [ ] **Step 1: Write failing ViewModel tests**

Cover:

```csharp
[Fact]
public async Task LoadInitialAsync_LoadsBilibiliPopularForBilibiliMode()
{
    var client = new FakeVideoPlatformClient { BilibiliPopular = new VideoPlatformPage { Items = [new VideoPlatformItem { Id = "BV1", Title = "热门" }] } };
    var vm = new VideoPlatformViewModel(client, VideoPlatformKind.Bilibili);

    await vm.LoadInitialAsync();

    Assert.Single(vm.Items);
    Assert.Equal("热门", vm.Items[0].Title);
}

[Fact]
public async Task LoadInitialAsync_LoadsYoutubeRegionsAndPopularForYoutubeMode()
{
    var client = new FakeVideoPlatformClient
    {
        Regions = [new YouTubeRegion { Code = "US", Name = "United States" }],
        YouTubePopular = new VideoPlatformPage { Items = [new VideoPlatformItem { Id = "yt1", Title = "Trending" }] }
    };
    var vm = new VideoPlatformViewModel(client, VideoPlatformKind.YouTube);

    await vm.LoadInitialAsync();

    Assert.Single(vm.Regions);
    Assert.Single(vm.Items);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter VideoPlatformViewModel`

Expected: FAIL because ViewModel/page do not exist.

- [ ] **Step 3: Implement ViewModel**

Expose:

```csharp
enum VideoPlatformKind { Bilibili, YouTube }
ObservableCollection<VideoPlatformItem> Items { get; }
ObservableCollection<YouTubeRegion> Regions { get; }
string SearchQuery { get; set; }
YouTubeRegion? SelectedRegion { get; set; }
bool IsLoading { get; private set; }
string? ErrorMessage { get; private set; }
string? NextPageToken { get; private set; }
Task LoadInitialAsync(CancellationToken cancellationToken = default);
Task SearchAsync(CancellationToken cancellationToken = default);
Task LoadMoreAsync(CancellationToken cancellationToken = default);
```

Playback:

- Add `TryGetPlayableUrl(VideoPlatformItem item): string?`.
- Return first non-empty `PlayableUrl`, `ProxyUrl`, or `Url` only if it starts with `http://` or `https://`.
- If no usable URL exists, set `ErrorMessage = "当前条目暂无可播放地址"`.

- [ ] **Step 4: Implement page and navigation**

Add Bilibili and YouTube sidebar entries. Reuse `VideoPlatformPage` with kind-specific title and search placeholders. YouTube shows region selector; Bilibili hides it.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter VideoPlatformViewModel`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add native-windows/src/SeleneNative.Core/ViewModels/VideoPlatformViewModel.cs `
  native-windows/src/SeleneNative/Views/VideoPlatformPage.xaml `
  native-windows/src/SeleneNative/Views/VideoPlatformPage.xaml.cs `
  native-windows/src/SeleneNative/MainWindow.xaml.cs `
  native-windows/tests/SeleneNative.Tests/ExtendedContent/VideoPlatformViewModelTests.cs
git commit -m "feat: add Windows video platform browsing"
```

---

## Task 6: macOS Bilibili and YouTube UI

**Files:**
- Create: `native-macos/Sources/SeleneNative/Stores/VideoPlatformStore.swift`
- Create: `native-macos/Sources/SeleneNative/Views/VideoPlatformView.swift`
- Modify: `native-macos/Sources/SeleneNative/Views/MainView.swift`
- Test: `native-macos/Tests/SeleneNativeTests/VideoPlatformStoreTests.swift`

**Interfaces:**
- Consumes `VideoPlatformAPIClient` from Task 2.
- Produces reusable SwiftUI view for Bilibili and YouTube.

- [ ] **Step 1: Write failing store tests**

Cover:

```swift
@MainActor
func testLoadInitialLoadsBilibiliPopular() async {
    let provider = FakeVideoPlatformProvider(bilibiliPopular: VideoPlatformPage(items: [VideoPlatformItem(id: "BV1", title: "热门")]))
    let store = VideoPlatformStore(provider: provider, kind: .bilibili)

    await store.loadInitial()

    XCTAssertEqual(store.items.first?.title, "热门")
}

@MainActor
func testLoadInitialLoadsYouTubeRegionsAndPopular() async {
    let provider = FakeVideoPlatformProvider(
        regions: [YouTubeRegion(code: "US", name: "United States")],
        youtubePopular: VideoPlatformPage(items: [VideoPlatformItem(id: "yt1", title: "Trending")])
    )
    let store = VideoPlatformStore(provider: provider, kind: .youtube)

    await store.loadInitial()

    XCTAssertEqual(store.regions.count, 1)
    XCTAssertEqual(store.items.count, 1)
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `swift test --filter VideoPlatformStoreTests`

Expected on macOS: FAIL because store/view do not exist. Record blocker if `swift` is unavailable.

- [ ] **Step 3: Implement store**

Expose:

```swift
enum VideoPlatformKind { case bilibili, youtube }

@MainActor
@Observable
final class VideoPlatformStore {
    var items: [VideoPlatformItem] = []
    var regions: [YouTubeRegion] = []
    var searchQuery = ""
    var selectedRegion: YouTubeRegion?
    var isLoading = false
    var errorMessage: String?
    var nextPageToken: String?

    func loadInitial() async
    func search() async
    func loadMore() async
    func playableURL(for item: VideoPlatformItem) -> URL?
}
```

- [ ] **Step 4: Implement view and navigation**

Add `.bilibili` and `.youtube` navigation entries. `VideoPlatformView` renders search, optional YouTube region menu, card grid/list, retry, and play unavailable state.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `swift test --filter VideoPlatformStoreTests`

Expected on macOS: PASS, or record exact toolchain blocker.

- [ ] **Step 6: Commit**

```bash
git add native-macos/Sources/SeleneNative/Stores/VideoPlatformStore.swift \
  native-macos/Sources/SeleneNative/Views/VideoPlatformView.swift \
  native-macos/Sources/SeleneNative/Views/MainView.swift \
  native-macos/Tests/SeleneNativeTests/VideoPlatformStoreTests.swift
git commit -m "feat: add macOS video platform browsing"
```

---

## Task 7: Windows TMDB and Douban Detail Enhancements

**Files:**
- Modify: `native-windows/src/SeleneNative.Core/ViewModels/DetailViewModel.cs`
- Modify: `native-windows/src/SeleneNative/Views/DetailPage.xaml.cs`
- Test: `native-windows/tests/SeleneNative.Tests/Detail/MetadataEnhancementTests.cs`

**Interfaces:**
- Consumes `MetadataEnhancementClient` from Task 1.
- Produces optional detail properties:
  - `TmdbBackdrop`
  - `DoubanComments`
  - `DoubanRecommendations`
  - `DoubanQuickInfo`
  - `TrailerRefresh`

- [ ] **Step 1: Write failing tests**

Cover:

```csharp
[Fact]
public async Task LoadAsync_WithDoubanId_LoadsOptionalEnhancementsWithoutBlockingBaseDetail()
{
    var provider = new FakeContentProvider();
    var metadata = new FakeMetadataEnhancementClient
    {
        Backdrop = new TmdbBackdropResult { BackdropUrl = "https://img.example/backdrop.jpg" },
        Comments = [new DoubanComment { Username = "u", Content = "good" }],
        Recommends = [new DoubanMovie { Id = "r1", Title = "Related", Poster = "", Year = "2026" }]
    };
    var vm = new DetailViewModel();
    var seed = new SearchResult { Id = "id1", Source = "src", Title = "Title", Year = "2026", DoubanId = 1292052 };

    await vm.LoadAsync(seed, provider, doubanClient: null, metadataClient: metadata);

    Assert.Equal("https://img.example/backdrop.jpg", vm.TmdbBackdrop?.BackdropUrl);
    Assert.Single(vm.DoubanComments);
    Assert.Single(vm.DoubanRecommendations);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter MetadataEnhancement`

Expected: FAIL because enhanced properties/signature do not exist.

- [ ] **Step 3: Implement ViewModel enhancements**

Add optional metadata loading after base detail load. Catch metadata failures independently and keep base detail visible. Use:

- `title`, `year`, source type to request TMDB backdrop.
- `doubanId` to request Douban comments/recommends/quick-info/trailer.

- [ ] **Step 4: Render enhancements**

Update `DetailPage.xaml.cs` to render:

- Backdrop image if available.
- Quick info text section.
- Comments list.
- Recommendations row.
- Trailer refresh result or action state.

Do not render empty containers.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj --filter MetadataEnhancement`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add native-windows/src/SeleneNative.Core/ViewModels/DetailViewModel.cs `
  native-windows/src/SeleneNative/Views/DetailPage.xaml.cs `
  native-windows/tests/SeleneNative.Tests/Detail/MetadataEnhancementTests.cs
git commit -m "feat: enhance Windows detail metadata"
```

---

## Task 8: macOS TMDB and Douban Detail Enhancements

**Files:**
- Create: `native-macos/Sources/SeleneNative/Stores/DetailEnhancementStore.swift`
- Create: `native-macos/Sources/SeleneNative/Views/DetailEnhancementsView.swift`
- Modify: `native-macos/Sources/SeleneNative/Views/DetailView.swift`
- Test: `native-macos/Tests/SeleneNativeTests/DetailEnhancementStoreTests.swift`

**Interfaces:**
- Consumes `MetadataEnhancementAPIClient` from Task 2.
- Produces optional enhancement state for existing `DetailView`.

- [ ] **Step 1: Write failing store tests**

Cover:

```swift
@MainActor
func testLoadWithDoubanIdLoadsOptionalEnhancements() async {
    let provider = FakeMetadataEnhancementProvider(
        backdrop: TmdbBackdropResult(backdropUrl: "https://img.example/backdrop.jpg"),
        comments: [DoubanComment(username: "u", content: "good")],
        recommendations: [DoubanMovie(id: "r1", title: "Related", poster: "", rate: nil, year: "2026")]
    )
    let store = DetailEnhancementStore(provider: provider)

    await store.load(title: "Title", year: "2026", sourceType: "movie", doubanId: 1292052)

    XCTAssertEqual(store.backdrop?.backdropUrl, "https://img.example/backdrop.jpg")
    XCTAssertEqual(store.comments.count, 1)
    XCTAssertEqual(store.recommendations.count, 1)
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `swift test --filter DetailEnhancementStoreTests`

Expected on macOS: FAIL because store/view do not exist. Record blocker if `swift` is unavailable.

- [ ] **Step 3: Implement store**

Expose:

```swift
@MainActor
@Observable
final class DetailEnhancementStore {
    var backdrop: TmdbBackdropResult?
    var quickInfo: DoubanQuickInfo?
    var comments: [DoubanComment] = []
    var recommendations: [DoubanMovie] = []
    var celebrityWorks: [DoubanCelebrityWork] = []
    var trailer: TrailerRefreshResult?
    var errorMessage: String?
    var isLoading = false

    func load(title: String, year: String, sourceType: String?, doubanId: Int?) async
    func refreshTrailer(doubanId: Int, force: Bool) async
}
```

Each metadata request catches its own failure and leaves other sections intact.

- [ ] **Step 4: Implement detail UI**

Add `DetailEnhancementsView` with optional sections and insert it into `DetailView`. Keep existing detail layout if no enhancement data exists.

- [ ] **Step 5: Run tests to verify GREEN**

Run: `swift test --filter DetailEnhancementStoreTests`

Expected on macOS: PASS, or record exact toolchain blocker.

- [ ] **Step 6: Commit**

```bash
git add native-macos/Sources/SeleneNative/Stores/DetailEnhancementStore.swift \
  native-macos/Sources/SeleneNative/Views/DetailEnhancementsView.swift \
  native-macos/Sources/SeleneNative/Views/DetailView.swift \
  native-macos/Tests/SeleneNativeTests/DetailEnhancementStoreTests.swift
git commit -m "feat: enhance macOS detail metadata"
```

---

## Task 9: Integration Verification and Packaging

**Files:**
- Modify only if integration tests reveal issues.

**Interfaces:**
- Consumes all previous tasks.
- Produces verified test/build/package status.

- [ ] **Step 1: Run Windows full test suite**

Run: `dotnet test .\native-windows\SeleneNative.sln`

Expected: PASS, with no new failures.

- [ ] **Step 2: Run Windows packaging flow**

Run the repository's native Windows packaging command. If `native-windows/build.ps1` exists, run:

```powershell
.\native-windows\build.ps1
```

Expected: `native-windows/publish/win-x64` is refreshed. If the script exposes a more specific publish command, use the README-documented Windows packaging path and record the command.

- [ ] **Step 3: Run macOS tests and build where possible**

Run in `native-macos`:

```bash
swift test
swift build -c release
PACKAGE_ONLY=true bash script/build_and_run.sh
```

Expected on macOS: PASS. On the current Windows environment, record blockers such as `swift` not found or `bash`/WSL unavailable.

- [ ] **Step 4: Run git diff checks**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors. Status may show intended changed files only.

- [ ] **Step 5: Commit any integration fixes**

If fixes were required:

```powershell
git add <fixed-files>
git commit -m "fix: stabilize native LunaTV feature parity"
```

If no fixes were required, do not create an empty commit.
