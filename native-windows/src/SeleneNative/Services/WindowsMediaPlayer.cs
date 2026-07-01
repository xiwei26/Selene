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
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
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
            SetError("Invalid video URL.");
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
        if (_disposed)
        {
            return;
        }

        _player.Pause();
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

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
        if (_disposed)
        {
            return;
        }

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
        var message = string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? "Playback failed. Windows Media Player could not open this source."
            : $"Playback failed: {args.ErrorMessage}";
        SetError(message);
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
