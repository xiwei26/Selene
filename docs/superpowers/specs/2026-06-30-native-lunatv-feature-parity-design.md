# Native LunaTV Feature Parity Design

## Goal

Bring the native Windows and native macOS clients closer to the current LunaTV backend feature surface for:

- Short drama browsing, search, detail, parsing, and playback.
- Bilibili browsing/search and playable result handling.
- YouTube browsing/search and playable result handling.
- TMDB visual/detail enhancements.
- Extended Douban information beyond categories/details.

The target is full native feature parity with the LunaTV web experience where the backend already exposes stable APIs, implemented in testable waves across both native clients.

## Scope

### In Scope

- Add native sidebar/navigation entries for Short Drama, Bilibili, and YouTube.
- Add service clients for LunaTV backend APIs:
  - `/api/shortdrama/categories`
  - `/api/shortdrama/recommend`
  - `/api/shortdrama/list`
  - `/api/shortdrama/search`
  - `/api/shortdrama/detail`
  - `/api/shortdrama/parse`
  - `/api/bilibili/popular`
  - `/api/bilibili/search`
  - `/api/youtube/popular`
  - `/api/youtube/search`
  - `/api/youtube/regions`
  - `/api/tmdb/backdrop`
  - `/api/tmdb/actor`
  - `/api/douban/comments`
  - `/api/douban/recommends`
  - `/api/douban/quick-info`
  - `/api/douban/suggest`
  - `/api/douban/celebrity-works`
  - `/api/douban/refresh-trailer`
- Use the active user session server URL and cookie for all LunaTV server calls.
- Integrate short drama parsed URLs with the existing native playback flows.
- Integrate Bilibili and YouTube results with existing playback only when the backend returns a playable or proxy URL.
- Add TMDB backdrop/logo/actor and Douban comments/recommendations/trailer data to native detail surfaces.
- Preserve existing search, favorites, history, live TV, and local mode behavior.
- Add focused tests for URL construction, query parameters, cookie forwarding, decoding, state transitions, and view model/store behavior.
- Rebuild/repackage the affected native app after native code changes when the platform toolchain is available.

### Out of Scope

- Reimplementing LunaTV server-side scraping, parsing, or anti-bot logic in native clients.
- Admin-only LunaTV configuration screens.
- TVBox management and diagnostics.
- Watch-room synchronized playback.
- Browser extension warnings and web-only DOM safeguards.
- Full offline video download management.

## Delivery Waves

### Wave 1: Content Source Entrypoints

Add first-class native browsing surfaces for:

- Short Drama
- Bilibili
- YouTube

Each surface supports:

- Loading state.
- Empty state.
- Error state with retry.
- Search.
- Pagination or load-more when the backend response supports it.
- Card/list display using native UI conventions.

Windows implementation:

- Add service clients under `native-windows/src/SeleneNative.Core/Services`.
- Add models under `native-windows/src/SeleneNative.Core/Models`.
- Add ViewModels under `native-windows/src/SeleneNative.Core/ViewModels`.
- Add WinUI pages under `native-windows/src/SeleneNative/Views`.
- Add navigation entries in `MainWindow.xaml.cs`.

macOS implementation:

- Add service clients under `native-macos/Sources/SeleneNative/Services`.
- Add models under `native-macos/Sources/SeleneNative/Models`.
- Add stores under `native-macos/Sources/SeleneNative/Stores`.
- Add SwiftUI views under `native-macos/Sources/SeleneNative/Views`.
- Add navigation entries in `MainView.swift`.

### Wave 2: Detail Enhancement

Enhance existing native detail views with LunaTV metadata:

- TMDB backdrop/logo for visual detail header enhancement.
- TMDB actor lookup when applicable.
- Douban quick info.
- Douban recommendations.
- Douban comments.
- Douban celebrity works.
- Douban trailer refresh.

Detail pages should degrade gracefully:

- If TMDB data is missing, retain the current poster-based layout.
- If Douban extended data fails, show the basic detail result and omit the failed section.
- If trailer refresh fails, keep the previous trailer state and expose a non-blocking error.

### Wave 3: Playback and Parsing

Short drama playback:

- Use `/api/shortdrama/parse` for selected episodes.
- Feed returned playable/proxy URL into the existing native player.
- Save history with enough metadata to resume playback.

Bilibili and YouTube playback:

- If backend search/popular/detail data includes a playable or proxy URL, use existing player.
- If only external metadata or webpage URLs are returned, present a clear unavailable state instead of attempting client-side scraping.

Shared playback requirements:

- Preserve current player behavior for existing search/detail flows.
- Reuse existing history/favorites models when possible.
- Add adapter fields only when the backend content type cannot fit existing `SearchResult`/`PlayRecord` shapes.

### Wave 4: Advanced Parity Polish

Add higher-fidelity behavior after core flows work:

- Source-specific filters.
- YouTube region selection.
- Bilibili popular/search mode switching.
- Short drama category selection and subcategory fallback.
- Resolution labels where backend data exposes them.
- TMDB/Douban image proxy handling.
- Stable cache keys and cache scoping by server URL.
- More complete empty and partial-failure states.

## Architecture

### Shared Principles

