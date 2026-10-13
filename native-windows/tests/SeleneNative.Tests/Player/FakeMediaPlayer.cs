using SeleneNative.Core.Services;

namespace SeleneNative.Tests.Player;

/// <summary>
/// Engine double that keeps "opened" and "playing" apart the way a real engine
/// does: <see cref="Load"/> only starts opening, and nothing plays, seeks or
/// reports a duration until <see cref="SimulateOpened"/>.
/// </summary>
internal sealed class FakeMediaPlayer : IMediaPlayer
{
    private double _position;
    private double _volume = 1;
    private bool _isMuted;
    private double _playbackRate = 1;

    public event EventHandler<MediaStateChangedEventArgs>? StateChanged;
    public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<MediaErrorEventArgs>? Error;
    public event EventHandler? MediaOpened;
    public event EventHandler? SeekabilityChanged;
    public event EventHandler? SeekCompleted;
    public event EventHandler? VolumeChanged;
    public event EventHandler? PlaybackRateChanged;

    public double LengthSeconds { get; set; }
    public bool Seekable { get; set; } = true;
    public bool IsOpened { get; private set; }
    public bool PlayRequested { get; private set; }
    public HashSet<double> UnsupportedRates { get; } = [];
    public List<string> LoadedUrls { get; } = [];
    public List<double> Seeks { get; } = [];

    public MediaPlaybackState State { get; private set; } = MediaPlaybackState.Stopped;
    public double Length => LengthSeconds;
    public bool CanSeek => IsOpened && Seekable;

    public double Position
    {
        get => _position;
        set
        {
            _position = value;
            Seeks.Add(value);
            SeekCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (_volume == value) return;
            _volume = value;
            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate == value) return;
            _playbackRate = value;
            PlaybackRateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool SupportsPlaybackRate(double rate) => !UnsupportedRates.Contains(rate);

    public void Load(string url)
    {
        LoadedUrls.Add(url);
        IsOpened = false;
        PlayRequested = false;
        _position = 0;

        // Real engines drop back to normal speed when a new item loads.
        PlaybackRate = 1;
        SimulateState(MediaPlaybackState.Opening);
    }

    public void Play()
    {
        PlayRequested = true;
        if (IsOpened)
        {
            SimulateState(MediaPlaybackState.Playing);
        }
    }

    public void Pause()
    {
        PlayRequested = false;
        if (IsOpened)
        {
            SimulateState(MediaPlaybackState.Paused);
        }
    }

    public void Stop()
    {
        IsOpened = false;
        PlayRequested = false;
        SimulateState(MediaPlaybackState.Stopped);
    }

    public void Dispose() { }

    /// <summary>The item finished opening: it starts playing only if play was requested.</summary>
    public void SimulateOpened()
    {
        IsOpened = true;
        MediaOpened?.Invoke(this, EventArgs.Empty);
        SimulateState(PlayRequested ? MediaPlaybackState.Playing : MediaPlaybackState.Paused);
    }

    public void SimulateState(MediaPlaybackState state)
    {
        State = state;
        StateChanged?.Invoke(this, new MediaStateChangedEventArgs(state));
    }

    /// <summary>A playback tick, not a seek.</summary>
    public void SimulatePosition(double position)
    {
        _position = position;
        PositionChanged?.Invoke(this, new MediaPositionChangedEventArgs(position));
    }

    /// <summary>The duration became known after the item was opened.</summary>
    public void SimulateDuration(double seconds)
    {
        LengthSeconds = seconds;
        SeekabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SimulateError(string message)
    {
        State = MediaPlaybackState.Error;
        Error?.Invoke(this, new MediaErrorEventArgs(message));
        StateChanged?.Invoke(this, new MediaStateChangedEventArgs(MediaPlaybackState.Error));
    }

    /// <summary>Playback ran to the end on its own.</summary>
    public void SimulateEnded()
    {
        _position = LengthSeconds;
        SimulateState(MediaPlaybackState.Ended);
    }
}
