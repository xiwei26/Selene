# Native Windows System Player Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the bundled VLC Windows playback engine with Windows' built-in media player stack to reduce installer size.

**Architecture:** Keep `SeleneNative.Core.Services.IMediaPlayer` as the stable boundary. The WinUI host will provide a new `WindowsMediaPlayer` adapter around `Windows.Media.Playback.MediaPlayer`, and `NativeVideoPlayerView` will host it through `MediaPlayerElement`.

**Tech Stack:** .NET 8, WinUI 3, Windows App SDK, `Windows.Media.Playback.MediaPlayer`, xUnit, Inno Setup packaging.

## Global Constraints

- The default Windows app package must not include `LibVLCSharp.WinUI` or `VideoLAN.LibVLC.Windows`.
- `PlayerViewModel`, play history, resume, episode switching, and existing player controls must keep using `IMediaPlayer`.
- The Core project must remain platform-neutral and must not reference WinUI or Windows media APIs.
- Windows built-in playback should report clear errors when a source cannot be opened.
- After native Windows changes, run tests, publish `native-windows/publish/win-x64`, and build an installable `.exe` installer.

---

## File Structure

- `native-windows/src/SeleneNative/Services/WindowsMediaPlayer.cs`: new Windows-native `IMediaPlayer` implementation and DI extension.
- `native-windows/src/SeleneNative/Views/NativeVideoPlayerView.xaml.cs`: replace VLC `VideoView` setup with a `MediaPlayerElement` surface.
- `native-windows/src/SeleneNative/SeleneNative.csproj`: remove VLC package references.
- `native-windows/src/SeleneNative/App.xaml.cs`: register `AddWindowsMediaPlayer`.
- `native-windows/tests/SeleneNative.Tests/Shell/WindowsMediaPlayerDependencyTests.cs`: text-level host tests that prevent VLC dependencies from returning.

### Task 1: Lock In Dependency Removal

**Files:**
- Create: `native-windows/tests/SeleneNative.Tests/Shell/WindowsMediaPlayerDependencyTests.cs`

