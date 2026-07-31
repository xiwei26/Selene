# Agent Instructions

## Backend Reference

The backend used by this project is:

```text
https://github.com/SzeMeng76/LunaTV
```

When updating API clients, match LunaTV routes and response shapes first. Native
clients should prefer the LunaTV server API surface when a user session has a
server URL.

Important native client expectations include:

- Search and detail routes are LunaTV-compatible `/api/search`, `/api/search/ws`,
  `/api/detail`, and `/api/search/resources`.
- Live TV routes may return `{ "success": true, "data": ... }`; native clients
  should support that envelope.
- LunaTV SSE search events put the event type in JSON data, for example
  `{"type":"source_result"}`.
- Douban category requests should follow the Flutter/LunaTV-compatible query
  shapes already used in `lib/services/douban_service.dart`.

In particular, Douban category data should use:

- `/api/douban/categories?kind=movie&category=\u70ed\u95e8&type=\u5168\u90e8`
- `/api/douban/categories?kind=tv&category=tv&type=tv`
- `/api/douban/categories?kind=tv&category=show&type=show`
- `/api/douban/categories?kind=tv&category=tv&type=tv_animation`

Do not assume the old direct Douban mobile endpoints are the primary backend.

## Native Client Architecture

- `native-ios/` is the native iPhone/iPad client. Treat `native-macos/` as the
  behavioral reference, but use iPadOS-native patterns: `NavigationSplitView`,
  adaptive grids, sheets, toolbars, and compact-width fallbacks.
- Keep networking in the `LunaTVClient` actor and keep UI state in
  `@MainActor @Observable` stores. Do not put URLSession calls directly in
  SwiftUI views.
- Playback state belongs in one shared coordinator. Persist records by exact
  `source + id` before clearing the player, and preserve backend episode order.
- The local Base58 subscription mode is intentionally separate from a server
  session. Do not assume favorites, history, or admin routes are available when
  `isLocalMode` is true.

## API and Decoding Lessons

- LunaTV responses are not uniform. Support bare arrays/objects and common
  `{ "data": ... }`, `{ "success": true, "data": ... }`, `list`, `results`,
  and `items` wrappers where the route permits them.
- Decode the preferred wrapper before decoding a permissive model. Models with
  default values can incorrectly decode an outer `{ "data": ... }` object as an
  empty model if the bare-object attempt runs first.
- SSE search events may put the event name inside JSON data (`type`) instead of
  an SSE `event:` line. Handle `start`, `source_result`, `source_error`, and
  `complete`, and keep a normal `/api/search` fallback.
- Preserve the backend cookie on every authenticated request, and include the
  base URL path when joining route paths so installations under a reverse-proxy
  prefix continue to work.
- Add a small `URLProtocol` contract smoke test for every new route family. At
  minimum assert the exact query shape, cookie header, and wrapper decoding.

## iOS Build and Distribution Lessons

- A simulator `.app` or simulator zip cannot be installed on a physical iPad.
  A device install requires an `iphoneos` archive, Apple signing, and an IPA or
  direct Xcode install. SideStore also requires a device IPA; never reuse the
  `iphonesimulator` binary.
- The iPad-first app can be compiled with strict warnings-as-errors typechecking
  even when Xcode cannot resolve a simulator destination because the installed
  Simulator Runtime does not match the Xcode SDK. Record that environment issue
  separately from source failures.
- `native-ios/script/package_simulator.sh` creates and verifies a universal
  arm64/x86_64 simulator app. Keep `native-ios/build/` ignored and do not commit
  derived data, app bundles, zips, or test binaries.
- For visual verification, install the generated app into an available iPad
  Simulator and launch it. Check the login form, keyboard avoidance, split-view
  navigation, playback sheet, and compact layout before handing off.

## Change and Git Hygiene

- Check `git worktree list`, `git status -sb`, and the diff before staging. An
  uncommitted change belongs to a worktree, not to an abstract branch; do not
  silently stage unrelated work.
- Create a feature branch from the repository default branch for publishable
  work. Keep generated packages out of commits and include the validation and
  packaging result in the PR description.

## Packaging After Changes

After every native client modification, rebuild and repackage the affected
native app before finishing the task. Do not revert or discard unrelated
working-tree changes while packaging.

For native Windows changes, run the Windows packaging flow after tests pass so
`native-windows/publish/win-x64` reflects the latest source.

For every new feature or code fix, also produce an installable Windows `.exe`
package before handing the work back, and report the installer path in the final
response. Do not stop at loose build output or the publish directory when an
installer can be built.

For native macOS changes, use the existing package-only script from the
repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

This rebuilds the Swift release binary, recreates the `.app` bundle, and
produces an installable DMG:

```text
native-macos/dist/Selene-1.1.0-macos.dmg
```

After packaging, verify both the app bundle and DMG exist and were freshly
generated. A concise check is:

```sh
ls -la native-macos/SeleneNative.app native-macos/SeleneNative.app/Contents native-macos/SeleneNative.app/Contents/MacOS native-macos/SeleneNative.app/Contents/Resources
plutil -p native-macos/SeleneNative.app/Contents/Info.plist
file native-macos/SeleneNative.app/Contents/MacOS/SeleneNative
ls -lh native-macos/dist/Selene-1.1.0-macos.dmg
```

Report the DMG path in the final response. Do not stop at the `.app` bundle
when a DMG can be built. Do not launch the app unless the user explicitly
asks.