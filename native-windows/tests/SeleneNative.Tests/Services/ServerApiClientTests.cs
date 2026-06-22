using System.Net;
using System.Text;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Services;

public sealed class ServerApiClientTests
{
    [Fact]
    public async Task LoginAsync_ShouldPersistCookieFromResponse()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://server.test/api/login", request.RequestUri!.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("Set-Cookie", "sid=abc; Path=/; HttpOnly");
            return response;
        });
        var client = new ServerApiClient("http://server.test", httpClient: new HttpClient(handler));

        var session = await client.LoginAsync("alice", "secret");

        Assert.Equal("alice", session.Username);
        Assert.Equal("sid=abc", session.Cookie);
        Assert.Equal("http://server.test", session.ServerUrl);
    }

    [Fact]
    public async Task SearchAsync_ShouldReadWrappedResults()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """
            {
              "results": [
                {
                  "id": "1",
                  "title": "Movie",
                  "source": "demo",
                  "source_name": "Demo",
                  "episodes": ["https://example.test/1.m3u8"],
                  "episodes_titles": ["Episode 1"]
                }
              ]
            }
            """));
        var client = new ServerApiClient("http://server.test", "sid=abc", new HttpClient(handler));

        var results = await client.SearchAsync("movie");

        var result = Assert.Single(results);
        Assert.Equal("Movie", result.Title);
        Assert.Equal("Demo", result.SourceName);
    }

    [Fact]
    public async Task GetFavoritesAsync_ShouldReadKeyedMap()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """
            {
              "demo+1": {
                "title": "Favorite",
                "source_name": "Demo",
                "cover": "https://example.test/f.jpg",
                "save_time": 100
              }
            }
            """));
        var client = new ServerApiClient("http://server.test", "sid=abc", new HttpClient(handler));

        var favorites = await client.GetFavoritesAsync();

        var favorite = Assert.Single(favorites);
        Assert.Equal("demo+1", favorite.Id);
        Assert.Equal("demo", favorite.Source);
        Assert.Equal("1", favorite.ItemId);
        Assert.Equal("Favorite", favorite.Title);
    }

    [Fact]
    public async Task GetPlayRecordsAsync_ShouldReadWrappedRecords()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """
            {
              "records": {
                "demo+1": {
                  "title": "Continue",
                  "source_name": "Demo",
                  "index": 2,
                  "play_time": 30,
                  "total_time": 60,
                  "save_time": 1000
                }
              }
            }
            """));
        var client = new ServerApiClient("http://server.test", "sid=abc", new HttpClient(handler));

        var records = await client.GetPlayRecordsAsync();

        var record = Assert.Single(records);
        Assert.Equal("demo+1", record.Id);
        Assert.Equal("demo", record.Source);
        Assert.Equal("Continue", record.Title);
        Assert.Equal(0.5, record.ProgressPercentage);
    }

    [Fact]
    public async Task GetPlayRecordsAsync_ShouldReadDataWrappedRecords()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """
            {
              "data": {
                "demo+1": {
                  "title": "Continue",
                  "source_name": "Demo",
                  "index": 2,
                  "play_time": 30,
                  "total_time": 60,
                  "save_time": 1000
                }
              }
            }
            """));
        var client = new ServerApiClient("http://server.test", "sid=abc", new HttpClient(handler));

        var records = await client.GetPlayRecordsAsync();

        var record = Assert.Single(records);
        Assert.Equal("demo+1", record.Id);
        Assert.Equal("demo", record.Source);
        Assert.Equal("Continue", record.Title);
    }

    [Fact]
    public async Task GetPlayRecordsAsync_ShouldReadDataRecordsWrappedRecords()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """
            {
              "data": {
                "records": {
                  "demo+1": {
                    "title": "Continue",
                    "source_name": "Demo",
                    "index": 2,
                    "play_time": 30,
                    "total_time": 60,
                    "save_time": 1000
                  }
                }
              }
            }
            """));
        var client = new ServerApiClient("http://server.test", "sid=abc", new HttpClient(handler));

        var records = await client.GetPlayRecordsAsync();

        var record = Assert.Single(records);
        Assert.Equal("demo+1", record.Id);
        Assert.Equal("demo", record.Source);
        Assert.Equal("Continue", record.Title);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handle(request));
        }
    }
}
