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
