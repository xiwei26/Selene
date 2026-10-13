using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Player;

public sealed class SourceMatcherTests
{
    private static SearchResult Work(
        string title,
        string source = "src",
        string id = "1",
        string year = "",
        string? typeName = null,
        int? doubanId = null)
    {
        return new SearchResult
        {
            Title = title,
            Source = source,
            Id = id,
            Year = year,
            TypeName = typeName,
            DoubanId = doubanId,
            Episodes = ["https://example.com/1.m3u8"],
        };
    }

    private static SearchResult Episodes(params string[] titles) => new()
    {
        Title = "剧",
        Source = "src",
        Id = "1",
        Episodes = titles.Select((_, index) => $"https://example.com/{index}.m3u8").ToList(),
        EpisodeTitles = [.. titles],
    };

    private static SearchResult Untitled(int count) => new()
    {
        Title = "剧",
        Source = "src",
        Id = "1",
        Episodes = Enumerable.Range(0, count).Select(index => $"https://example.com/{index}.m3u8").ToList(),
    };

    // ---- same work ----

    [Theory]
    [InlineData("庆余年", "庆余年")]
    [InlineData("庆余年 第二季", "庆余年第2季")]
    [InlineData("庆余年 第二季", "庆余年 Season 2")]
    [InlineData("庆余年 第二季", "庆余年 S2")]
    [InlineData("流浪地球 2", "流浪地球2")]
    [InlineData("Breaking Bad", "breaking  bad")]
    [InlineData("三体：黑暗森林", "三体 黑暗森林")]
    [InlineData("ＡＢＣ１２３", "abc123")]
    [InlineData("哈利·波特 第一部", "哈利波特第1部")]
    public void NormalizeTitle_ShouldTreatSpellingVariantsAsEqual(string left, string right)
    {
        Assert.Equal(SourceMatcher.NormalizeTitle(left), SourceMatcher.NormalizeTitle(right));
    }

    [Theory]
    [InlineData("庆余年 第一季", "庆余年 第二季")]
    [InlineData("庆余年", "庆余年 第二季")]
    [InlineData("流浪地球", "流浪地球2")]
    [InlineData("哈利波特 第一部", "哈利波特 第二部")]
    [InlineData("庆余年 第二季", "庆余年 第二部")]
    [InlineData("庆余年", "庆余年之帝王业")]
    [InlineData("十二怒汉", "十三怒汉")]
    public void NormalizeTitle_ShouldKeepSeasonsPartsAndSequelsApart(string left, string right)
    {
        Assert.NotEqual(SourceMatcher.NormalizeTitle(left), SourceMatcher.NormalizeTitle(right));
    }

