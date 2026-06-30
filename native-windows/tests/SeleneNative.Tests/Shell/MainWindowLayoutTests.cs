using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class MainWindowLayoutTests
{
    [Fact]
    public void ContentHost_ShouldStretchHostedPages()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));

        Assert.Contains("HorizontalContentAlignment = HorizontalAlignment.Stretch", source);
        Assert.Contains("VerticalContentAlignment = VerticalAlignment.Stretch", source);
    }

    [Fact]
    public void NavigationView_ShouldStretchToWindow()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));

        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Stretch", source);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Stretch", source);
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
