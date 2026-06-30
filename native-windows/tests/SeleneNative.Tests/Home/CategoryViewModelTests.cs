using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Home;

public sealed class CategoryViewModelTests
{
    [Fact]
    public async Task LoadMoviesAsync_ShouldUseRequestedNavigationCategory()
    {
        var viewModel = new CategoryViewModel();

        await viewModel.LoadAnimeAsync(new StubBangumiClient([NewBangumi()]), reset: true);
        await viewModel.LoadMoviesAsync(new StubDoubanClient(), "tv", reset: true);

        Assert.Equal("tv", viewModel.CategoryKind);
        Assert.Empty(viewModel.AnimeItems);
        Assert.Equal("TV", Assert.Single(viewModel.MovieItems).Title);
    }

    [Fact]
    public async Task LoadAnimeAsync_ShouldUseAnimeCategory()
    {
        var viewModel = new CategoryViewModel();

        await viewModel.LoadMoviesAsync(new StubDoubanClient(), "shows", reset: true);
        await viewModel.LoadAnimeAsync(new StubBangumiClient([NewBangumi()]), reset: true);

        Assert.Equal("anime", viewModel.CategoryKind);
        Assert.Empty(viewModel.MovieItems);
        Assert.Equal("Anime", Assert.Single(viewModel.AnimeItems).DisplayTitle);
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

    private static BangumiItem NewBangumi()
    {
        return new BangumiItem
        {
            Id = 1,
            Name = "Anime",
            NameCn = "Anime",
            Images = new BangumiImages { Large = "https://img.example/bangumi.jpg" },
            Rating = new BangumiRating { Score = 7.5 }
        };
    }

    private sealed class StubDoubanClient : IDoubanClient
    {
        public Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoubanMovie>>([NewMovie("Movie")]);
        }

        public Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoubanMovie>>([NewMovie("TV")]);
        }

        public Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoubanMovie>>([NewMovie("Show")]);
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
}
