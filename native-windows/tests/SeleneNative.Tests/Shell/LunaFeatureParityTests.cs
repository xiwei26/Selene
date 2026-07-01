using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class LunaFeatureParityTests
{
    [Fact]
    public void MainNavigation_ShouldExposeLunaFeatureEntries()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));

        Assert.Contains("NavItem(\"短剧\", \"shortdrama\"", source);
        Assert.Contains("NavItem(\"Bilibili\", \"bilibili\"", source);
        Assert.Contains("NavItem(\"YouTube\", \"youtube\"", source);
    }

    [Fact]
    public void ServerApiClient_ShouldUseLunaFeatureEndpoints()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative.Core", "Services", "ServerApiClient.cs"));

        Assert.Contains("/api/shortdrama/recommend", source);
        Assert.Contains("/api/shortdrama/search", source);
        Assert.Contains("/api/shortdrama/detail", source);
        Assert.Contains("/api/bilibili/popular", source);
        Assert.Contains("/api/bilibili/search", source);
        Assert.Contains("/api/youtube/popular", source);
        Assert.Contains("/api/youtube/search", source);
        Assert.Contains("/api/tmdb/backdrop", source);
        Assert.Contains("/api/douban/quick-info", source);
        Assert.Contains("/api/douban/comments", source);
        Assert.Contains("/api/douban/recommends", source);
    }

    [Fact]
    public void DetailPage_ShouldRenderEnhancedMetadata()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "DetailPage.xaml.cs"));

        Assert.Contains("TmdbBackdrop", source);
        Assert.Contains("QuickInfo", source);
        Assert.Contains("Comments", source);
        Assert.Contains("Recommendations", source);
    }

    [Fact]
    public void PlayerPage_ShouldRenderEnhancedMetadataBelowFixedVideo()
    {
        var xaml = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "PlayerPage.xaml"));
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "PlayerPage.xaml.cs"));

        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
        Assert.Contains("VerticalScrollMode=\"Enabled\"", xaml);
        Assert.Contains("x:Name=\"InfoPanel\"", xaml);
        Assert.Contains("PlayerMetadataViewModel", source);
        Assert.Contains("LoadPlayerMetadataAsync", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(relativeParts));
    }
}
