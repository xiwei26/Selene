# Selene Native Windows

This folder contains the native Windows client for Selene, built with WinUI 3
and .NET 8.

## Current Status

This is an early migration, not a full feature-parity port of the Flutter or
native macOS clients.

Implemented today:

- Self-contained WinUI 3 app publish and launch flow.
- Basic native shell with left navigation.
- Home screen data pipeline:
  - Continue watching
  - Hot movies
  - Hot TV shows
  - Today's Bangumi calendar
  - Hot shows
- Core models, services, and ViewModel coverage for the home screen.

Not implemented yet:

- Login and session management.
- Search, detail pages, categories, favorites, and full history pages.
- Video playback, resume, DLNA, PiP, and live TV playback.
- Settings, theme switching, update checking, and about page.
- Dependency injection setup, CommunityToolkit.Mvvm adoption, and libVLCSharp
  integration.

## Requirements

- Windows 10 version 1903 or later.
- .NET 8 SDK.
- Visual Studio 2022 is optional for command-line builds, but useful for WinUI
  debugging.

## Build And Run

From this directory:

```powershell
.\build.ps1
```

The publish output is written to:

```text
publish\win-x64
```

Run the published app:

```powershell
.\run.ps1
```

## Tests

```powershell
dotnet test tests\SeleneNative.Tests\SeleneNative.Tests.csproj -c Release -p:Platform=x64
```

## Project Layout

```text
native-windows/
|-- src/
|   |-- SeleneNative/          WinUI 3 application
|   `-- SeleneNative.Core/     Testable models, services, and ViewModels
|-- tests/
|   `-- SeleneNative.Tests/    xUnit tests
|-- build.ps1
`-- run.ps1
```

## Architecture Notes

- `MainWindow.xaml` currently contains only the root grid.
- The visible shell and home screen are constructed in `MainWindow.xaml.cs`
  because richer template-based XAML hit a Windows App SDK XAML compiler failure
  without actionable diagnostics.
- `EnableMsixTooling` is enabled for Windows App SDK build target support, while
  the app still publishes unpackaged and self-contained.
- Public API base URLs are currently hard-coded and should move behind
  configuration before broader feature migration.
