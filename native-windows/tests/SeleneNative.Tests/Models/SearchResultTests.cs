using System.Text.Json;
using SeleneNative.Core.Models;
using Xunit;

namespace SeleneNative.Tests.Models;

public class SearchResultTests
{
    [Fact]
    public void Deserialize_WithValidJson_ShouldReturnSearchResult()
    {
        // Arrange
        var json = """
        {
            "id": "123",
            "title": "测试视频",
            "poster": "https://example.com/poster.jpg",
            "episodes": ["https://example.com/ep1", "https://example.com/ep2"],
            "episodes_titles": ["第1集", "第2集"],
            "source": "test",
            "source_name": "测试源",
            "class": "电影",
            "year": "2024",
            "desc": "这是一个测试视频",
            "type_name": "电影",
            "douban_id": 12345
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<SearchResult>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.Id);
        Assert.Equal("测试视频", result.Title);
        Assert.Equal("https://example.com/poster.jpg", result.Poster);
        Assert.Equal(2, result.Episodes.Count);
        Assert.Equal(2, result.EpisodeTitles.Count);
        Assert.Equal("test", result.Source);
        Assert.Equal("测试源", result.SourceName);
        Assert.Equal("电影", result.ClassName);
        Assert.Equal("2024", result.Year);
        Assert.Equal("这是一个测试视频", result.Description);
        Assert.Equal("电影", result.TypeName);
        Assert.Equal(12345, result.DoubanId);
    }

    [Fact]
    public void Deserialize_WithMissingOptionalFields_ShouldReturnSearchResult()
    {
        // Arrange
        var json = """
        {
            "id": "123",
            "title": "测试视频",
            "poster": "https://example.com/poster.jpg",
            "episodes": [],
            "episodes_titles": [],
            "source": "test",
            "source_name": "测试源",
            "year": "2024"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<SearchResult>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.Id);
        Assert.Equal("测试视频", result.Title);
        Assert.Null(result.ClassName);
        Assert.Null(result.Description);
        Assert.Null(result.TypeName);
        Assert.Null(result.DoubanId);
    }

    [Fact]
    public void Deserialize_WithEmptyJson_ShouldReturnDefaultValues()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = JsonSerializer.Deserialize<SearchResult>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Id);
        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(string.Empty, result.Poster);
        Assert.Empty(result.Episodes);
        Assert.Empty(result.EpisodeTitles);
        Assert.Equal(string.Empty, result.Source);
        Assert.Equal(string.Empty, result.SourceName);
        Assert.Equal(string.Empty, result.Year);
    }
}
