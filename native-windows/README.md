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
- In-app playback with Windows `MediaPlayer` / WinUI `MediaPlayerElement` and
  its native transport controls; see [Player](#player).

Features depend on the connected LunaTV server and Windows media codec support.
The core API clients, models, and view models remain independent of WinUI.

## Player

The player page has three parts: the video with the native transport controls,
a fixed toolbar, and an info area that scrolls on its own and can be folded
away. Nothing needed for playback sits in the scrolling area.

| Feature | Behavior |
| --- | --- |
| Native controls | Play/pause, elapsed and remaining time, seek bar, volume and mute, shown on pointer move, touch, or keyboard focus. |
| Toolbar | Previous/next episode, back/forward 10 seconds, play/pause, mute, volume, speed, episode picker, source list, full screen. The "more" menu holds auto-next, show/hide info, and reload. Every command has a Chinese label and tooltip. |
| Seeking | Buttons and keys move 10 seconds within the media. Offered only when the engine reports the media as seekable; otherwise the buttons and the native seek bar are disabled. |
| Volume | 0-100%. Mute keeps the volume; unmuting restores it. Native controls, toolbar, and keys all show the engine's value. |
| Speed | 0.5x, 0.75x, 1x, 1.25x, 1.5x, 2x. A speed the media cannot play is listed disabled with the reason. The chosen speed is kept across episodes and sources until the app closes. |
| Full screen | Real window full screen (`AppWindow` presenter): title bar, navigation, page title, info, and video margins are hidden. The toolbar floats over the picture and hides when idle. Leaving restores the window's position, size, and maximized state. The video element is not re-created, so playback is not interrupted. |
| Episodes | Previous/next follow the backend order and are disabled at the first/last episode. "倒序" only changes the order shown in the picker, never the episode number or history. |
| Auto-next | On by default, saved in `player.json`. An episode that plays to its end starts the next one once. Errors, stopping, and seeking to the end do not. The last episode shows "播放完毕". |
| Sources | Lists other sources of the same work and switches to the matching episode, keeping position, paused state, and speed. If the target lacks the episode or its detail fails to load, the current source keeps playing and the reason is shown. |
| Retry and resume | Reload keeps the episode and position. A resume position is applied once, when the media is open, seekable, and has a known duration. |
| Records | The record is taken before the episode or source changes, when leaving the page, and when the window closes (the window waits for the write). It is written locally first, then to the server. |

### Shortcuts

| Key | Action |
| --- | --- |
| Space | Play / pause |
| Left / Right | Back / forward 10 seconds |
| Up / Down | Volume +5% / -5% (Up also unmutes) |
| M | Mute / unmute |
| F / F11 | Toggle full screen |
| Esc | Close the open menu or flyout; otherwise leave full screen. Never closes the player. |
| Ctrl+Left / Ctrl+Right | Previous / next episode |

Shortcuts work only while the player page is showing, and a key press is
handled once: either as a shortcut or by the focused control. An open menu or
flyout keeps every key. A focused button keeps Space and Enter; a focused
toolbar command also keeps the arrow keys, which move between commands; a
slider keeps the arrow keys; text boxes, combo boxes, and the navigation pane
keep everything except F11 and Esc. A mouse click on the toolbar does not take
focus away from the video.

### Limits

- Which sources count as the same work is decided conservatively: the same
  Douban id, or the same normalized title with no conflicting year or media
  type. Season and part markers must match, so "第二季" and "第2季" are the same
  work but a bare "2" suffix is not treated as "第二季". A source that only
  shares a keyword is never offered.
- Episodes are matched by name first, then by episode number; the plain position
  is used only when both sources list the same number of episodes.
- Playback started from history looks up other sources the first time the
  source list is opened, not before.
- If a newly selected source fails after the switch, the player shows the error
  and stays on it; it does not switch back by itself.
- Volume and speed last until the app closes. Only auto-next is persisted.
- Records are not saved periodically, so a crash loses the progress made since
  the last episode change.
- The WinUI 3 transport controls have no full-window button and drop buttons
  when narrow, which is why speed, 10-second steps, episodes, and full screen
  are toolbar commands. Below about 700 px of content width the toolbar moves
  previous/next and the 10-second steps into its "more" menu first.
- Not implemented: subtitles and audio-track selection, picture-in-picture,
  casting (the native cast button is hidden), intro/outro skipping, and manual
  quality selection.

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
`-Version` when packaging a separately versioned release. Close a native client
that was started from this folder (`run.ps1` or a build output) before
rebuilding; a copy installed elsewhere can stay open. Scripts resolve paths
relative to their own location.

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

### Player Verification

Two extra checks exercise the real Windows media engine. Both render their own
short solid-colour MP4 clips with `MediaComposition`, so they need no server and
no network. Neither is part of `build.ps1` or the installer.

Engine probe, with no window: opened versus playing, volume, mute, speeds,
seeking, resume, source switch, auto-next, rapid episode changes, errors, close.

```powershell
dotnet run --project .\native-windows\tests\SeleneNative.PlaybackProbe -c Release -p:Platform=x64
```

UI smoke test, in the real window: layout at 1024x640, 800x600, and maximized,
toolbar commands, flyouts, focus and key routing, full-screen round trips,
auto-next, themes, errors, leaving, and closing the window while playing. It
opens the app for about two minutes, enters full screen briefly, writes
`report.txt` and PNG renderings to the output folder, and exits with 0 when
every check passes.

```powershell
dotnet build .\native-windows\src\SeleneNative\SeleneNative.csproj -c Release -p:Platform=x64 -p:SeleneUiSmoke=true
$env:SELENE_NATIVE_DATA_DIR = "$env:TEMP\SeleneNativeUiSmoke\data"
$env:SELENE_NATIVE_UISMOKE_OUT = "$env:TEMP\SeleneNativeUiSmoke\out"
.\native-windows\src\SeleneNative\bin\x64\Release\net8.0-windows10.0.19041.0\SeleneNative.exe
```

`SELENE_NATIVE_DATA_DIR` moves the session, records, settings, and cache to
another folder, so a test instance never touches the installed app's data. The
smoke script is compiled in only by `-p:SeleneUiSmoke=true`; rebuild without it
(or run `build.ps1`, which cleans first) before publishing.

The smoke test drives buttons through their automation peers and posts key
messages to its own window. It does not cover a physical pointer hovering or
dragging the native seek bar, touch, audible output, a screen reader, display
scales other than the one it runs on, or streams from a real LunaTV server.

## Project Layout

```text
native-windows/
|-- src/
|   |-- SeleneNative/          WinUI shell, native controls, and Windows media host
|   `-- SeleneNative.Core/     Models, LunaTV services, and view models
|-- tests/
|   |-- SeleneNative.Tests/           xUnit tests
|   |-- SeleneNative.PlaybackProbe/   Headless check of the real media engine
|   `-- SeleneNative.UiSmoke/         In-process UI smoke test (opt-in build)
|-- build.ps1                 Build, test, and self-contained publish
|-- build-installer.ps1       Inno Setup installer packaging
|-- installer.iss
`-- run.ps1                   Launch the published native app
```

## Architecture

Playback has one coordinator, `PlayerViewModel` in `SeleneNative.Core`. It owns
the current work and episode, the resume position, episode and source changes,
auto-next, and the record snapshots; the page only renders its state and
forwards commands. `IMediaPlayer` reports the engine's real state, seekability,
volume, mute, and speed, each with a change notification, because the native
transport controls drive the engine directly. `WindowsMediaPlayer` re-raises
engine callbacks on the UI thread and drops callbacks that belong to an item
that has since been replaced. `SourceMatcher` decides which results are the same
work and which episode corresponds, and `PlayerShortcuts` maps a key press to at
most one command. `MainWindow` owns the window presenter for full screen and
holds the window open on close until the record is written. The requirements
are in [the player-controls handoff](../docs/superpowers/plans/2026-10-13-windows-player-controls.md).

`App.xaml` loads WinUI's `XamlControlsResources` so controls use the standard
Fluent styles and Windows accent color. The shell applies the persisted theme
through `Root.RequestedTheme`; custom content uses dynamic theme resources.
The player keeps its video surface dark while other metadata follows the theme.

Pages are built with native WinUI controls in XAML and C#. Theme-aware shared
components live in `UiHelpers.cs`. `SeleneNative.Core` uses the existing LunaTV
routes and response shapes, including `/api/search`, `/api/detail`,
`/api/playrecords`, and server-side `/api/douban/*` requests for logged-in users.
