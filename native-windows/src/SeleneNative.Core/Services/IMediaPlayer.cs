namespace SeleneNative.Core.Services;

/// <summary>
/// Abstraction over a media engine so the Core project can stay platform-agnostic
/// and unit-testable. Native hosts bind platform-specific implementations;
/// tests provide a fake.
/// </summary>
/// <remarks>
/// Implementations raise every event on the thread that owns UI state, and only
/// for the item that is currently loaded. Volume, mute and rate may be changed by
/// native transport controls that bypass the view model, so each has a change
/// notification.
/// </remarks>
public interface IMediaPlayer : IDisposable
{
    event EventHandler<MediaStateChangedEventArgs>? StateChanged;
    event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    event EventHandler<MediaErrorEventArgs>? Error;

    /// <summary>
    /// Raised once the loaded item has been opened. An opened item is ready, not
    /// necessarily playing; <see cref="State"/> reports whether it actually plays.
    /// </summary>
    event EventHandler? MediaOpened;

    /// <summary>Raised when <see cref="Length"/> or <see cref="CanSeek"/> changes. Duration may arrive late.</summary>
    event EventHandler? SeekabilityChanged;

    /// <summary>Raised after any seek finishes, including seeks made by native controls.</summary>
    event EventHandler? SeekCompleted;

    /// <summary>Raised when <see cref="Volume"/> or <see cref="IsMuted"/> changes.</summary>
    event EventHandler? VolumeChanged;

    event EventHandler? PlaybackRateChanged;

    MediaPlaybackState State { get; }

    /// <summary>Total duration of the current item, in seconds. 0 if unknown.</summary>
    double Length { get; }

    /// <summary>Current playback position in seconds.</summary>
    double Position { get; set; }

    /// <summary>Whether the engine can actually seek the current item. False until it is opened.</summary>
    bool CanSeek { get; }

    /// <summary>Volume from 0 to 1. Independent of <see cref="IsMuted"/>.</summary>
    double Volume { get; set; }

    bool IsMuted { get; set; }

    /// <summary>Playback speed multiplier; 1 is normal speed. Engines may reset it when an item loads.</summary>
    double PlaybackRate { get; set; }

    /// <summary>Whether the engine can play the current item at <paramref name="rate"/>.</summary>
    bool SupportsPlaybackRate(double rate);

    void Load(string url);
    void Play();
    void Pause();
    void Stop();
}

public enum MediaPlaybackState
{
    Stopped,
    Opening,
    Buffering,
    Playing,
    Paused,
    Ended,
    Error,
}

public sealed class MediaStateChangedEventArgs(MediaPlaybackState state) : EventArgs
{
    public MediaPlaybackState State { get; } = state;
}

public sealed class MediaPositionChangedEventArgs(double position) : EventArgs
{
    public double Position { get; } = position;
}

public sealed class MediaErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