- Native clients are thin clients over LunaTV APIs.
- No direct third-party scraping is added to the native apps.
- All server calls use `serverURL` from the active `LoginSession`.
- Authenticated requests forward the session cookie.
- Model decoding should tolerate missing optional fields and server-side shape drift.
- Existing local mode remains unaffected. New server-backed features require a server session.

### Windows Architecture

Use the current pattern:

- `*Client` service for HTTP calls.
- Codable-style C# model classes with `JsonPropertyName`.
- ViewModel owns loading/error/list/search state.
- WinUI page binds to ViewModel and delegates playback/navigation upward.

New services:

- `ShortDramaClient`
- `BilibiliClient`
- `YouTubeClient`
- `TMDBClient`
- `DoubanExtendedClient`

### macOS Architecture

Use the current pattern:

- `*Client` service for HTTP calls.
- `Codable` model structs with tolerant decoding where needed.
- `@Observable` store owns loading/error/list/search state.
- SwiftUI views render state and delegate playback/navigation upward.

New services:

- `ShortDramaAPIClient`
- `BilibiliAPIClient`
- `YouTubeAPIClient`
- `TMDBAPIClient`
- `DoubanExtendedAPIClient`

## Data Flow

1. User logs in or resumes a saved server session.
2. Navigation creates feature services using the active server URL and cookie.
3. Feature page loads initial data:
   - Short Drama: categories and recommended/list data.
   - Bilibili: popular data.
   - YouTube: regions and popular data.
4. User searches or filters.
5. ViewModel/store requests backend data, updates loading/error/items.
6. User opens a detail item.
7. Detail surface loads base detail plus TMDB/Douban enhancement sections.
8. User selects an episode or playable item.
9. Client requests parse/play data if needed.
10. Existing player receives the playable URL.
11. History/favorites save through existing server-backed providers when possible.

## UI Behavior

### Navigation

Add sidebar entries:

- Short Drama
- Bilibili
- YouTube

Keep the existing content sections grouped clearly. The new entries should not replace movie, TV, anime, show, live, favorites, or history.

### Cards and Lists

Each content source should show:

- Poster/thumbnail.
- Title.
- Year/date when available.
- Source label.
- Rating or view count when available.
- Episode/count metadata when available.

### Detail Enhancements

Existing detail pages gain optional sections:

- Hero/backdrop area with TMDB visual data.
- Cast/actor row when available.
- Douban quick info.
- Douban comments.
- Related recommendations.
- Celebrity works.
- Trailer action when backend can refresh trailer data.

Sections should disappear when no data is available rather than showing empty containers.

## Error Handling

- Missing server session: show a sign-in/server-required message.
- HTTP 401/403: ask user to re-login or check server permissions.
- HTTP 404: show source-specific not found state.
- Parse/play URL unavailable: show a playable-unavailable state.
- Partial metadata failure: keep base content visible and omit the failed enhancement.
- Network timeout: show retry.
- Decode failure: show a concise error and log enough context in tests or debug output.

## Testing

### Windows

Add unit tests under `native-windows/tests/SeleneNative.Tests` for:

- Service URL construction and query parameters.
- Cookie forwarding.
- Response decoding for each new endpoint family.
- ViewModel loading/search/error state.
- Short drama parse-to-playback handoff.
- Detail enhancement composition with missing optional data.

Run:

- `dotnet test .\native-windows\SeleneNative.sln`
- Windows packaging flow after tests pass so `native-windows/publish/win-x64` reflects the latest source.

### macOS

Add XCTest coverage under `native-macos/Tests/SeleneNativeTests` for:

- Service URL construction and query parameters.
- Cookie forwarding.
- Response decoding for each new endpoint family.
- Store loading/search/error state.
- Short drama parse-to-playback handoff.
- Detail enhancement composition with missing optional data.

Run when a macOS Swift toolchain is available:

- `swift test`
- `swift build -c release`
- `PACKAGE_ONLY=true bash script/build_and_run.sh`

Current Windows-only environments may not have `swift` or `bash`; in that case, record the blocker and do not claim macOS build success.

## Parallel Development Plan

Use subagents during implementation after the plan is written:

- macOS subagent:
  - Swift models.
  - Swift service clients.
  - Stores.
  - SwiftUI views.
  - XCTest coverage.

- Windows subagent:
  - C# models.
  - HTTP clients.
  - ViewModels.
  - WinUI pages.
  - xUnit coverage.
  - Windows packaging.

Main agent responsibilities:

- Keep API contracts aligned with LunaTV.
- Resolve shared design decisions.
- Review both subagent diffs.
- Run available tests.
- Ensure final user-facing report distinguishes verified results from toolchain blockers.

## Acceptance Criteria

- Native Windows and native macOS both expose Short Drama, Bilibili, and YouTube navigation entries.
- Short Drama supports category/recommend/list/search/detail/parse playback.
- Bilibili supports popular and search browsing, and playback when backend data provides a usable URL.
- YouTube supports region-aware popular browsing and search, and playback when backend data provides a usable URL.
- Existing detail views show TMDB and extended Douban sections when data is available.
- Existing playback, search, favorites, history, live TV, and local mode are not regressed.
- Tests cover the new service clients and state owners.
- Windows tests pass and Windows package output is refreshed.
- macOS tests/build/package are run where the Swift/macOS toolchain is available, or the environment blocker is explicitly reported.