    [Fact]
    public void IsSameWork_ShouldAcceptTheSameDoubanId_EvenWhenTitlesDiffer()
    {
        var reference = Work("三体", doubanId: 26647087, year: "2023");
        var candidate = Work("三体 (电视剧版)", source: "b", doubanId: 26647087, year: "2022");

        Assert.True(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Fact]
    public void IsSameWork_ShouldRejectADifferentDoubanId_EvenWhenTitlesMatch()
    {
        // A remake with the same name.
        var reference = Work("倚天屠龙记", doubanId: 1291543, year: "2003");
        var candidate = Work("倚天屠龙记", source: "b", doubanId: 26786669, year: "2003");

        Assert.False(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Fact]
    public void IsSameWork_ShouldRejectAYearConflict_WithoutFallingBackToTheTitle()
    {
        var reference = Work("倚天屠龙记", year: "2003");
        var candidate = Work("倚天屠龙记", source: "b", year: "2019");

        Assert.False(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Fact]
    public void IsSameWork_ShouldRejectAMediaTypeConflict()
    {
        var reference = Work("你好，李焕英", typeName: "喜剧片");
        var candidate = Work("你好，李焕英", source: "b", typeName: "国产剧");

        Assert.False(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Theory]
    [InlineData("", "2023")]
    [InlineData("unknown", "2023")]
    [InlineData("2023", "")]
    public void IsSameWork_ShouldNotTreatAMissingYearAsAConflict(string referenceYear, string candidateYear)
    {
        var reference = Work("三体", year: referenceYear);
        var candidate = Work("三体", source: "b", year: candidateYear);

        Assert.True(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Theory]
    [InlineData("喜剧", "喜剧片")]
    [InlineData("动漫", "动画片")]
    [InlineData(null, "国产剧")]
    [InlineData("国产剧", "大陆剧")]
    public void IsSameWork_ShouldNotInventATypeConflictFromAmbiguousLabels(string? referenceType, string? candidateType)
    {
        var reference = Work("同名作品", typeName: referenceType);
        var candidate = Work("同名作品", source: "b", typeName: candidateType);

        Assert.True(SourceMatcher.IsSameWork(reference, candidate));
    }

    [Fact]
    public void IsSameWork_ShouldRejectResultsThatOnlyShareAKeyword()
    {
        var reference = Work("三体");

        Assert.False(SourceMatcher.IsSameWork(reference, Work("三体 第二季", source: "b")));
        Assert.False(SourceMatcher.IsSameWork(reference, Work("我的三体", source: "c")));
        Assert.False(SourceMatcher.IsSameWork(reference, Work("", source: "d")));
        Assert.False(SourceMatcher.IsSameWork(Work(""), Work("", source: "e")));
    }

    [Fact]
    public void BuildCandidates_ShouldPutTheCurrentSourceFirst_AndDeduplicateBySourceAndId()
    {
        var current = Work("三体", source: "a", id: "1", year: "2023");
        var currentAgain = Work("三体", source: "a", id: "1", year: "2023");
        var other = Work("三体", source: "b", id: "7", year: "2023");
        var otherAgain = Work("三体", source: "b", id: "7", year: "2023");
        var sameSourceOtherId = Work("三体", source: "b", id: "8", year: "2023");
        var wrongYear = Work("三体", source: "c", id: "1", year: "2006");
        var noIdentity = Work("三体", source: "", id: "");

        var candidates = SourceMatcher.BuildCandidates(
            current,
            [other, currentAgain, otherAgain, wrongYear, sameSourceOtherId, noIdentity]);

        Assert.Equal([current, other, sameSourceOtherId], candidates);
    }

    [Fact]
    public void BuildCandidates_ShouldReturnOnlyTheCurrentSource_WhenThereIsNoPool()
    {
        var current = Work("三体");

        Assert.Equal([current], SourceMatcher.BuildCandidates(current, null));
    }

    // ---- episode matching ----

    [Fact]
    public void MatchEpisodeIndex_ShouldPreferTheEpisodeName_OverThePosition()
    {
        var from = Episodes("第1集", "第2集", "第3集");
        var to = Episodes("第3集", "第2集", "第1集");

        Assert.Equal(2, SourceMatcher.MatchEpisodeIndex(from, 0, to));
        Assert.Equal(1, SourceMatcher.MatchEpisodeIndex(from, 1, to));
        Assert.Equal(0, SourceMatcher.MatchEpisodeIndex(from, 2, to));
    }

    [Theory]
    [InlineData("第5集", "5")]
    [InlineData("第5集", "05")]
    [InlineData("第五集", "第5集")]
    [InlineData("EP05", "第5话")]
    [InlineData("S01E05", "5集")]
    [InlineData("第 5 集", "第5集")]
    public void MatchEpisodeIndex_ShouldMatchTheSameEpisodeNumber_AcrossNamingStyles(string fromTitle, string toTitle)
    {
        var from = Episodes("a", "b", fromTitle);
        var to = Episodes("第1集", "第2集", "第3集", "第4集", toTitle, "第6集");

        Assert.Equal(4, SourceMatcher.MatchEpisodeIndex(from, 2, to));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldReturnNull_WhenTheTargetLacksTheEpisode()
    {
        var from = Episodes("第1集", "第2集", "第3集", "第4集", "第5集");
        var to = Episodes("第1集", "第2集", "第3集");

        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 4, to));
        Assert.Equal(2, SourceMatcher.MatchEpisodeIndex(from, 2, to));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldNotFallBackToThePosition_WhenTheTargetSkipsAnEpisode()
    {
        // The target lacks episode 2, so position 1 there is episode 3.
        var from = Episodes("第1集", "第2集", "第3集");
        var to = Episodes("第1集", "第3集", "第4集");

        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 1, to));
        Assert.Equal(1, SourceMatcher.MatchEpisodeIndex(from, 2, to));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldUseThePosition_WhenNeitherSourceNamesEpisodes()
    {
        Assert.Equal(3, SourceMatcher.MatchEpisodeIndex(Untitled(10), 3, Untitled(12)));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(Untitled(10), 8, Untitled(5)));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldMapNumberedTitlesOntoAnUntitledSource()
    {
        var from = Episodes("第3集", "第4集", "第5集");

        // Untitled episodes count from one, so "第5集" is the fifth entry.
        Assert.Equal(4, SourceMatcher.MatchEpisodeIndex(from, 2, Untitled(6)));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 2, Untitled(4)));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldUseThePositionForFreeFormNames_OnlyWhenTheCountsAgree()
    {
        Assert.Equal(0, SourceMatcher.MatchEpisodeIndex(Episodes("HD"), 0, Episodes("正片")));
        Assert.Equal(1, SourceMatcher.MatchEpisodeIndex(Episodes("HD中字"), 0, Episodes("HD国语", "HD中字")));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(Episodes("HD"), 0, Episodes("抢先版", "正片")));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldSkipEntriesWithoutAPlayableUrl()
    {
        var from = Episodes("第1集", "第2集");
        var to = Episodes("第1集", "第2集");
        to.Episodes[1] = "";

        Assert.Equal(0, SourceMatcher.MatchEpisodeIndex(from, 0, to));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 1, to));
    }

    [Fact]
    public void MatchEpisodeIndex_ShouldRejectAmbiguousOrOutOfRangeRequests()
    {
        var from = Episodes("第1集", "第2集");

        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, -1, from));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 2, from));
        Assert.Null(SourceMatcher.MatchEpisodeIndex(from, 0, Untitled(0)));

        // Two entries carry the same name and neither sits at the original position.
        Assert.Null(SourceMatcher.MatchEpisodeIndex(Episodes("第1集"), 0, Episodes("预告", "第1集", "第1集")));
    }

    [Fact]
    public void EpisodeTitle_ShouldFallBackToTheOneBasedNumber()
    {
        var result = Episodes("序章", " ");

        Assert.Equal("序章", SourceMatcher.EpisodeTitle(result, 0));
        Assert.Equal("第2集", SourceMatcher.EpisodeTitle(result, 1));
        Assert.Equal("第8集", SourceMatcher.EpisodeTitle(Untitled(10), 7));
    }
}
