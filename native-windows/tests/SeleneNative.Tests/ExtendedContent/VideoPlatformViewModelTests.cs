using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.ExtendedContent;

public sealed class VideoPlatformViewModelTests
{
    [Fact]
    public async Task LoadInitialAsync_LoadsBilibiliPopularForBilibiliMode()
    {
        var client = new FakeVideoPlatformClient
        {
            BilibiliPopular = new VideoPlatformPage
            {
                Items = [new VideoPlatformItem { Id = "BV1", Title = "Popular" }]
            }
        };
        var vm = new VideoPlatformViewModel(client, VideoPlatformKind.Bilibili);

        await vm.LoadInitialAsync();

        Assert.Single(vm.Items);
        Assert.Equal("Popular", vm.Items[0].Title);
    }

    [Fact]
    public async Task LoadInitialAsync_LoadsYoutubeRegionsAndPopularForYoutubeMode()
    {
        var client = new FakeVideoPlatformClient
        {
            Regions = [new YouTubeRegion { Code = "US", Name = "United States" }],
            YouTubePopular = new VideoPlatformPage
            {
                Items = [new VideoPlatformItem { Id = "yt1", Title = "Trending" }]
            }
        };
        var vm = new VideoPlatformViewModel(client, VideoPlatformKind.YouTube);

        await vm.LoadInitialAsync();

        Assert.Single(vm.Regions);
        Assert.Single(vm.Items);
        Assert.Equal("US", vm.SelectedRegion?.Code);
    }

    [Fact]
    public void TryGetPlayableUrl_ReturnsFirstHttpUrl()
    {
        var vm = new VideoPlatformViewModel(new FakeVideoPlatformClient(), VideoPlatformKind.Bilibili);

        var url = vm.TryGetPlayableUrl(new VideoPlatformItem
        {
            PlayableUrl = "https://video.example/play.m3u8",
            ProxyUrl = "https://video.example/proxy.m3u8",
            Url = "https://video.example/page"
        });

        Assert.Equal("https://video.example/play.m3u8", url);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void TryGetPlayableUrl_ReturnsProxyUrlWhenPlayableUrlIsMissing()
    {
        var vm = new VideoPlatformViewModel(new FakeVideoPlatformClient(), VideoPlatformKind.Bilibili);

        var url = vm.TryGetPlayableUrl(new VideoPlatformItem
        {
            ProxyUrl = "https://video.example/proxy.m3u8",
            Url = "https://video.example/page"
        });

        Assert.Equal("https://video.example/proxy.m3u8", url);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void TryGetPlayableUrl_WhenOnlyPageUrlExists_SetsUnavailableError()
    {
        var vm = new VideoPlatformViewModel(new FakeVideoPlatformClient(), VideoPlatformKind.YouTube);

        var url = vm.TryGetPlayableUrl(new VideoPlatformItem { Url = "https://youtube.com/watch?v=yt1" });

        Assert.Null(url);
        Assert.Equal("当前条目暂无可播放地址", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadInitialAsync_WithSelectedYouTubeRegion_LoadsPopularForSelectedRegion()
    {
        var client = new FakeVideoPlatformClient
        {
            Regions =
            [
                new YouTubeRegion { Code = "US", Name = "United States" },
                new YouTubeRegion { Code = "JP", Name = "Japan" }
            ],
            YouTubePopular = new VideoPlatformPage
            {
                Items = [new VideoPlatformItem { Id = "yt1", Title = "Trending" }]
            }
        };
        var vm = new VideoPlatformViewModel(client, VideoPlatformKind.YouTube);

        await vm.LoadInitialAsync();
        vm.SelectedRegion = vm.Regions.Single(region => region.Code == "JP");
        await vm.LoadInitialAsync();

        Assert.Equal("JP", vm.SelectedRegion?.Code);
        Assert.Equal("JP", client.LastYouTubePopularRegion);
    }

    private sealed class FakeVideoPlatformClient : IVideoPlatformClient
    {
        public VideoPlatformPage BilibiliPopular { get; init; } = new();
        public VideoPlatformPage YouTubePopular { get; init; } = new();
        public IReadOnlyList<YouTubeRegion> Regions { get; init; } = [];
        public string? LastYouTubePopularRegion { get; private set; }

        public Task<VideoPlatformPage> LoadBilibiliPopularAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BilibiliPopular);
        }

        public Task<VideoPlatformPage> SearchBilibiliAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BilibiliPopular);
        }

        public Task<VideoPlatformPage> LoadYouTubePopularAsync(string regionCode = "US", string? pageToken = null, CancellationToken cancellationToken = default)
        {
            LastYouTubePopularRegion = regionCode;
            return Task.FromResult(YouTubePopular);
        }

        public Task<VideoPlatformPage> SearchYouTubeAsync(string query, string contentType = "all", string order = "relevance", int maxResults = 25, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(YouTubePopular);
        }

        public Task<IReadOnlyList<YouTubeRegion>> LoadYouTubeRegionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Regions);
        }
    }
}
