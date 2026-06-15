# Continue Watching Click-to-Play Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clicking "continue watching" / history / favorites items in the native macOS app navigates to a full PlayerScreen in the detail area and resumes playback from the saved progress.

**Architecture:** Extract the player from `SearchResultsView` into a standalone `PlayerScreen` that covers the `NavigationSplitView` detail area. `MainView` orchestrates the flow: receives a `PlayRecord` tap, calls `provider.detail(source:id:)` to fetch episode URLs, configures `PlayerStore` with resume-seek, and conditionally shows `PlayerScreen` over the current content. `HomeView`, `HistoryView`, and `FavoritesView` each get a simple `onPlayRecord` callback.

**Tech Stack:** SwiftUI (`NavigationSplitView`, `@Observable`), AVKit, `@MainActor`

---

### Task 1: Add `pendingSeekTime` to PlayerStore

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Stores/PlayerStore.swift`

- [ ] **Step 1: Add pendingSeekTime property**

After line 15 (`var totalTime: Int = 0`), add:
```swift
var pendingSeekTime: Int?
```

- [ ] **Step 2: Apply seek in the .readyToPlay observer**

In `observe(player:item:)` at approximately line 120, inside the `.readyToPlay` branch, add:
```swift
if let seekTime = self?.pendingSeekTime {
    let target = CMTime(seconds: Double(seekTime), preferredTimescale: 600)
    self?.player?.seek(to: target, toleranceBefore: .zero, toleranceAfter: .zero)
    self?.pendingSeekTime = nil
}
```

The exact insertion point — replace the existing `totalTime` line inside `if item.status == .readyToPlay`:

```swift
} else if item.status == .readyToPlay {
    self?.totalTime = Self.seconds(from: item.duration)
    if let seekTime = self?.pendingSeekTime {                         // ← add
        let target = CMTime(seconds: Double(seekTime), preferredTimescale: 600)
        self?.player?.seek(to: target, toleranceBefore: .zero, toleranceAfter: .zero)
        self?.pendingSeekTime = nil
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add native-macos/Sources/SeleneNative/Stores/PlayerStore.swift
git commit -m "feat: add pendingSeekTime for resume playback"
```

---

### Task 2: Add `loadDetailAndPlay(record:)` to PlayerStore

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Stores/PlayerStore.swift`

- [ ] **Step 1: Add method to PlayerStore**

Add this method after `toggleEpisodeOrder()` (around line 81):

```swift
@MainActor
func loadDetailAndPlay(record: PlayRecord, provider: ContentProvider) async {
    do {
        guard let result = try await provider.detail(source: record.source, id: record.id) else {
            playbackError = "未找到该视频详情"
            return
        }
        currentSourceResults = [result]
        currentResult = result
        currentEpisodeIndex = record.index

        guard result.episodes.indices.contains(record.index),
              let url = URL(string: result.episodes[record.index]) else {
            playbackError = "剧集链接不可用"
            return
        }

        replaceItem(url: url, result: result, index: record.index)
        if record.playTime > 0 {
            pendingSeekTime = record.playTime
        }
        play()
    } catch {
        playbackError = "获取视频详情失败: \(error.localizedDescription)"
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add native-macos/Sources/SeleneNative/Stores/PlayerStore.swift
git commit -m "feat: add loadDetailAndPlay method for PlayRecord playback"
```

---

### Task 3: Create PlayerScreen standalone view

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/PlayerScreen.swift`

- [ ] **Step 1: Write PlayerScreen**

```swift
import SwiftUI

struct PlayerScreen: View {
    @Bindable var playerStore: PlayerStore
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            // Close button bar
            HStack {
                Button {
                    onClose()
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "chevron.left")
                        Text("返回")
                    }
                }
                .buttonStyle(.borderless)
                .padding(.leading, 16)
                .padding(.vertical, 10)

                Spacer()

                if let title = playerStore.currentResult?.title {
                    Text(title)
                        .font(.headline)
                        .lineLimit(1)
                }

                Spacer()
                // Balance the trailing space so the title stays centered-ish
                HStack(spacing: 6) { } .frame(width: 60)
            }

            Divider()

            PlayerView(playerStore: playerStore)
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add native-macos/Sources/SeleneNative/Views/PlayerScreen.swift
git commit -m "feat: create standalone PlayerScreen view"
```

---

### Task 4: Wire MainView as playback orchestrator

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Views/MainView.swift`

- [ ] **Step 1: Add isPlaying state**

After line 50 (`@State private var liveProvider = LiveServiceClient()`), add:
```swift
@State private var isPlaying = false
```

- [ ] **Step 2: Add playRecord method**

Add after the `init()` block (before `var body`):

```swift
@MainActor
private func playRecord(_ record: PlayRecord) {
    guard let session = sessionStore.session else { return }
    playerStore.currentSourceResults = []
    playerStore.stop()
    Task {
        await playerStore.loadDetailAndPlay(record: record, provider: provider)
        if playerStore.playbackError == nil {
            isPlaying = true
        }
    }
}
```

- [ ] **Step 3: Update contentView to show PlayerScreen when playing**

Replace the existing `contentView` computed property (lines 109-148) with:

```swift
@ViewBuilder
private var contentView: some View {
    if isPlaying {
        PlayerScreen(
            playerStore: playerStore,
            onClose: { isPlaying = false }
        )
    } else {
        switch selection ?? .search {
        case .home:
            HomeView(
                historyStore: historyStore,
                doubanProvider: doubanProvider,
                bangumiProvider: bangumiProvider,
                onPlayRecord: playRecord
            )
        case .search:
            SearchResultsView(
                searchStore: searchStore,
                playerStore: playerStore,
                provider: provider,
                favoritesStore: favoritesStore,
                historyStore: historyStore,
                session: sessionStore.session
            )
        case .movie:
            CategoryView(category: .movie, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
        case .tv:
            CategoryView(category: .tv, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
        case .anime:
            CategoryView(category: .anime, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
        case .show:
            CategoryView(category: .show, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
        case .live:
            LiveScreenView(liveStore: liveStore, provider: liveProvider)
        case .favorites:
            FavoritesView(
                favoritesStore: favoritesStore,
                provider: provider,
                onPlayRecord: playRecord
            )
        case .history:
            HistoryView(
                historyStore: historyStore,
                provider: provider,
                onPlayRecord: playRecord
            )
        case .settings:
            SettingsView(
                sessionStore: sessionStore,
                themeStore: themeStore,
                versionService: VersionService()
            )
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add native-macos/Sources/SeleneNative/Views/MainView.swift
git commit -m "feat: wire MainView as playback orchestrator with PlayerScreen"
```

---

### Task 5: Add onPlayRecord callback to HomeView

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Views/HomeView.swift`

- [ ] **Step 1: Add callback property and onTapGesture**

```swift
struct HomeView: View {
    let historyStore: HistoryStore
    let doubanProvider: DoubanProviding
    let bangumiProvider: BangumiProviding
    let onPlayRecord: ((PlayRecord) -> Void)?          // ← add this line (after bangumiProvider)

    // ... existing properties ...
```

Then in `continueWatchingSection` (around line 59, after the VideoCardView), add `.onTapGesture`:

```swift
VideoCardView(
    title: record.title,
    poster: record.cover,
    sourceName: record.sourceName,
    year: record.year,
    subtitle: "第\(record.index + 1)集",
    progress: record.progressPercentage
)
.frame(width: 260)
.onTapGesture { onPlayRecord?(record) }                // ← add this
```

- [ ] **Step 2: Commit**

```bash
git add native-macos/Sources/SeleneNative/Views/HomeView.swift
git commit -m "feat: add onPlayRecord callback to HomeView continue watching"
```

---

### Task 6: Add onPlayRecord callback to HistoryView

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Views/HistoryView.swift`

- [ ] **Step 1: Add callback property and onTapGesture**

```swift
struct HistoryView: View {
    let historyStore: HistoryStore
    let provider: ContentProvider
    let onPlayRecord: ((PlayRecord) -> Void)?          // ← add this line

    // ...
```

Then add `.onTapGesture` to the VideoCardView inside the List (around line 20):

```swift
List(historyStore.playRecords) { record in
    VideoCardView(
        title: record.title,
        poster: record.cover,
        sourceName: record.sourceName,
        year: record.year,
        subtitle: "第\(record.index + 1)集 \(record.formattedPlayTime) / \(record.formattedTotalTime)",
        progress: record.progressPercentage
    )
    .onTapGesture { onPlayRecord?(record) }            // ← add this
}
```

- [ ] **Step 2: Commit**

```bash
git add native-macos/Sources/SeleneNative/Views/HistoryView.swift
git commit -m "feat: add onPlayRecord callback to HistoryView"
```

---

### Task 7: Add onPlayRecord callback to FavoritesView

**Files:**
- Modify: `native-macos/Sources/SeleneNative/Views/FavoritesView.swift`

- [ ] **Step 1: Add callback property and onTapGesture**

```swift
struct FavoritesView: View {
    let favoritesStore: FavoritesStore
    let provider: ContentProvider
    let onPlayRecord: ((PlayRecord) -> Void)?          // ← add this line

    // ...
```

Then add `.onTapGesture` to the VideoCardView inside the List (around line 18):

```swift
List(favoritesStore.favorites) { item in
    VideoCardView(
        title: item.title,
        poster: item.cover,
        sourceName: item.sourceName,
        year: item.year,
        subtitle: "共\(item.totalEpisodes)集"
    )
    .onTapGesture {                                    // ← add this
        let record = PlayRecord(
            id: item.id,
            source: item.source,
            title: item.title,
            sourceName: item.sourceName,
            year: item.year,
            cover: item.cover,
            index: 0,
            totalEpisodes: item.totalEpisodes,
            playTime: 0,
            totalTime: 0,
            saveTime: item.saveTime,
            searchTitle: item.title
        )
        onPlayRecord?(record)
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add native-macos/Sources/SeleneNative/Views/FavoritesView.swift
git commit -m "feat: add onPlayRecord callback to FavoritesView"
```

---

### Task 8: Build and verify

- [ ] **Step 1: Build the project**

```bash
cd /Users/xiwei/Documents/Selene/native-macos
xcodebuild -scheme SeleneNative -destination "platform=macOS" build
```

Expected: Build succeeds with no errors.

- [ ] **Step 2: Quick visual verification**

Run the app and check:
1. Navigate to Home → "继续观看" section → click a card → PlayerScreen appears with video loaded
2. Video should seek to the saved `playTime` position
3. Click "返回" → goes back to Home
4. Navigate to History → click a card → same behavior
5. Navigate to Favorites → click a card → same behavior
6. Search → select result → click an episode → still works via inline PlayerView

- [ ] **Step 3: Final commit if any build fixes were needed**

```bash
git commit -am "fix: resolve build issues after playback integration"
```
