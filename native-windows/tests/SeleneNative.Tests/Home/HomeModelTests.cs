using System.Text.Json;
using SeleneNative.Core.Models;
using Xunit;

namespace SeleneNative.Tests.Home;

public sealed class HomeModelTests
{
    [Fact]
    public void DoubanMovie_ShouldDeserializeRecentHotCard()
    {
        const string json = """
        {
          "id": 1292052,
          "title": "The Movie",
          "pic": { "normal": "https://img.example/poster.jpg" },
          "rating": { "value": "9.6" },
          "card_subtitle": "1994 / Drama"
        }
        """;

        var movie = JsonSerializer.Deserialize<DoubanMovie>(json);

        Assert.NotNull(movie);
        Assert.Equal("1292052", movie.Id);
        Assert.Equal("The Movie", movie.Title);
        Assert.Equal("https://img.example/poster.jpg", movie.Poster);
        Assert.Equal("9.6", movie.Rate);
        Assert.Equal("1994", movie.Year);
    }

    [Fact]
    public void BangumiItem_ShouldDecodeHtmlEntitiesAndChooseBestImage()
    {
        const string json = """
        {
          "id": 42,
          "url": "https://bgm.tv/subject/42",
          "type": 2,
          "name": "A &amp; B",
          "name_cn": "甲 &amp; 乙",
          "summary": "Story",
          "air_date": "2026-01-01",
          "air_weekday": 1,
          "rating": { "total": 10, "count": {}, "score": 8.5 },
          "rank": 5,
          "images": { "large": "", "common": "https://img.example/common.jpg", "medium": "", "small": "", "grid": "" },
          "collection": { "doing": 1, "on_hold": 2, "dropped": 3, "wish": 4, "collect": 5 }
        }
        """;

        var item = JsonSerializer.Deserialize<BangumiItem>(json);

        Assert.NotNull(item);
        Assert.Equal(42, item.Id);
        Assert.Equal("A & B", item.Name);
        Assert.Equal("甲 & 乙", item.NameCn);
        Assert.Equal("https://img.example/common.jpg", item.Images.BestImageUrl);
        Assert.Equal(8.5, item.Rating.Score);
    }

    [Fact]
    public void PlayRecord_ShouldComputeProgressPercentage()
    {
        const string json = """
        {
          "title": "Episode Show",
          "source": "demo",
          "source_name": "Demo Source",
          "id": "show-1",
          "cover": "https://img.example/show.jpg",
          "year": "2025",
          "index": 2,
          "play_time": 30,
          "total_time": 120,
          "save_time": "2026-06-22T12:00:00Z"
        }
        """;

        var record = JsonSerializer.Deserialize<PlayRecord>(json);

        Assert.NotNull(record);
        Assert.Equal("Episode Show", record.Title);
        Assert.Equal(2, record.EpisodeNumber);
        Assert.Equal(0.25, record.ProgressPercentage);
        Assert.Equal("Demo Source", record.SourceName);
    }
}
