using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Live;

public sealed class M3UParserTests
{
    [Fact]
    public void Parse_ShouldExtractChannels()
    {
        var content = """
        #EXTM3U
        #EXTINF:-1 tvg-id="cctv1" tvg-logo="https://img/cctv1.png" group-title="央视",CCTV-1 综合
        https://example.com/cctv1.m3u8
        #EXTINF:-1 tvg-id="cctv2" group-title="央视",CCTV-2 财经
        https://example.com/cctv2.m3u8
        """;

        var channels = M3UParser.Parse(content, "test-source");

        Assert.Equal(2, channels.Count);
        Assert.Equal("CCTV-1 综合", channels[0].Name);
        Assert.Equal("cctv1", channels[0].TvgId);
        Assert.Equal("https://img/cctv1.png", channels[0].Logo);
        Assert.Equal("央视", channels[0].Group);
        Assert.Equal("CCTV-2 财经", channels[1].Name);
        Assert.Equal("cctv2", channels[1].TvgId);
    }

    [Fact]
    public void Parse_ShouldHandleEmptyContent()
    {
        Assert.Empty(M3UParser.Parse("", "src"));
        Assert.Empty(M3UParser.Parse(null!, "src"));
    }

    [Fact]
    public void Parse_ShouldDefaultMissingAttributes()
    {
        var content = """
        #EXTINF:-1,Unknown Channel
        https://example.com/stream.m3u8
        """;

        var channels = M3UParser.Parse(content, "src");

        Assert.Single(channels);
        Assert.Equal("Unknown Channel", channels[0].Name);
        Assert.Equal("未分组", channels[0].Group);
    }
}
