using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

public sealed class PlayerViewModelTests
{
    [Fact]
    public void Stop_ShouldResetAllState()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateState(MediaPlaybackState.Playing);
        player.SimulatePosition(42.0);

        vm.Stop();

        Assert.Null(vm.CurrentEpisodeUrl);
        Assert.Equal(0, vm.PlayTime);
        Assert.Equal(0, vm.TotalTime);
        Assert.Null(vm.CurrentResult);
        Assert.Null(vm.PlaybackError);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void PendingSeekTime_ShouldBeConsumedOnPlaying()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.PendingSeekTime = 60;

        player.SimulateState(MediaPlaybackState.Playing);

        Assert.Null(vm.PendingSeekTime);
        Assert.Equal(60, player.Position, 1);
    }

    [Fact]
    public void Retry_ShouldReloadAndPlay()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        player.SimulateState(MediaPlaybackState.Error);
        vm.PlaybackError = "fail";

        vm.Retry();

        Assert.Null(vm.PlaybackError);
        Assert.Equal(MediaPlaybackState.Playing, player.State);
    }

    [Fact]
    public void MakePlayRecord_ShouldReturnNull_WhenNoResult()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        Assert.Null(vm.MakePlayRecord());
    }

    [Fact]
    public void MakePlayRecord_ShouldIncludeProgress()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 100 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Title", "src"), 2);
        player.SimulateState(MediaPlaybackState.Playing);
        player.SimulatePosition(30.0);

        var record = vm.MakePlayRecord();

        Assert.NotNull(record);
        Assert.Equal("Title", record.Title);
        Assert.Equal("src", record.Source);
        Assert.Equal(3, record.EpisodeNumber);
        Assert.Equal(30, record.PlayTime, 1);
        Assert.Equal(100, record.TotalTime, 1);
    }

    [Fact]
    public void ToggleEpisodeOrder_ShouldFlip()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        Assert.False(vm.IsEpisodeReversed);
        vm.ToggleEpisodeOrder();
        Assert.True(vm.IsEpisodeReversed);
    }

    private static SearchResult NewResult(string title, string source, int episodeCount = 5)
    {
        var episodes = Enumerable.Range(0, episodeCount)
            .Select(i => $"https://example.com/{source}/ep{i + 1}.m3u8")
            .ToList();
        return new SearchResult
        {
            Id = "1",
            Title = title,
            Source = source,
            SourceName = source,
            Episodes = episodes,
        };
    }

    private sealed class FakeMediaPlayer : IMediaPlayer
    {
        public double LengthSeconds { get; set; }
        public double Length => LengthSeconds;
        public double Position { get; set; }
        public MediaPlaybackState State { get; private set; } = MediaPlaybackState.Stopped;

        public event EventHandler<MediaStateChangedEventArgs>? StateChanged;
        public event EventHandler<MediaPositionChangedEventArgs>? PositionChanged;
        public event EventHandler<MediaErrorEventArgs>? Error;

        public void Load(string url) { }
        public void Play() => SimulateState(MediaPlaybackState.Playing);
        public void Pause() => SimulateState(MediaPlaybackState.Paused);
        public void Stop() => SimulateState(MediaPlaybackState.Stopped);
        public void Dispose() { }

        public void SimulateState(MediaPlaybackState state)
        {
            State = state;
            StateChanged?.Invoke(this, new MediaStateChangedEventArgs(state));
        }

        public void SimulatePosition(double position)
        {
            Position = position;
            PositionChanged?.Invoke(this, new MediaPositionChangedEventArgs(position));
        }
    }
}
