# Selene Native Windows

Selene's Windows entry point is the native WinUI 3 / .NET 8 app in this folder.
The installer and Start menu shortcut launch `SeleneNative.exe`.

## Interface

- Native Fluent controls and an adaptive `NavigationView` that collapses at
  smaller window sizes.
- A draggable WinUI title bar with system window buttons and a Mica backdrop on
  supported Windows versions; Windows provides the fallback surface.
- System, light, and dark themes. Changes apply immediately and persist for the
  next launch.
- A home recommendation, continue-watching posters, movie/TV/show categories,
  and the Bangumi calendar. Poster actions support Tab, Enter, and Space.
- Native login, search, favorites, history, live TV, settings, and admin pages.
- LunaTV short-drama, Bilibili, YouTube, Douban, and TMDB feature pages.
- In-app playback with Windows `MediaPlayer` / WinUI `MediaPlayerElement`,
  seeking, retry, episode switching, resume, and backend playback records.

Features depend on the connected LunaTV server and Windows media codec support.
The core API clients, models, and view models remain independent of WinUI.

## Requirements

- Windows 10 version 1903 (build 18362) or later; Windows 11 supports Mica.
- .NET 8 SDK for development.
- Inno Setup 6 for creating the installer.
- Visual Studio 2022 is optional for command-line builds.

The installer includes the .NET and Windows App SDK runtime files.

## Build, Test, And Package

From the repository root:

```powershell
.\native-windows\build-installer.ps1 -Platform x64
```

This runs the Windows build and tests, publishes the self-contained app to
`native-windows/publish/win-x64`, and produces:

```text
native-windows/dist/selene-1.1.0-windows-x64-setup.exe
```

The version comes from the repository's `pubspec.yaml`; override it with
`-Version` when packaging a separately versioned release. Close a running native
client before rebuilding. Scripts resolve paths relative to their own location.

Build and publish without an installer:

```powershell
.\native-windows\build.ps1 -Platform x64
```

Run the published native client:

```powershell
.\native-windows\run.ps1
```

Run core and regression tests:

```powershell
dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj -c Release -p:Platform=x64
```

## Project Layout

```text
native-windows/
|-- src/
|   |-- SeleneNative/          WinUI shell, native controls, and Windows media host
|   `-- SeleneNative.Core/     Models, LunaTV services, and view models
|-- tests/
|   `-- SeleneNative.Tests/    xUnit tests
|-- build.ps1                 Build, test, and self-contained publish
|-- build-installer.ps1       Inno Setup installer packaging
|-- installer.iss
`-- run.ps1                   Launch the published native app
```

## Architecture

`App.xaml` loads WinUI's `XamlControlsResources` so controls use the standard
Fluent styles and Windows accent color. The shell applies the persisted theme
through `Root.RequestedTheme`; custom content uses dynamic theme resources.
The player keeps its video surface dark while other metadata follows the theme.

Pages are built with native WinUI controls in XAML and C#. Theme-aware shared
components live in `UiHelpers.cs`. `SeleneNative.Core` uses the existing LunaTV
routes and response shapes, including `/api/search`, `/api/detail`,
`/api/playrecords`, and server-side `/api/douban/*` requests for logged-in users.
