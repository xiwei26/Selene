using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SeleneNative.Services;

using CorePlaybackState = SeleneNative.Core.Services.MediaPlaybackState;
using WindowsPlaybackState = Windows.Media.Playback.MediaPlaybackState;

/// <summary>
/// <see cref="IMediaPlayer"/> over the Windows <see cref="MediaPlayer"/>. Engine
/// callbacks arrive on arbitrary threads; every event is re-raised on the UI
/// dispatcher, and callbacks that belong to an item that has since been replaced
/// are dropped. Native transport controls drive the same <see cref="MediaPlayer"/>
/// directly, so volume, mute, rate, seeks and state are all reported from the
/// engine rather than from what this class was asked to do.
/// </summary>
public sealed class WindowsMediaPlayer : IMediaPlayer
{
    private readonly object _gate = new();
    private readonly MediaPlayer _player = new();
    private readonly DispatcherQueue? _dispatcher;
    private MediaPlayerElement? _attachedElement;
    private volatile MediaSource? _currentSource;
    private CorePlaybackState _state = CorePlaybackState.Stopped;
    private bool _terminalLatch;
    private int _loadGeneration;
    private int _positionPending;
    private volatile bool _disposed;

    public WindowsMediaPlayer()
        : this(DispatcherQueue.GetForCurrentThread())
    {
    }

    public WindowsMediaPlayer(DispatcherQueue? dispatcher)
    {
        _dispatcher = dispatcher;
        _player.AutoPlay = false;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
        _player.VolumeChanged += OnVolumeChanged;
        _player.IsMutedChanged += OnVolumeChanged;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPositionChanged;
        _player.PlaybackSession.NaturalDurationChanged += OnSeekabilityChanged;
        _player.PlaybackSession.SeekableRangesChanged += OnSeekabilityChanged;
        _player.PlaybackSession.SeekCompleted += OnSeekCompleted;
        _player.PlaybackSession.PlaybackRateChanged += OnPlaybackRateChanged;
    }

    public event EventHandler<MediaStateChangedEventArgs>? StateChanged;
    public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<MediaErrorEventArgs>? Error;
    public event EventHandler? MediaOpened;
    public event EventHandler? SeekabilityChanged;
    public event EventHandler? SeekCompleted;
    public event EventHandler? VolumeChanged;
    public event EventHandler? PlaybackRateChanged;

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
            return duration <= TimeSpan.Zero || duration == TimeSpan.MaxValue ? 0 : duration.TotalSeconds;
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

