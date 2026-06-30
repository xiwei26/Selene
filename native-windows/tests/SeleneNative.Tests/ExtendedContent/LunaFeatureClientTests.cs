using System.Net;
using System.Text;
using System.Text.Json;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.ExtendedContent;

public sealed class LunaFeatureClientTests
{
    [Fact]
    public async Task ShortDramaClient_SearchAsync_RequestsExpectedPathAndCookie()
    {
        using var handler = new RecordingHandler("""{"data":{"list":[],"total":0}}""");
        var client = new ShortDramaClient("http://server.test", "sid=abc", new HttpClient(handler));

        await client.SearchAsync("hero", page: 2, pageSize: 24);

        Assert.Equal("http://server.test/api/shortdrama/search?query=hero&page=2&size=24", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sid=abc", handler.Request.Headers.GetValues("Cookie").Single());
    }

    [Fact]
    public async Task VideoPlatformClient_LoadYouTubePopularAsync_RequestsRegionAndToken()
    {
        using var handler = new RecordingHandler("""{"items":[],"nextPageToken":"n2"}""");
        var client = new VideoPlatformClient("http://server.test", "sid=abc", new HttpClient(handler));

        await client.LoadYouTubePopularAsync("JP", "p1");

        Assert.Equal("http://server.test/api/youtube/popular?regionCode=JP&pageToken=p1", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task VideoPlatformClient_LoadBilibiliPopularAsync_UsesPnPsAndDecodesVideos()
    {
        using var handler = new RecordingHandler("""{"videos":[{"bvid":"BV1","title":"Popular","pic":"p.jpg","play":12345,"pubdate":1710000000}]}""");
        var client = new VideoPlatformClient("http://server.test", httpClient: new HttpClient(handler));

        var page = await client.LoadBilibiliPopularAsync(page: 3, pageSize: 20);

        Assert.Equal("http://server.test/api/bilibili/popular?pn=3&ps=20", handler.Request!.RequestUri!.ToString());
        Assert.Equal("BV1", page.Items.Single().Id);
        Assert.Equal("p.jpg", page.Items.Single().Thumbnail);
        Assert.Equal("12345", page.Items.Single().Views);
        Assert.Equal("1710000000", page.Items.Single().PublishedAt);
    }

    [Fact]
    public async Task VideoPlatformClient_SearchBilibiliAsync_UsesQParameter()
    {
        using var handler = new RecordingHandler("""{"videos":[]}""");
        var client = new VideoPlatformClient("http://server.test", httpClient: new HttpClient(handler));

        await client.SearchBilibiliAsync("music");

        Assert.Equal("http://server.test/api/bilibili/search?q=music", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task VideoPlatformClient_SearchYouTubeAsync_UsesQAndDecodesRawApiItems()
    {
        using var handler = new RecordingHandler(
            """
            {"videos":[{"id":{"videoId":"yt1","kind":"youtube#video"},"snippet":{"title":"Trailer","description":"Desc","channelTitle":"Studio","publishedAt":"2026-01-02T00:00:00Z","thumbnails":{"default":{"url":"small.jpg"},"high":{"url":"large.jpg"}}}}],"nextPageToken":"n2"}
            """);
        var client = new VideoPlatformClient("http://server.test", httpClient: new HttpClient(handler));

        var page = await client.SearchYouTubeAsync("trailers", contentType: "video", order: "date", maxResults: 10);

        Assert.Equal("http://server.test/api/youtube/search?q=trailers&contentType=video&order=date&maxResults=10", handler.Request!.RequestUri!.ToString());
        var item = Assert.Single(page.Items);
        Assert.Equal("yt1", item.Id);
        Assert.Equal("Trailer", item.Title);
        Assert.Equal("large.jpg", item.Thumbnail);
        Assert.Equal("Studio", item.Author);
        Assert.Equal("2026-01-02T00:00:00Z", item.PublishedAt);
        Assert.Equal("n2", page.NextPageToken);
    }

    [Fact]
    public void VideoPlatformItem_DeserializesBackwardCompatibleTopLevelShape()
    {
        var json = """{"items":[{"id":"legacy","title":"Legacy","cover":"cover.jpg","thumbnail":"thumb.jpg","views":"99"}]}""";

        var page = JsonSerializer.Deserialize<VideoPlatformPage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var item = Assert.Single(page!.Items);
        Assert.Equal("legacy", item.Id);
        Assert.Equal("Legacy", item.Title);
        Assert.Equal("cover.jpg", item.Cover);
        Assert.Equal("thumb.jpg", item.Thumbnail);
        Assert.Equal("99", item.Views);
    }

    [Fact]
    public async Task ShortDramaClient_LoadShortDramaListAsync_UsesCategoryIdParameter()
    {
        using var handler = new RecordingHandler("""{"list":[]}""");
        var client = new ShortDramaClient("http://server.test", httpClient: new HttpClient(handler));

        await client.LoadShortDramaListAsync("12", page: 2, pageSize: 24);

        Assert.Equal("http://server.test/api/shortdrama/list?categoryId=12&page=2&size=24", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task MetadataEnhancementClient_LoadBackdropAsync_UsesLunaTvParameterNames()
    {
        using var handler = new RecordingHandler("""{"data":{"backdropUrl":"b.jpg"}}""");
        var client = new MetadataEnhancementClient("http://server.test", httpClient: new HttpClient(handler));

        await client.LoadBackdropAsync("Title", originalTitle: "Original", year: "2026", sourceType: "movie");

        Assert.Equal("http://server.test/api/tmdb/backdrop?title=Title&original_title=Original&year=2026&stype=movie", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task MetadataEnhancementClient_LoadDoubanCommentsAsync_RequestsIdStartLimitSort()
    {
        using var handler = new RecordingHandler("""{"code":200,"data":{"comments":[],"start":0,"limit":10,"total":0}}""");
        var client = new MetadataEnhancementClient("http://server.test", "sid=abc", new HttpClient(handler));

        await client.LoadDoubanCommentsAsync("1292052", start: 0, limit: 10, sort: "new_score");

        Assert.Equal("http://server.test/api/douban/comments?id=1292052&start=0&limit=10&sort=new_score", handler.Request!.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler, IDisposable
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
