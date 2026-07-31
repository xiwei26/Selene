# Selene Native iOS

`native-ios` is the SwiftUI rewrite of Selene for iPhone and iPad, with an
iPad-first interface.

## Native feature set

- Native LunaTV server login and Keychain-persisted session
- Base58 local subscription mode (search + live only)
- `NavigationSplitView` sidebar designed for iPadOS
- Home, continue-watching, Bangumi and adaptive Douban category layouts
- LunaTV SSE aggregated search with progress and standard-search fallback
- Douban metadata, recommendations and full-screen poster viewing
- Favorites, search history and playback history synchronized with LunaTV
- Live TV source/group browsing, EPG and unified AVKit playback
- Short drama, Bilibili, YouTube and server administration surfaces
- AVKit playback with source/episode switching and progress persistence
- Background audio session for continued playback
- System, light and dark appearance modes
- Responsive compact layouts for iPhone

The navigation and primary feature surface are aligned with `native-macos`,
while using iPadOS-native split views, sheets, toolbars and adaptive grids.

## Security notes

- Session cookies are stored in the Keychain (`AfterFirstUnlockThisDeviceOnly`),
  not UserDefaults. Older plaintext sessions are migrated once and removed.
- App Transport Security allows local-network HTTP and non-HTTPS *media*
  streams only. Prefer `https://` server and subscription API hosts.
- Local subscription mode hides server-only destinations (home, Douban,
  platforms, library, admin).

## Build

Open `Selene.xcodeproj` in Xcode, select an iPad simulator, and run the
`Selene` scheme.

Command-line simulator build:

```sh
xcodebuild \
  -project native-ios/Selene.xcodeproj \
  -scheme Selene \
  -sdk iphonesimulator \
  -destination 'generic/platform=iOS Simulator' \
  CODE_SIGNING_ALLOWED=NO \
  build
```

Contract smoke tests (no Simulator required):

```sh
native-ios/script/run_tests.sh
```

When the installed Simulator runtime does not match the Xcode SDK, create a
universal simulator package without launching:

```sh
native-ios/script/package_simulator.sh
```

The script produces:

```text
native-ios/build/Simulator/Selene.app
native-ios/build/Simulator/Selene-native-ios-simulator.zip
```