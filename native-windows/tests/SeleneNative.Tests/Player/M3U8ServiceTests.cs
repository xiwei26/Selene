using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Player;

public sealed class M3U8ServiceTests
{
    [Theory]
    [InlineData("https://example.com/1080p/index.m3u8", 3)]
    [InlineData("https://example.com/4k/index.m3u8", 4)]
    [InlineData("https://example.com/2160/index.m3u8", 4)]
    [InlineData("https://example.com/720p/index.m3u8", 2)]
    [InlineData("https://example.com/480p/index.m3u8", 1)]
    [InlineData("https://example.com/stream.mp4", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void ResolutionRank_ShouldMatchExpected(string? url, int expected)
    {
        Assert.Equal(expected, M3U8Service.ResolutionRank(url ?? string.Empty));
    }

    [Fact]
    public void SortedByLikelyQuality_ShouldSortDescending()
    {
        var urls = new[]
        {
            "https://example.com/480p/index.m3u8",
            "https://example.com/1080p/index.m3u8",
            "https://example.com/720p/index.m3u8",
            "https://example.com/4k/index.m3u8",
        };

        var sorted = M3U8Service.SortedByLikelyQuality(urls);

        Assert.Equal(4, sorted.Count);
        Assert.Contains("4k", sorted[0]);
        Assert.Contains("1080p", sorted[1]);
        Assert.Contains("720p", sorted[2]);
        Assert.Contains("480p", sorted[3]);
    }

    [Fact]
    public void SortedByLikelyQuality_ShouldFilterEmptyUrls()
    {
        var urls = new[] { "", "https://example.com/1080p/index.m3u8", null! };
        var sorted = M3U8Service.SortedByLikelyQuality(urls);
        Assert.Single(sorted);
    }
}