    public bool CanSeek => IsCurrentSourceOpen() && _player.PlaybackSession.CanSeek;

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0, 1);
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set => _player.IsMuted = value;
    }

    public double PlaybackRate
    {
        get => _player.PlaybackSession.PlaybackRate;
        set => _player.PlaybackSession.PlaybackRate = value;
    }

    public bool SupportsPlaybackRate(double rate)
    {
        if (rate <= 0 || !IsCurrentSourceOpen())
        {
            return false;
        }

        try
        {
            return _player.PlaybackSession.IsSupportedPlaybackRateRange(rate, rate);
        }
        catch
        {
            return false;
        }
    }

    public void Load(string url)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _loadGeneration);
        lock (_gate)
        {
            _terminalLatch = false;
        }

        var previous = _currentSource;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _currentSource = null;
            _player.Source = null;
            Release(previous);
            SetError("视频地址无效。");
            return;
        }

        var source = MediaSource.CreateFromUri(uri);
        _currentSource = source;
        SetState(CorePlaybackState.Opening);
        _player.Source = source;
        Release(previous);
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

        Interlocked.Increment(ref _loadGeneration);
        lock (_gate)
        {
            _terminalLatch = false;
        }

        var previous = _currentSource;
        _currentSource = null;
        _player.Pause();
        _player.Source = null;
        Release(previous);
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
        _player.VolumeChanged -= OnVolumeChanged;
        _player.IsMutedChanged -= OnVolumeChanged;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.PlaybackSession.NaturalDurationChanged -= OnSeekabilityChanged;
        _player.PlaybackSession.SeekableRangesChanged -= OnSeekabilityChanged;
        _player.PlaybackSession.SeekCompleted -= OnSeekCompleted;
        _player.PlaybackSession.PlaybackRateChanged -= OnPlaybackRateChanged;
        _player.Dispose();
        Release(_currentSource);
        _currentSource = null;
    }

    private bool IsCurrentSourceOpen()
    {
        try
        {
            return _currentSource is { IsOpen: true };
        }
        catch
        {
            // The source was released on another thread while it was being inspected.
            return false;
        }
    }

    private static void Release(MediaSource? source)
    {
        try
        {
            source?.Dispose();
        }
        catch
        {
            // Releasing a replaced source is best-effort.
        }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        // While the current source is still opening, an "opened" callback can only
        // belong to the item it replaced.
        if (!IsCurrentSourceOpen())
        {
            return;
        }

        Post(() =>
        {
            MediaOpened?.Invoke(this, EventArgs.Empty);

            // Opened is not playing: report whatever the session really does now.
            if (Map(_player.PlaybackSession.PlaybackState) is CorePlaybackState state &&
                state != CorePlaybackState.Opening)
            {
                SetState(state);
            }
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        if (!IsCurrentSourceOpen())
        {
            return;
        }

        lock (_gate)
        {
            _terminalLatch = true;
        }

        SetState(CorePlaybackState.Ended);
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var message = string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? "Windows 媒体播放器无法打开该视频源。"
            : args.ErrorMessage;
        SetError(message);
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        // Without a loaded item (after Stop) the session's reactions are not playback states.
        if (_currentSource is null || Map(sender.PlaybackState) is not CorePlaybackState mapped)
        {
            return;
        }

        lock (_gate)
        {
            if (mapped == CorePlaybackState.Paused)
            {
                // The session parks in Paused after it ends or fails; keep the real outcome.
                if (_terminalLatch)
                {
                    return;
                }
            }
            else
            {
                _terminalLatch = false;
            }
        }

        SetState(mapped);
    }

    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        // Position ticks are frequent; at most one is queued for the UI thread.
        if (Interlocked.Exchange(ref _positionPending, 1) == 1)
        {
            return;
        }

        var generation = Volatile.Read(ref _loadGeneration);
        Dispatch(() =>
        {
            Volatile.Write(ref _positionPending, 0);
            if (_disposed || generation != Volatile.Read(ref _loadGeneration))
            {
                return;
            }

            PositionChanged?.Invoke(this, new MediaPositionChangedEventArgs(Position));
        });
    }

    private void OnSeekabilityChanged(MediaPlaybackSession sender, object args) =>
        Post(() => SeekabilityChanged?.Invoke(this, EventArgs.Empty));

    private void OnSeekCompleted(MediaPlaybackSession sender, object args) =>
        Post(() => SeekCompleted?.Invoke(this, EventArgs.Empty));

    private void OnVolumeChanged(MediaPlayer sender, object args) =>
        Post(() => VolumeChanged?.Invoke(this, EventArgs.Empty), currentItemOnly: false);

    private void OnPlaybackRateChanged(MediaPlaybackSession sender, object args) =>
        Post(() => PlaybackRateChanged?.Invoke(this, EventArgs.Empty), currentItemOnly: false);

    private static CorePlaybackState? Map(WindowsPlaybackState state) => state switch
    {
        WindowsPlaybackState.Opening => CorePlaybackState.Opening,
        WindowsPlaybackState.Buffering => CorePlaybackState.Buffering,
        WindowsPlaybackState.Playing => CorePlaybackState.Playing,
        WindowsPlaybackState.Paused => CorePlaybackState.Paused,
        _ => null,
    };

    private void SetError(string message)
    {
        lock (_gate)
        {
            _terminalLatch = true;
        }

        SetState(CorePlaybackState.Error);
        Post(() => Error?.Invoke(this, new MediaErrorEventArgs(message)));
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

        Post(() => StateChanged?.Invoke(this, new MediaStateChangedEventArgs(state)));
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread. With
    /// <paramref name="currentItemOnly"/>, the action is dropped if another item
    /// was loaded (or playback stopped) after the engine raised the callback.
    /// </summary>
    private void Post(Action action, bool currentItemOnly = true)
    {
        var generation = Volatile.Read(ref _loadGeneration);
        Dispatch(() =>
        {
            if (_disposed || (currentItemOnly && generation != Volatile.Read(ref _loadGeneration)))
            {
                return;
            }

            action();
        });
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(() => action());
        }
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
