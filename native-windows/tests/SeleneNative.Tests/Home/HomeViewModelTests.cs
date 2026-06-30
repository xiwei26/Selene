using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Home;

public sealed class HomeViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShouldAggregateAllHomeSections()
    {
        var viewModel = new HomeViewModel(
            new StubDoubanClient(
                movies: [NewMovie("Movie")],
                tvShows: [NewMovie("TV")],
                shows: [NewMovie("Show")]),
            new StubBangumiClient([NewBangumi("Anime")]),
            new StubPlayRecordStore([NewRecord("Continue")]));

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Continue", Assert.Single(viewModel.PlayRecords).Title);
        Assert.Equal("Movie", Assert.Single(viewModel.HotMovies).Title);
        Assert.Equal("TV", Assert.Single(viewModel.HotTvShows).Title);
        Assert.Equal("Anime", Assert.Single(viewModel.TodayBangumi).DisplayTitle);
        Assert.Equal("Show", Assert.Single(viewModel.HotShows).Title);
    }

    [Fact]
    public async Task LoadAsync_WithNoRemoteContent_ShouldExposeEmptyState()
    {
        var viewModel = new HomeViewModel(
            new StubDoubanClient([], [], []),
            new StubBangumiClient([]),
            new StubPlayRecordStore([]));

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Equal("首页暂时没有加载到内容", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_WithOnlyPlayRecords_ShouldNotExposeEmptyState()
    {
        var viewModel = new HomeViewModel(
            new StubDoubanClient([], [], []),
            new StubBangumiClient([]),
            new StubPlayRecordStore([NewRecord("Continue")]));

        await viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Continue", Assert.Single(viewModel.PlayRecords).Title);
    }

    [Fact]
    public async Task LoadAsync_WithProvider_ShouldUseRemotePlayRecords()
    {
        var viewModel = new HomeViewModel(
            new StubDoubanClient([], [], []),
            new StubBangumiClient([]),
            new StubPlayRecordStore([NewRecord("Local")]));

        await viewModel.LoadAsync(new StubContentProvider([NewRecord("Remote")]));

        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Remote", Assert.Single(viewModel.PlayRecords).Title);
    }

    private static DoubanMovie NewMovie(string title)
    {
        return new DoubanMovie
        {
            Id = title,
            Title = title,
            Poster = "https://img.example/poster.jpg",
            Rate = "8.0",
            Year = "2026"
        };
    }

    private static BangumiItem NewBangumi(string title)
    {
        return new BangumiItem
        {
            Id = 1,
            Name = title,
            NameCn = title,
            Images = new BangumiImages { Large = "https://img.example/bangumi.jpg" },
            Rating = new BangumiRating { Score = 7.5 }
        };
    }

    private static PlayRecord NewRecord(string title)
    {
        return new PlayRecord
        {
            Id = title,
            Title = title,
            Source = "demo",
            SourceName = "Demo",
            EpisodeNumber = 1,
            PlayTime = 10,
            TotalTime = 20
        };
    }

    private sealed class StubDoubanClient(
        IReadOnlyList<DoubanMovie> movies,
        IReadOnlyList<DoubanMovie> tvShows,
        IReadOnlyList<DoubanMovie> shows) : IDoubanClient
    {
        public Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(movies);
        }

        public Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(tvShows);
        }

        public Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(shows);
        }

        public Task<DoubanMovie?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DoubanMovie?>(null);
        }
    }

    private sealed class StubBangumiClient(IReadOnlyList<BangumiItem> items) : IBangumiClient
    {
        public Task<IReadOnlyList<BangumiItem>> GetTodayCalendarAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<BangumiItem>> GetCalendarByWeekdayAsync(
            int weekday,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items);
        }
    }

    private sealed class StubPlayRecordStore(IReadOnlyList<PlayRecord> records) : IPlayRecordStore
    {
        public Task<IReadOnlyList<PlayRecord>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(records);
        }

        public Task SaveAsync(PlayRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveAllAsync(IEnumerable<PlayRecord> records, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubContentProvider(IReadOnlyList<PlayRecord> records) : IContentProvider
    {
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
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        }

        public Task<SearchResult?> DetailAsync(
            string source,
            string id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SearchResult?>(null);
        }

        public Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SearchResource>>([]);
        }

        public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(
            CancellationToken cancellationToken = default)
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

        public Task RemoveFavoriteAsync(
            string source,
            string id,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(records);
        }

        public Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeletePlayRecordAsync(
            string source,
            string id,
            CancellationToken cancellationToken = default)
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
