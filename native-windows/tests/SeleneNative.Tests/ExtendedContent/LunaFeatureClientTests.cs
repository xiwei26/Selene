using System.Net;
using System.Text;
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
