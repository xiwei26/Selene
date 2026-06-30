# Selene

Selene is a native desktop video client backed by the LunaTV API surface.

The desktop implementation is now focused on native clients:

- `native-macos/`: SwiftUI macOS app.
- `native-windows/`: WinUI 3 / .NET 8 Windows app.

The old Flutter desktop folders `macos/` and `windows/` have been removed so
desktop work does not split between cross-platform and native implementations.

## Backend

Selene targets the LunaTV backend:

```text
https://github.com/SzeMeng76/LunaTV
```

Native clients should use LunaTV-compatible server routes when a user session
has a server URL. Important API families include:

- Search and detail: `/api/search`, `/api/search/ws`, `/api/detail`,
  `/api/search/resources`.
- Playback history: `/api/playrecords`.
- Favorites and search history through the server content provider.
- Live TV with support for `{ "success": true, "data": ... }` envelopes.
- Douban categories and details through LunaTV-compatible `/api/douban/*`
  endpoints.

Recommended Douban category requests:

```text
/api/douban/categories?kind=movie&category=\u70ed\u95e8&type=\u5168\u90e8
/api/douban/categories?kind=tv&category=tv&type=tv
/api/douban/categories?kind=tv&category=show&type=show
/api/douban/categories?kind=tv&category=tv&type=tv_animation
```

Do not treat the old direct Douban mobile endpoints as the primary backend.

## Native Windows

The Windows client lives in `native-windows/`.

Stack:

- WinUI 3
- .NET 8
- Windows App SDK
- LibVLCSharp for in-app playback
- xUnit tests
- Inno Setup installer packaging

Current playback behavior:

- Episodes preserve backend order.
- Continue-watching resolves by exact source/id first, then only by same title.
- Returning from the player persists the current episode and progress before
  playback state is cleared.
- Playback records are saved to the backend through `/api/playrecords` when a
  server session exists, and the home screen reloads continue-watching from that
  backend provider.
- The player includes seek, retry, episode order, and pause/resume controls.

Build, test, publish, and create an installable `.exe`:

```powershell
cd native-windows
.\build-installer.ps1 -Platform x64
```

The installer is written to:

```text
native-windows/dist/selene-1.6.8-windows-x64-setup.exe
```

Run the full Windows test suite:

```powershell
dotnet test .\native-windows\SeleneNative.sln
```

## Native macOS

The macOS client lives in `native-macos/`.

Package the native macOS app from the repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

The generated app bundle is:

```text
native-macos/SeleneNative.app
```

## Repository Layout

```text
native-macos/      Native SwiftUI macOS client
native-windows/    Native WinUI 3 Windows client
lib/               Shared Flutter-era API references and mobile/web code
android/ ios/      Flutter mobile shells
web/ linux/        Legacy Flutter targets
docs/              Planning and migration notes
agents.md          Agent build and packaging instructions
```

For new desktop work, prefer the native clients over Flutter desktop targets.

## Development Rules

- Match LunaTV routes and response shapes first.
- Keep native client behavior aligned across macOS and Windows where practical.
- After Windows native changes, run tests and build an installable `.exe`.
- After macOS native changes, rebuild the `.app` bundle.
- Do not stop at loose publish output when an installer or app bundle can be
  produced.
