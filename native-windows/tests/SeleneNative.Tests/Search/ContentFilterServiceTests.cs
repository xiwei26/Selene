using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Search;

public sealed class ContentFilterServiceTests
{
    [Fact]
    public void Filter_ShouldExcludeBlockedKeyword()
    {
        var results = new[]
        {
            new SearchResult { Title = "Great Movie", SourceName = "src" },
            new SearchResult { Title = "Avoid This", SourceName = "src" },
        };
        var svc = new ContentFilterService();

        var filtered = svc.Filter(results, "avoid");

        Assert.Single(filtered);
        Assert.Equal("Great Movie", filtered[0].Title);
    }

    [Fact]
    public void Filter_ShouldBeCaseInsensitive()
    {
        var results = new[] { new SearchResult { Description = "BLOCKED word" } };
        var svc = new ContentFilterService();

        Assert.Empty(svc.Filter(results, "blocked"));
        Assert.Single(svc.Filter(results, "something"));
    }

    [Fact]
    public void Filter_ShouldHandleEmptyKeywords()
    {
        var results = new[] { new SearchResult { Title = "A" }, new SearchResult { Title = "B" } };
        var svc = new ContentFilterService();

        Assert.Equal(2, svc.Filter(results, "").Count);
        Assert.Equal(2, svc.Filter(results, "  ").Count);
    }
}
