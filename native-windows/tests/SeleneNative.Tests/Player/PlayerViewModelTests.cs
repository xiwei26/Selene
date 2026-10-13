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
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateOpened();
        player.SimulatePosition(42.0);

        vm.Stop();

        Assert.Null(vm.CurrentEpisodeUrl);
        Assert.Equal(0, vm.PlayTime);
        Assert.Equal(0, vm.TotalTime);
        Assert.Null(vm.CurrentResult);
        Assert.Null(vm.PlaybackError);
        Assert.False(vm.IsLoading);
        Assert.False(vm.CanSeek);
        Assert.Equal(MediaPlaybackState.Stopped, vm.PlaybackState);
    }

    [Fact]
    public void PendingSeekTime_ShouldBeConsumedOncePlaybackIsReady()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.PendingSeekTime = 60;
        vm.Play();

        player.SimulateOpened();

        Assert.Null(vm.PendingSeekTime);
        Assert.Equal(60, player.Position, 1);
    }

    [Fact]
    public void SeekTo_ShouldClampAndUpdatePlayerPosition()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateOpened();

        Assert.True(vm.SeekTo(180));
        Assert.Equal(120, player.Position, 1);
        Assert.Equal(120, vm.PlayTime, 1);
        Assert.Equal(120, vm.TotalTime, 1);

        Assert.True(vm.SeekTo(-30));
        Assert.Equal(0, player.Position, 1);
        Assert.Equal(0, vm.PlayTime, 1);
    }

    [Fact]
    public void PositionChanged_ShouldRefreshTotalTime_WhenLengthArrivesLate()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateOpened();

        player.LengthSeconds = 120;
        player.SimulatePosition(42);

        Assert.Equal(42, vm.PlayTime, 1);
        Assert.Equal(120, vm.TotalTime, 1);
    }

    [Fact]
    public void Retry_ShouldReloadAndPlay()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateError("fail");
        Assert.Equal("fail", vm.PlaybackError);

        vm.Retry();
        player.SimulateOpened();

        Assert.Null(vm.PlaybackError);
        Assert.Equal(2, player.LoadedUrls.Count);
        Assert.Equal(MediaPlaybackState.Playing, player.State);
    }

    [Fact]
    public void TogglePlayPause_ShouldPause_WhenCurrentlyPlaying()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateOpened();

        vm.TogglePlayPause();

        Assert.Equal(MediaPlaybackState.Paused, player.State);
        Assert.False(vm.IsPlaybackActive);
    }

    [Fact]
    public void TogglePlayPause_ShouldPlay_WhenCurrentlyPaused()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        vm.Play();
        player.SimulateOpened();
        vm.Pause();

        vm.TogglePlayPause();

        Assert.Equal(MediaPlaybackState.Playing, player.State);
        Assert.True(vm.IsPlaybackActive);
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
        vm.Play();
        player.SimulateOpened();
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
    public async Task LoadDetailAndPlayAsync_ShouldRequestOriginalItemId()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        var provider = new StubContentProvider
        {
            OnDetail = (source, id) => Task.FromResult<SearchResult?>(new SearchResult
            {
                Id = id,
                Title = "Video",
                Source = source,
                SourceName = "Source A",
                Episodes = [$"https://example.com/{id}/ep1.m3u8", $"https://example.com/{id}/ep2.m3u8"],
            }),
        };
        var record = new PlayRecord
        {
            Id = "source-a+video-1",
            Source = "source-a",
            Title = "Video",
            SourceName = "Source A",
            EpisodeNumber = 1,
            PlayTime = 30,
            TotalTime = 120,
            SearchTitle = "Video"
        };

        await vm.LoadDetailAndPlayAsync(record, provider);
        player.SimulateOpened();

        Assert.Equal(("source-a", "video-1"), Assert.Single(provider.DetailRequests));
        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Equal("https://example.com/video-1/ep1.m3u8", vm.CurrentEpisodeUrl);
        Assert.Equal(30, player.Position, 1);
        Assert.Equal(MediaPlaybackState.Playing, player.State);
    }

    [Fact]
    public async Task LoadDetailAndPlayAsync_ShouldPreserveEpisodeOrder_WhenUrlsContainQualityMarkers()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        var provider = new StubContentProvider
        {
            OnDetail = (_, _) => Task.FromResult<SearchResult?>(new SearchResult
            {
                Id = "video-1",
                Title = "Video",
                Source = "source-a",
                SourceName = "Source A",
                Episodes =
                [
                    "https://example.com/video-1/ep1-480.m3u8",
                    "https://example.com/video-1/ep2-2160.m3u8"
                ],
            }),
        };
        var record = new PlayRecord
        {
            Id = "source-a+video-1",
            Source = "source-a",
            Title = "Video",
            SourceName = "Source A",
            EpisodeNumber = 1,
            SearchTitle = "Video"
        };

        await vm.LoadDetailAndPlayAsync(record, provider);

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Equal("https://example.com/video-1/ep1-480.m3u8", vm.CurrentEpisodeUrl);
    }

    [Fact]
    public async Task LoadDetailAndPlayAsync_ShouldNotPlayDifferentSearchResult_WhenOriginalRecordIsMissing()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        var provider = new StubContentProvider
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<SearchResult>>(
            [
                new SearchResult
                {
                    Id = "other-video",
                    Title = "Super Girl",
                    Source = "other-source",
                    SourceName = "Other Source",
                    Episodes = ["https://example.com/other/ep1.m3u8"],
                }
            ]),
        };
        var record = new PlayRecord
        {
            Id = "source-a+cang-yuan-tu",
            Source = "source-a",
            Title = "沧元图",
            SourceName = "Source A",
            EpisodeNumber = 1,
            SearchTitle = "沧元图"
        };

        await vm.LoadDetailAndPlayAsync(record, provider);

        Assert.Null(vm.CurrentResult);
        Assert.Null(vm.CurrentEpisodeUrl);
        Assert.NotNull(vm.PlaybackError);
        Assert.False(vm.IsLoading);
        Assert.Empty(player.LoadedUrls);
    }

    [Fact]
    public async Task LoadDetailAndPlayAsync_ShouldPlaySameTitleSearchResult_WhenStoredSourceIdNoLongerMatches()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        var provider = new StubContentProvider
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<SearchResult>>(
            [
                new SearchResult
                {
                    Id = "new-cang-yuan-tu",
                    Title = "沧元图",
                    Source = "new-source",
                    SourceName = "TV-360资源",
                    Episodes =
                    [
                        "https://example.com/cang/ep1.m3u8",
                        "https://example.com/cang/ep2.m3u8"
                    ],
                }
            ]),
        };
        var record = new PlayRecord
        {
            Id = "old-source+old-cang-yuan-tu",
            Source = "old-source",
            Title = "沧元图",
            SourceName = "TV-360资源",
            EpisodeNumber = 2,
            SearchTitle = "沧元图"
        };

        await vm.LoadDetailAndPlayAsync(record, provider);

        Assert.Equal("沧元图", vm.CurrentResult?.Title);
        Assert.Equal(1, vm.CurrentEpisodeIndex);
        Assert.Equal("https://example.com/cang/ep2.m3u8", vm.CurrentEpisodeUrl);
        Assert.Null(vm.PlaybackError);
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
}
