using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.ExtendedContent;

public sealed class ShortDramaViewModelTests
{
    [Fact]
    public async Task LoadInitialAsync_LoadsCategoriesAndRecommendedItems()
    {
        var client = new FakeShortDramaClient
        {
            Categories = [new ShortDramaCategory { Id = "1", Name = "Urban" }],
            Recommended = new ShortDramaListResult
            {
                Items = [new ShortDramaItem { Id = "s1", Name = "Short One", Cover = "c.jpg" }],
                Total = 1
            }
        };
        var vm = new ShortDramaViewModel(client);

        await vm.LoadInitialAsync();

        Assert.False(vm.IsLoading);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Categories);
        Assert.Single(vm.Items);
        Assert.Equal("Short One", vm.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_UsesQueryAndReplacesItems()
    {
        var client = new FakeShortDramaClient
        {
            SearchResult = new ShortDramaListResult
            {
                Items = [new ShortDramaItem { Id = "s2", Name = "Found" }]
            }
        };
        var vm = new ShortDramaViewModel(client) { SearchQuery = "hero" };

        await vm.SearchAsync();

        Assert.Equal("hero", client.LastSearchQuery);
        Assert.Single(vm.Items);
        Assert.Equal("Found", vm.Items[0].Name);
    }

    [Fact]
    public async Task PlayEpisodeAsync_ParsesSelectedEpisodeAndRaisesPlayRequested()
    {
        var client = new FakeShortDramaClient
        {
            Detail = new ShortDramaDetail
            {
                Id = "s1",
                Name = "Short One",
                Episodes =
                [
                    new ShortDramaEpisode { Episode = 1, Title = "Episode 1" },
                    new ShortDramaEpisode { Episode = 2, Title = "Episode 2" }
                ]
            },
            ParseResult = new ShortDramaParseResult { ParsedUrl = "https://video.example/2.m3u8" }
        };
        var vm = new ShortDramaViewModel(client);
        string? playedUrl = null;
        vm.PlayRequested += url => playedUrl = url;

        var item = new ShortDramaItem { Id = "s1", Name = "Short One" };
        await vm.LoadDetailAsync(item);
        vm.SelectedEpisodeNumber = 2;
        await vm.PlayEpisodeAsync(item);

        Assert.Equal("s1", client.LastDetailId);
        Assert.Equal(2, client.LastParsedEpisode);
        Assert.Equal("https://video.example/2.m3u8", playedUrl);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task PlayEpisodeAsync_WhenParseHasNoUrl_SetsErrorWithoutPlaying()
    {
        var vm = new ShortDramaViewModel(new FakeShortDramaClient
        {
            ParseResult = new ShortDramaParseResult()
        });
        var playCount = 0;
        vm.PlayRequested += _ => playCount++;

        await vm.PlayEpisodeAsync(new ShortDramaItem { Id = "s1", Name = "Short One" }, episode: 1);

        Assert.Equal(0, playCount);
        Assert.NotNull(vm.ErrorMessage);
    }

    private sealed class FakeShortDramaClient : IShortDramaClient
    {
        public IReadOnlyList<ShortDramaCategory> Categories { get; init; } = [];
        public ShortDramaListResult Recommended { get; init; } = new();
        public ShortDramaListResult SearchResult { get; init; } = new();
        public ShortDramaDetail? Detail { get; init; }
        public ShortDramaParseResult? ParseResult { get; init; }
        public string? LastSearchQuery { get; private set; }
        public string? LastDetailId { get; private set; }
        public int? LastParsedEpisode { get; private set; }

        public Task<IReadOnlyList<ShortDramaCategory>> LoadShortDramaCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Categories);
        }

        public Task<ShortDramaListResult> LoadShortDramaRecommendAsync(string? category = null, int size = 24, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Recommended);
        }

        public Task<ShortDramaListResult> LoadShortDramaListAsync(string categoryId, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Recommended);
        }

        public Task<ShortDramaListResult> SearchAsync(string query, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default)
        {
            LastSearchQuery = query;
            return Task.FromResult(SearchResult);
        }

        public Task<ShortDramaDetail?> LoadDetailAsync(string id, string? name = null, CancellationToken cancellationToken = default)
        {
            LastDetailId = id;
            return Task.FromResult(Detail);
        }

        public Task<ShortDramaParseResult?> ParseAsync(string id, int episode, string? name = null, CancellationToken cancellationToken = default)
        {
            LastParsedEpisode = episode;
            return Task.FromResult(ParseResult);
        }
    }
}
