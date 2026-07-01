# Native Windows System Player Design

## Goal

Reduce the native Windows installer size by replacing the bundled VLC playback
engine with the Windows built-in media stack for the default in-app player.

The user-facing playback workflow should remain the same: selecting an episode
opens the in-app player, progress is saved, resume continues to work, and the
existing player controls keep their current behavior.

## Current State

The Windows app uses `IMediaPlayer` in the Core project and binds the WinUI host
to a `LibVlcMediaPlayer` implementation. The app package includes
`LibVLCSharp.WinUI` and `VideoLAN.LibVLC.Windows`, which pull large native VLC
runtime files into the publish and installer output.

This is a good replacement point because `PlayerViewModel`, history, resume, and
episode switching already depend on the `IMediaPlayer` abstraction rather than
VLC directly.

## Recommended Approach

Use WinUI's `MediaPlayerElement` plus `Windows.Media.Playback.MediaPlayer` as
the default Windows playback engine.

The implementation should:

- Add a Windows-native `IMediaPlayer` implementation.
- Replace the VLC-specific video surface with a `MediaPlayerElement` surface.
- Remove the VLC NuGet references from `SeleneNative.csproj`.
- Keep the public `IMediaPlayer` contract unchanged unless a small compatibility
  addition is required.
- Preserve progress, duration, play, pause, stop, seek, and error propagation.

## Compatibility

Windows' built-in media stack should cover common MP4, H.264/AAC, and many HLS
streams. It will not be as forgiving as VLC for unusual containers, codecs,
playlist structures, or source-specific quirks.

To avoid making playback brittle, the first implementation should include clear
error reporting when Windows cannot open a source. A later compatibility feature
can add an optional external-player fallback for users who need VLC-level codec
coverage without bundling VLC inside Selene.

## Architecture

`PlayerViewModel` remains the state owner. It continues to call `IMediaPlayer`
for `Load`, `Play`, `Pause`, `Stop`, and `Position`.

The Windows host provides:

- `WindowsMediaPlayer`, an `IMediaPlayer` adapter around
  `Windows.Media.Playback.MediaPlayer`.
- `NativeVideoPlayerView`, a WinUI user control that owns a `MediaPlayerElement`
  and attaches it to `WindowsMediaPlayer`.
- DI registration through an `AddWindowsMediaPlayer` extension.

The Core project should remain platform-neutral and should not reference WinUI
or Windows media APIs.

## Testing

Existing `PlayerViewModel` unit tests should keep passing because they use a
fake `IMediaPlayer`.

Add focused host-level tests where practical for:

- The Windows app no longer references VLC packages.
- The video surface and DI registration refer to the Windows media
  implementation.

Manual verification after implementation should include:

- Build and test the Windows solution.
- Publish the Windows app.
- Build an installable `.exe` installer.
- Report the installer path.

## Out of Scope

This change will not add WebView2 playback, mpv, external VLC launching, custom
subtitle rendering, or codec installation guidance. Those can be evaluated after
the native Windows player is measured against real content sources.
