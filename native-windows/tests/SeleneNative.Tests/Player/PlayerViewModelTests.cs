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
    public void SeekTo_ShouldClampAndUpdatePlayerPosition()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        player.SimulateState(MediaPlaybackState.Playing);

        vm.SeekTo(180);

        Assert.Equal(120, player.Position, 1);
        Assert.Equal(120, vm.PlayTime, 1);
        Assert.Equal(120, vm.TotalTime, 1);
    }

    [Fact]
    public void PositionChanged_ShouldRefreshTotalTime_WhenLengthArrivesLate()
    {
        var player = new FakeMediaPlayer();
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/video.m3u8", NewResult("Test", "src1"), 0);
        player.SimulateState(MediaPlaybackState.Playing);

        player.LengthSeconds = 120;
        player.SimulatePosition(42);

        Assert.Equal(42, vm.PlayTime, 1);
        Assert.Equal(120, vm.TotalTime, 1);
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
    public async Task LoadDetailAndPlayAsync_ShouldRequestOriginalItemId()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 120 };
        var vm = new PlayerViewModel(() => player);
        var provider = new CapturingContentProvider();
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

        Assert.Equal("source-a", provider.RequestedSource);
        Assert.Equal("video-1", provider.RequestedId);
        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Equal("https://example.com/video-1/ep1.m3u8", vm.CurrentEpisodeUrl);
        Assert.Equal(30, player.Position, 1);
    }

    [Fact]
    public async Task LoadDetailAndPlayAsync_ShouldPreserveEpisodeOrder_WhenUrlsContainQualityMarkers()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        var provider = new CapturingContentProvider
        {
            DetailResult = new SearchResult
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
            }
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
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        var provider = new CapturingContentProvider
        {
            DetailResult = null,
            SearchResults =
            [
                new SearchResult
                {
                    Id = "other-video",
                    Title = "Super Girl",
                    Source = "other-source",
                    SourceName = "Other Source",
                    Episodes = ["https://example.com/other/ep1.m3u8"],
                }
            ]
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
    }

    [Fact]
    public async Task LoadDetailAndPlayAsync_ShouldPlaySameTitleSearchResult_WhenStoredSourceIdNoLongerMatches()
    {
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());
        var provider = new CapturingContentProvider
        {
            DetailResult = null,
            SearchResults =
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
            ]
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

    private sealed class CapturingContentProvider : IContentProvider
    {
        public string? RequestedSource { get; private set; }
        public string? RequestedId { get; private set; }
        public SearchResult? DetailResult { get; init; }
        public IReadOnlyList<SearchResult> SearchResults { get; init; } = [];

        public Task<LoginSession> LoginAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SearchResults);
        }

        public Task<SearchResult?> DetailAsync(
            string source,
            string id,
            CancellationToken cancellationToken = default)
        {
            RequestedSource = source;
            RequestedId = id;
            if (DetailResult is not null || SearchResults.Count > 0)
            {
                return Task.FromResult(DetailResult);
            }

            var result = new SearchResult
            {
                Id = id,
                Title = "Video",
                Source = source,
                SourceName = "Source A",
                Episodes =
                [
                    $"https://example.com/{id}/ep1.m3u8",
                    $"https://example.com/{id}/ep2.m3u8"
                ],
            };
            return Task.FromResult<SearchResult?>(result);
        }

        public Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SearchResource>>([]);
        }

        public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FavoriteItem>>([]);
        }

        public Task AddFavoriteAsync(
            string source,
            string id,
            Dictionary<string, object> data,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFavoriteAsync(string source, string id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlayRecord>>([]);
        }

        public Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeletePlayRecordAsync(string source, string id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearPlayRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetSearchHistoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public Task AddSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SearchSuggestion>> SearchSuggestionsAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);
        }

        public Task<IReadOnlyList<LiveSource>> GetLiveSourcesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LiveSource>>([]);
        }

        public Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(
            string sourceKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LiveChannel>>([]);
        }

        public Task<EpgData?> GetLiveEpgAsync(
            string tvgId,
            string sourceKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<EpgData?>(null);
        }
    }
}
