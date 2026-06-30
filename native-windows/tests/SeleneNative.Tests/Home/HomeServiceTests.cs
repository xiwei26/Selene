using System.Net;
using System.Text;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Home;

public sealed class HomeServiceTests
{
    [Fact]
    public async Task DoubanClient_GetHotMovies_ShouldRequestRecentHotMovies()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("/rexxar/api/v2/subject/recent_hot/movie", request.RequestUri?.AbsolutePath);
            Assert.Contains("category=%E7%83%AD%E9%97%A8", request.RequestUri?.Query);
            Assert.Contains("type=%E5%85%A8%E9%83%A8", request.RequestUri?.Query);

            return JsonResponse("""
            {
              "items": [
                { "id": "m1", "title": "Movie One", "poster": "https://img.example/m1.jpg", "rate": "8.8", "year": "2026" }
              ]
            }
            """);
        });
        var client = new DoubanClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        });

        var movies = await client.GetHotMoviesAsync();

        var movie = Assert.Single(movies);
        Assert.Equal("Movie One", movie.Title);
    }

    [Theory]
    [InlineData("movie", "category=%E7%83%AD%E9%97%A8", "type=%E5%85%A8%E9%83%A8")]
    [InlineData("tv", "category=tv", "type=tv")]
    [InlineData("shows", "category=show", "type=show")]
    public async Task DoubanClient_WithBackend_ShouldRequestLunaCategories(
        string section,
        string expectedCategory,
        string expectedType)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("http://server.test/api/douban/categories", request.RequestUri?.GetLeftPart(UriPartial.Path));
            Assert.Contains(expectedCategory, request.RequestUri?.Query);
            Assert.Contains(expectedType, request.RequestUri?.Query);
            Assert.Contains("limit=20", request.RequestUri?.Query);
            Assert.Contains("start=0", request.RequestUri?.Query);
            Assert.True(request.Headers.TryGetValues("Cookie", out var cookies));
            Assert.Contains("sid=abc", cookies);

            return JsonResponse("""
            {
              "code": 200,
              "list": [
                { "id": "m1", "title": "Luna Item", "poster": "https://img.example/m1.jpg", "rate": "8.8", "year": "2026" }
              ]
            }
            """);
        });
        var client = new DoubanClient(new HttpClient(handler));
        client.ConfigureBackend("http://server.test", "sid=abc");

        var movies = section switch
        {
            "tv" => await client.GetHotTvShowsAsync(),
            "shows" => await client.GetHotShowsAsync(),
            _ => await client.GetHotMoviesAsync(),
        };

        var movie = Assert.Single(movies);
        Assert.Equal("Luna Item", movie.Title);
    }

    [Fact]
    public async Task BangumiClient_GetCalendarByWeekday_ShouldReturnMatchingDay()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("/calendar", request.RequestUri?.AbsolutePath);

            return JsonResponse("""
            [
              {
                "weekday": { "en": "Mon", "cn": "星期一", "ja": "月曜", "id": 1 },
                "items": [
                  {
                    "id": 100,
                    "url": "",
                    "type": 2,
                    "name": "Bangumi One",
                    "name_cn": "番组一",
                    "summary": "",
                    "air_date": "2026-01-01",
                    "air_weekday": 1,
                    "rating": { "total": 1, "count": {}, "score": 7.7 },
                    "rank": 10,
                    "images": { "large": "https://img.example/b1.jpg", "common": "", "medium": "", "small": "", "grid": "" },
                    "collection": { "doing": 0, "on_hold": 0, "dropped": 0, "wish": 0, "collect": 0 }
                  }
                ]
              }
            ]
            """);
        });
        var client = new BangumiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test")
        });

        var items = await client.GetCalendarByWeekdayAsync(1);

        var item = Assert.Single(items);
        Assert.Equal("番组一", item.DisplayTitle);
    }

    [Fact]
    public async Task PlayRecordStore_LoadAsync_ShouldReadKeyedMapAndSortNewestFirst()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempFile, """
        {
          "old": {
            "title": "Old",
            "source": "demo",
            "source_name": "Demo",
            "id": "old",
            "index": 1,
            "play_time": 1,
            "total_time": 2,
            "save_time": "2026-01-01T00:00:00Z"
          },
          "new": {
            "title": "New",
            "source": "demo",
            "source_name": "Demo",
            "id": "new",
            "index": 2,
            "play_time": 3,
            "total_time": 4,
            "save_time": "2026-06-22T00:00:00Z"
          }
        }
        """);

        try
        {
            var store = new PlayRecordStore(tempFile);

            var records = await store.LoadAsync();

            Assert.Collection(
                records,
                first => Assert.Equal("New", first.Title),
                second => Assert.Equal("Old", second.Title));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
