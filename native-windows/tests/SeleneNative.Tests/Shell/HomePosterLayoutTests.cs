using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class HomePosterLayoutTests
{
    [Fact]
    public void PlayRecordCards_ShouldUsePosterAspectRatio()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "UiHelpers.cs"));
        var method = ExtractMethod(source, "public static UIElement CreatePlayRecordCard");

        Assert.Contains("new StackPanel { Width = 180", method);
        Assert.Contains("CreateImageHost(record.Cover, 180, 252)", method);
    }

    [Fact]
    public void ImageHost_ShouldDisplayCompletePosterImage()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "UiHelpers.cs"));
        var method = ExtractMethod(source, "public static Border CreateImageHost");

        Assert.Contains("Stretch = Stretch.Uniform", method);
        Assert.DoesNotContain("Stretch = Stretch.UniformToFill", method);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {signature}.");

        var nextMethod = source.IndexOf("\n    public static", start + signature.Length, StringComparison.Ordinal);
        return nextMethod > start ? source[start..nextMethod] : source[start..];
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
