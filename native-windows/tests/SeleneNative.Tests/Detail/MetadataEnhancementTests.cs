using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Detail;

public sealed class MetadataEnhancementTests
{
    [Fact]
    public async Task LoadAsync_WithDoubanId_LoadsOptionalEnhancementsWithoutBlockingBaseDetail()
    {
        var metadata = new FakeMetadataEnhancementClient
        {
            Backdrop = new TmdbBackdropResult { BackdropUrl = "https://img.example/backdrop.jpg" },
            Comments = [new DoubanComment { Username = "u", Content = "good" }],
            Recommends = [new DoubanMovie { Id = "r1", Title = "Related", Poster = "", Year = "2026" }],
            QuickInfo = new DoubanQuickInfo { Summary = "quick" },
            Trailer = new TrailerRefreshResult { TrailerUrl = "https://video.example/trailer.mp4" }
        };
        var vm = new DetailViewModel();
        var seed = new SearchResult
        {
            Id = "id1",
            Source = "src",
            Title = "Title",
            Year = "2026",
            DoubanId = 1292052,
            Episodes = ["https://video.example/1.m3u8"]
        };

        await vm.LoadAsync(seed, provider: null, doubanClient: null, metadataClient: metadata);

        Assert.Same(seed, vm.Result);
        Assert.Single(vm.Episodes);
        Assert.Equal("https://img.example/backdrop.jpg", vm.TmdbBackdrop?.BackdropUrl);
        Assert.Single(vm.DoubanComments);
        Assert.Single(vm.DoubanRecommendations);
        Assert.Equal("quick", vm.DoubanQuickInfo?.Summary);
        Assert.Equal("https://video.example/trailer.mp4", vm.TrailerRefresh?.TrailerUrl);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_WhenMetadataFails_KeepsBaseDetailAndNoPageError()
    {
        var vm = new DetailViewModel();
        var seed = new SearchResult
        {
            Id = "id1",
            Source = "src",
            Title = "Title",
            Year = "2026",
            DoubanId = 1292052,
            Episodes = ["https://video.example/1.m3u8"]
        };

        await vm.LoadAsync(seed, provider: null, doubanClient: null, metadataClient: new FailingMetadataEnhancementClient());

        Assert.Same(seed, vm.Result);
        Assert.Single(vm.Episodes);
        Assert.Null(vm.TmdbBackdrop);
        Assert.Empty(vm.DoubanComments);
        Assert.Empty(vm.DoubanRecommendations);
        Assert.Null(vm.ErrorMessage);
    }

    private sealed class FakeMetadataEnhancementClient : IMetadataEnhancementClient
    {
        public TmdbBackdropResult? Backdrop { get; init; }
        public IReadOnlyList<DoubanComment> Comments { get; init; } = [];
        public IReadOnlyList<DoubanMovie> Recommends { get; init; } = [];
        public DoubanQuickInfo? QuickInfo { get; init; }
        public TrailerRefreshResult? Trailer { get; init; }

        public Task<TmdbBackdropResult?> LoadBackdropAsync(string title, string? originalTitle, string? year, string? sourceType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Backdrop);
        }

        public Task<TmdbActorResult?> LoadActorAsync(string actor, string type = "movie", int limit = 20, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TmdbActorResult?>(null);
        }

        public Task<IReadOnlyList<DoubanComment>> LoadDoubanCommentsAsync(string id, int start = 0, int limit = 10, string sort = "new_score", CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Comments);
        }

        public Task<IReadOnlyList<DoubanMovie>> LoadDoubanRecommendsAsync(string kind, int limit = 20, int start = 0, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Recommends);
        }

        public Task<DoubanQuickInfo?> LoadDoubanQuickInfoAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(QuickInfo);
        }

        public Task<IReadOnlyList<DoubanSuggestItem>> SuggestDoubanAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoubanSuggestItem>>([]);
        }

        public Task<IReadOnlyList<DoubanCelebrityWork>> LoadCelebrityWorksAsync(string name, int limit = 20, string mode = "search", CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DoubanCelebrityWork>>([]);
        }

        public Task<TrailerRefreshResult?> RefreshTrailerAsync(string id, bool force = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Trailer);
        }
    }

    private sealed class FailingMetadataEnhancementClient : IMetadataEnhancementClient
    {
        public Task<TmdbBackdropResult?> LoadBackdropAsync(string title, string? originalTitle, string? year, string? sourceType, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<TmdbActorResult?> LoadActorAsync(string actor, string type = "movie", int limit = 20, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<IReadOnlyList<DoubanComment>> LoadDoubanCommentsAsync(string id, int start = 0, int limit = 10, string sort = "new_score", CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<IReadOnlyList<DoubanMovie>> LoadDoubanRecommendsAsync(string kind, int limit = 20, int start = 0, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<DoubanQuickInfo?> LoadDoubanQuickInfoAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<IReadOnlyList<DoubanSuggestItem>> SuggestDoubanAsync(string query, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<IReadOnlyList<DoubanCelebrityWork>> LoadCelebrityWorksAsync(string name, int limit = 20, string mode = "search", CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }

        public Task<TrailerRefreshResult?> RefreshTrailerAsync(string id, bool force = false, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("metadata failed");
        }
    }
}
