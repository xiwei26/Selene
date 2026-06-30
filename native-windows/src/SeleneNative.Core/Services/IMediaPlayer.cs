namespace SeleneNative.Core.Services;

/// <summary>
/// Abstraction over a media engine so the Core project can stay platform-agnostic
/// and unit-testable. The WinUI host binds a LibVLCSharp implementation; tests
/// provide a fake.
/// </summary>
public interface IMediaPlayer : IDisposable
{
    event EventHandler<MediaStateChangedEventArgs>? StateChanged;
    event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
    event EventHandler<MediaErrorEventArgs>? Error;

    MediaPlaybackState State { get; }

    /// <summary>Total duration of the current item, in seconds. 0 if unknown.</summary>
    double Length { get; }

    /// <summary>Current playback position in seconds.</summary>
    double Position { get; set; }

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
