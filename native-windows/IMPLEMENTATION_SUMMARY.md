# Native Windows Implementation Summary

## Current Status

The Windows native client is a WinUI 3 / .NET 8 application that targets `net8.0-windows10.0.19041.0` and publishes as a self-contained Windows App SDK build. As of **2026-06-22**, it is at functional parity with the `native-macos` SwiftUI app across all major modules.

## Phase progress

| Phase | Scope | Status |
|------:|-------|--------|
| 0 | Architecture: split `MainWindow.xaml.cs` into `Views/` Pages, add DI (`Microsoft.Extensions.DependencyInjection`), add `CommunityToolkit.Mvvm`, fix `IPlayRecordStore.SaveAsync`, delete dead `wpf-app/` / `test-app/`, add Converters / Helpers / Assets scaffolding | ✅ Done |
| 1 | In-app player (LibVLCSharp), `PlayerViewModel`, `M3U8Service`, `NativeVideoPlayerView`, `PlayerPage`, 10s progress save, loadDetailAndPlay 3-step resume | ✅ Done |
| 2 | SSE streaming search, `AggregatedSearchResult`, `ContentFilterService`, `SearchPage` with split-pane + filter bar + history chips | ✅ Done |
| 3 | `DetailViewModel` + `DetailPage`, Douban metadata, source switcher, episode list | ✅ Done |
| 4 | `LiveViewModel` upgrade (EPG, groups), `M3UParser`, `EpgParser`, `LiveService` interface, `LivePage` with EPG display | ✅ Done |
| 5 | `CategoryViewModel` + `CategoryPage` for movies / tv / shows / anime (per-weekday Bangumi) | ✅ Done |
| 6 | `CacheService`, `VersionService`, `ThemeService`, `SettingsPage` with theme/update/cache/logout | ✅ Done |
| 7 | Cleanup, full build verification | ✅ Done |

## Implemented

### Core library (`SeleneNative.Core`)

**Models (12):** `BangumiItem` (+ sub-types), `DoubanMovie` (+ `DoubanResponse`), `SearchResult`, `SearchResource`, `SearchSuggestion`, `PlayRecord`, `FavoriteItem`, `LoginSession`, `LiveSource`, `LiveChannel`, `EpgProgram`, `EpgData`, `AggregatedSearchResult`, `JsonModelHelpers`, `ModelValueReaders`.

**Services (18):** `ServerApiClient` (`IContentProvider`), `BangumiClient`, `DoubanClient` (including `GetDetailAsync`), `PlayRecordStore` (read+write), `SessionStore`, `SSESearchClient`, `ContentFilterService`, `CacheService` (TTL-based), `VersionService`, `ThemeService`, `LiveService` (`ServerLiveService`), `M3UParser`, `EpgParser`, `M3U8Service`, `IMediaPlayer`, `ApiException`.

**ViewModels (10):** `HomeViewModel`, `LoginViewModel`, `SearchViewModel` (SSE path + aggregation + filtering), `FavoritesViewModel`, `HistoryViewModel`, `LiveViewModel` (EPG + groups), `SettingsViewModel` (theme/cache/update/logout), `PlayerViewModel` (loadDetailAndPlay 3-step resume + progress save), `DetailViewModel`, `CategoryViewModel`.

**Helpers:** `URLNormalizer`, `JsonHelper`, `NetworkHelper`.

### WinUI app (`SeleneNative`)

**Views (12):** `HomePage`, `LoginPage`, `SearchPage` (split-pane + filter bar), `DetailPage` (Douban rating + source switcher + episodes), `PlayerPage` (LibVLCSharp video + controls + time display + error bar), `CategoryPage` (movies/tv/shows/anime), `LivePage` (sources + groups + EPG), `FavoritesPage`, `HistoryPage`, `SettingsPage` (theme/update/cache/logout), `NativeVideoPlayerView` (LibVLCSharp host).

**Infrastructure:** DI container in `App.xaml.cs`, `LibVlcMediaPlayer` service, `UiHelpers`, `Converters/` (BoolToVisibility, StringToImageSource, ProgressToColor).

### Architecture

- All testable logic lives in `SeleneNative.Core` (no WinUI / WindowsAppSDK dependency).
- WinUI 3 host references `Core` with `Microsoft.Extensions.DependencyInjection` + `CommunityToolkit.Mvvm`.
- ViewModels use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm.
- All pages use `UiHelpers.*` static methods for imperative UI construction.
- Navigation: `ContentControl` swapper via `MainWindow.ShowPage()`.

### Tests

- **69 xUnit tests** across `Player/`, `Search/`, `Detail/`, `Live/`, `Services/`, `Home/`, `Models/`, `Helpers/`.
- All passing.

## Build / run

```powershell
cd native-windows
.\build.ps1 -Platform x64          # clean + build + test + publish
.\run.ps1 -Platform x64            # launch
```

## Verification

```powershell
dotnet test tests/SeleneNative.Tests/SeleneNative.Tests.csproj -c Release -p:Platform=x64
# Result: 69 passed, 0 failed, 0 skipped
```
