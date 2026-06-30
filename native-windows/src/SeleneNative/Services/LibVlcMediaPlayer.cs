using System;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SeleneNative.Core.Services;

namespace SeleneNative.Services;

public sealed class LibVlcMediaPlayer : IMediaPlayer
{
    private readonly object _gate = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private LibVLCSharp.Platforms.Windows.VideoView? _attachedView;

    public LibVlcMediaPlayer()
    {
    }

    public event EventHandler<Core.Services.MediaStateChangedEventArgs>? StateChanged;
    public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<MediaErrorEventArgs>? Error;

    public MediaPlaybackState State
    {
        get
        {
            lock (_gate)
            {
                return _player?.State switch
                {
                    VLCState.Playing => MediaPlaybackState.Playing,
                    VLCState.Paused => MediaPlaybackState.Paused,
                    VLCState.Opening => MediaPlaybackState.Opening,
                    VLCState.Buffering => MediaPlaybackState.Buffering,
                    VLCState.Stopped => MediaPlaybackState.Stopped,
                    VLCState.Ended => MediaPlaybackState.Ended,
                    VLCState.Error => MediaPlaybackState.Error,
                    _ => MediaPlaybackState.Stopped,
                };
            }
        }
    }

    public double Length
    {
        get
        {
            lock (_gate)
            {
                return _player is null ? 0 : Math.Max(0, _player.Length / 1000.0);
            }
        }
    }

    public double Position
    {
        get
        {
            lock (_gate)
            {
                return _player is null ? 0 : Math.Max(0, _player.Position * _player.Length / 1000.0);
            }
        }
        set
        {
            lock (_gate)
            {
                if (_player is null || _player.Length <= 0) return;
                _player.Position = (float)Math.Clamp(value * 1000.0 / _player.Length, 0, 1);
            }
        }
    }

    public void Load(string url)
    {
        lock (_gate)
        {
            if (_libVlc is null)
            {
                throw new InvalidOperationException("Video surface is not initialized.");
            }

            DetachPlayer();
            _media = new Media(_libVlc, new Uri(url));
            _player = new MediaPlayer(_media) { EnableHardwareDecoding = true };
            _player.Playing += OnPlayingChanged;
            _player.Paused += OnPausedChanged;
            _player.Stopped += OnStoppedChanged;
            _player.EncounteredError += OnErrorEncountered;
            _player.EndReached += OnEndReached;
            _player.PositionChanged += OnNativePositionChanged;
            AttachCurrentPlayerToView();
        }
    }

    public void Play()
    {
        lock (_gate) { _player?.Play(); }
    }

    public void Pause()
    {
        lock (_gate) { _player?.Pause(); }
    }

    public void Stop()
    {
        lock (_gate) { _player?.Stop(); }
    }

    public void Dispose()
    {
        lock (_gate) { DetachPlayer(); }
    }

    internal void InitializeVideoView(
        LibVLCSharp.Platforms.Windows.VideoView view,
        IReadOnlyList<string> swapChainOptions)
    {
        lock (_gate)
        {
            _attachedView = view;
            if (_libVlc is null)
            {
                _libVlc = new LibVLC(swapChainOptions.ToArray());
                _libVlc.SetUserAgent("senshinya/selene/1.0.0", "selene-native-windows");
            }

            AttachCurrentPlayerToView();
        }
    }

    internal void DetachFromView(LibVLCSharp.Platforms.Windows.VideoView view)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_attachedView, view))
            {
                return;
            }

            view.MediaPlayer = null;
            _attachedView = null;
        }
    }

    private void AttachCurrentPlayerToView()
    {
        if (_attachedView is null)
        {
            return;
        }

        _attachedView.MediaPlayer = _player;
    }

    private void DetachPlayer()
    {
        if (_player is not null)
        {
            _player.Playing -= OnPlayingChanged;
            _player.Paused -= OnPausedChanged;
            _player.Stopped -= OnStoppedChanged;
            _player.EncounteredError -= OnErrorEncountered;
            _player.EndReached -= OnEndReached;
            _player.PositionChanged -= OnNativePositionChanged;
            try { _player.Stop(); } catch { }
            if (_attachedView is not null)
            {
                _attachedView.MediaPlayer = null;
            }
            _player.Dispose();
            _player = null;
        }
        _media?.Dispose();
        _media = null;
    }

    private void OnPlayingChanged(object? sender, EventArgs e) =>
        StateChanged?.Invoke(this, new Core.Services.MediaStateChangedEventArgs(MediaPlaybackState.Playing));
    private void OnPausedChanged(object? sender, EventArgs e) =>
        StateChanged?.Invoke(this, new Core.Services.MediaStateChangedEventArgs(MediaPlaybackState.Paused));
    private void OnStoppedChanged(object? sender, EventArgs e) =>
        StateChanged?.Invoke(this, new Core.Services.MediaStateChangedEventArgs(MediaPlaybackState.Stopped));
    private void OnErrorEncountered(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(this, new Core.Services.MediaStateChangedEventArgs(MediaPlaybackState.Error));
        Error?.Invoke(this, new MediaErrorEventArgs("视频播放失败,请重试"));
    }
    private void OnEndReached(object? sender, EventArgs e) =>
        StateChanged?.Invoke(this, new Core.Services.MediaStateChangedEventArgs(MediaPlaybackState.Ended));
    private void OnNativePositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
    {
        var length = Length;
        if (length <= 0) return;
        PositionChanged?.Invoke(this, new MediaPositionChangedEventArgs(e.Position * length));
    }
}

internal static class LibVlcMediaPlayerServiceCollectionExtensions
{
    public static IServiceCollection AddLibVlcMediaPlayer(this IServiceCollection services)
    {
        services.AddSingleton<IMediaPlayer>(sp => new LibVlcMediaPlayer());
        return services;
    }
}
