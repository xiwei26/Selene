# Agent Instructions

## Backend Reference

The backend used by this project is:

```text
https://github.com/SzeMeng76/LunaTV
```

When updating API clients, match LunaTV routes and response shapes first. Important native client expectations include:

- Search and detail routes are LunaTV-compatible `/api/search`, `/api/search/ws`, `/api/detail`, and `/api/search/resources`.
- Live TV routes may return `{ "success": true, "data": ... }`; native clients should support that envelope.
- LunaTV SSE search events put the event type in JSON data, for example `{"type":"source_result"}`.
- Douban category requests should follow the Flutter/LunaTV-compatible query shapes already used in `lib/services/douban_service.dart`.

## Native macOS Packaging

After every modification, rebuild and repackage the native macOS app before finishing the task.

Use the existing package-only script from the repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

This rebuilds the Swift release binary and recreates:

```text
native-macos/SeleneNative.app
```

After packaging, verify the app bundle exists and was freshly generated. A concise check is:

```sh
ls -la native-macos/SeleneNative.app native-macos/SeleneNative.app/Contents native-macos/SeleneNative.app/Contents/MacOS native-macos/SeleneNative.app/Contents/Resources
plutil -p native-macos/SeleneNative.app/Contents/Info.plist
file native-macos/SeleneNative.app/Contents/MacOS/SeleneNative
```

Do not launch the app unless the user explicitly asks. Do not revert or discard unrelated working-tree changes while packaging.
