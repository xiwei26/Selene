using SeleneNative.Core.Models;
using Xunit;

namespace SeleneNative.Tests.Search;

public sealed class AggregatedSearchResultTests
{
    [Fact]
    public void RebuildAggregates_ShouldGroupByKey()
    {
        var results = new[]
        {
            NewResult("Title", "2025", "movie", "src1", "1"),
            NewResult("Title", "2025", "movie", "src2", "2"),
            NewResult("Other", "2024", "tv", "src1", "3"),
        };

        var agg = AggregatedSearchResult.RebuildAggregates(results);

        Assert.Equal(2, agg.Count);
        Assert.Equal(2, agg[0].OriginalResults.Count);
        Assert.Equal("Title", agg[0].Title);
        Assert.Single(agg[1].OriginalResults);
    }

    [Fact]
    public void BuildKey_ShouldBeCaseInsensitiveOnGrouping()
    {
        var results = new[]
        {
            NewResult("Title", "2025", "movie", "src1", "1"),
            NewResult("TITLE", "2025", "movie", "src2", "2"),
        };
        var agg = AggregatedSearchResult.RebuildAggregates(results);
        Assert.Single(agg);
        Assert.Equal(2, agg[0].OriginalResults.Count);
    }

    private static SearchResult NewResult(string title, string year, string type, string source, string id)
    {
        return new SearchResult
        {
            Title = title,
            Year = year,
            TypeName = type,
            Source = source,
            Id = id,
        };
    }
}
