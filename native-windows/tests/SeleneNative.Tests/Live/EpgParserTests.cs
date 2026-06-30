using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Live;

public sealed class EpgParserTests
{
    [Fact]
    public void Parse_ShouldExtractProgrammes()
    {
        var content = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tv>
        <programme channel="cctv1" start="20260622080000 +0800" stop="20260622090000 +0800">
            <title>新闻联播</title>
            <desc>晚间新闻</desc>
        </programme>
        <programme channel="cctv1" start="20260622090000 +0800" stop="20260622100000 +0800">
            <title>焦点访谈</title>
        </programme>
        <programme channel="cctv2" start="20260622080000 +0800" stop="20260622090000 +0800">
            <title>财经新闻</title>
        </programme>
        </tv>
        """;

        var epg = EpgParser.Parse(content, "cctv1", "src", "https://epg.example.com");

        Assert.NotNull(epg);
        Assert.Equal(2, epg.Programs.Count);
        Assert.Equal("新闻联播", epg.Programs[0].Title);
        Assert.Equal("晚间新闻", epg.Programs[0].Description);
        Assert.Equal("焦点访谈", epg.Programs[1].Title);
    }

    [Fact]
    public void Parse_ShouldReturnNull_ForEmptyContent()
    {
        Assert.Null(EpgParser.Parse("", "tvg", "src", "url"));
        Assert.Null(EpgParser.Parse(null!, "tvg", "src", "url"));
    }

    [Fact]
    public void Parse_ShouldReturnNull_WhenNoMatchingChannel()
    {
        var content = """
        <tv><programme channel="other" start="20260622080000 +0800" stop="20260622090000 +0800">
        <title>X</title></programme></tv>
        """;

        Assert.Null(EpgParser.Parse(content, "cctv1", "src", "url"));
    }
}