**Interfaces:**
- Consumes: repository files as text.
- Produces: tests that assert the host project no longer references VLC and does reference the Windows media implementation.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class WindowsMediaPlayerDependencyTests
{
    [Fact]
    public void WindowsHost_ShouldNotReferenceBundledVlcPackages()
    {
        var project = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "SeleneNative.csproj"));

        Assert.DoesNotContain("LibVLCSharp", project);
        Assert.DoesNotContain("VideoLAN.LibVLC.Windows", project);
    }

    [Fact]
    public void App_ShouldRegisterWindowsMediaPlayer()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "App.xaml.cs"));

        Assert.Contains("AddWindowsMediaPlayer", source);
        Assert.DoesNotContain("AddLibVlcMediaPlayer", source);
    }

    [Fact]
    public void NativeVideoPlayerView_ShouldUseMediaPlayerElement()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "NativeVideoPlayerView.xaml.cs"));

        Assert.Contains("MediaPlayerElement", source);
        Assert.Contains("WindowsMediaPlayer", source);
        Assert.DoesNotContain("LibVLCSharp", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(relativeParts));
    }
}
```

- [ ] **Step 2: Run the new test to verify it fails**

Run: `dotnet test native-windows/tests/SeleneNative.Tests/SeleneNative.Tests.csproj -c Release -p:Platform=x64 --filter WindowsMediaPlayerDependencyTests`

Expected: FAIL because the project still references VLC, `App.xaml.cs` still registers `AddLibVlcMediaPlayer`, and the video surface still imports `LibVLCSharp`.

### Task 2: Replace VLC With Windows Media Player

**Files:**
- Create: `native-windows/src/SeleneNative/Services/WindowsMediaPlayer.cs`
- Modify: `native-windows/src/SeleneNative/Views/NativeVideoPlayerView.xaml.cs`
- Modify: `native-windows/src/SeleneNative/App.xaml.cs`
- Modify: `native-windows/src/SeleneNative/SeleneNative.csproj`
- Delete: `native-windows/src/SeleneNative/Services/LibVlcMediaPlayer.cs`

**Interfaces:**
- Consumes: `IMediaPlayer.Load(string url)`, `Play`, `Pause`, `Stop`, `Position`, `Length`, `StateChanged`, `PositionChanged`, and `Error`.
- Produces: `WindowsMediaPlayer.AttachTo(MediaPlayerElement element)`, `WindowsMediaPlayer.DetachFrom(MediaPlayerElement element)`, and `IServiceCollection AddWindowsMediaPlayer(this IServiceCollection services)`.

- [ ] **Step 1: Implement `WindowsMediaPlayer`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SeleneNative.Services;

using CorePlaybackState = SeleneNative.Core.Services.MediaPlaybackState;
using WindowsPlaybackState = Windows.Media.Playback.MediaPlaybackState;

public sealed class WindowsMediaPlayer : IMediaPlayer
{
    private readonly object _gate = new();
    private readonly MediaPlayer _player = new();
    private MediaPlayerElement? _attachedElement;
    private CorePlaybackState _state = CorePlaybackState.Stopped;
    private bool _disposed;

    public WindowsMediaPlayer()
    {
        _player.AutoPlay = false;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPositionChanged;
    }

    public event EventHandler<MediaStateChangedEventArgs>? StateChanged;
    public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<MediaErrorEventArgs>? Error;

    public CorePlaybackState State
    {
        get { lock (_gate) { return _state; } }
    }

    public double Length
    {
        get
        {
            var duration = _player.PlaybackSession.NaturalDuration;
            return duration <= TimeSpan.Zero ? 0 : duration.TotalSeconds;
        }
    }

    public double Position
    {
        get => Math.Max(0, _player.PlaybackSession.Position.TotalSeconds);
        set
        {
            var length = Length;
            var target = length > 0 ? Math.Clamp(value, 0, length) : Math.Max(0, value);
            _player.PlaybackSession.Position = TimeSpan.FromSeconds(target);
        }
    }

    public void Load(string url)
    {
        ThrowIfDisposed();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            SetError("视频地址无效。");
            return;
        }

        SetState(CorePlaybackState.Opening);
        _player.Source = MediaSource.CreateFromUri(uri);
    }

    public void Play()
    {
        ThrowIfDisposed();
        _player.Play();
    }

    public void Pause()
    {
        if (_disposed) return;
        _player.Pause();
    }

    public void Stop()
    {
        if (_disposed) return;
        _player.Pause();
        _player.Source = null;
        SetState(CorePlaybackState.Stopped);
    }

    public void AttachTo(MediaPlayerElement element)
    {
        lock (_gate)
        {
            _attachedElement = element;
            element.SetMediaPlayer(_player);
        }
    }

    public void DetachFrom(MediaPlayerElement element)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_attachedElement, element))
            {
                return;
            }

            element.SetMediaPlayer(null);
            _attachedElement = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.Dispose();
    }

    private void OnMediaOpened(MediaPlayer sender, object args) => SetState(CorePlaybackState.Playing);
    private void OnMediaEnded(MediaPlayer sender, object args) => SetState(CorePlaybackState.Ended);

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        SetError(string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? "视频播放失败，Windows 内置播放器无法打开该资源。"
            : $"视频播放失败：{args.ErrorMessage}");
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        var mapped = sender.PlaybackState switch
        {
            WindowsPlaybackState.Opening => CorePlaybackState.Opening,
            WindowsPlaybackState.Buffering => CorePlaybackState.Buffering,
            WindowsPlaybackState.Playing => CorePlaybackState.Playing,
            WindowsPlaybackState.Paused => CorePlaybackState.Paused,
            _ => State,
        };

        SetState(mapped);
    }

    private void OnPositionChanged(MediaPlaybackSession sender, object args) =>
        PositionChanged?.Invoke(this, new MediaPositionChangedEventArgs(sender.Position.TotalSeconds));

    private void SetError(string message)
    {
        SetState(CorePlaybackState.Error);
        Error?.Invoke(this, new MediaErrorEventArgs(message));
    }

    private void SetState(CorePlaybackState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, new MediaStateChangedEventArgs(state));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

internal static class WindowsMediaPlayerServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsMediaPlayer(this IServiceCollection services)
    {
        services.AddSingleton<IMediaPlayer>(_ => new WindowsMediaPlayer());
        return services;
    }
}
```

- [ ] **Step 2: Replace the video surface**

Set `NativeVideoPlayerView.xaml.cs` to create a `MediaPlayerElement`, attach it to `WindowsMediaPlayer`, toggle the loading ring from `StateChanged`, and detach on unload.

- [ ] **Step 3: Update DI and project dependencies**

Change `App.xaml.cs` from `services.AddLibVlcMediaPlayer();` to `services.AddWindowsMediaPlayer();`. Remove the `LibVLCSharp.WinUI` and `VideoLAN.LibVLC.Windows` package references from `SeleneNative.csproj`. Delete `LibVlcMediaPlayer.cs`.

- [ ] **Step 4: Run tests**

Run: `dotnet test native-windows/tests/SeleneNative.Tests/SeleneNative.Tests.csproj -c Release -p:Platform=x64`

Expected: PASS.

### Task 3: Package and Verify

**Files:**
- Generated: `native-windows/publish/win-x64/SeleneNative.exe`
- Generated: `native-windows/dist/selene-<version>-windows-x64-setup.exe`

**Interfaces:**
- Consumes: passing Windows source and tests.
- Produces: fresh publish output and installer.

- [ ] **Step 1: Build, test, and publish**

Run: `.\build.ps1 -Configuration Release -Platform x64` from `native-windows`.

Expected: PASS, with `native-windows/publish/win-x64/SeleneNative.exe` present.

- [ ] **Step 2: Build installer**

Run: `.\build-installer.ps1 -Configuration Release -Platform x64 -SkipBuild` from `native-windows`.

Expected: PASS, with `native-windows/dist/selene-<version>-windows-x64-setup.exe` present.

- [ ] **Step 3: Verify VLC files are gone from publish output**

Run: `Get-ChildItem native-windows/publish/win-x64 -Recurse | Where-Object { $_.Name -match 'vlc|libvlc|VideoLAN' }`

Expected: no output.
