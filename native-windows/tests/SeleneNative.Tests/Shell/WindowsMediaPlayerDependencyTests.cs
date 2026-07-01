using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class WindowsMediaPlayerDependencyTests
{
    [Fact]
    public void WindowsHost_ShouldNotReferenceBundledVlcPackages()
    {
        var project = File.ReadAllText(FindRepoFile(
            "native-windows",
            "src",
            "SeleneNative",
            "SeleneNative.csproj"));

        Assert.DoesNotContain("LibVLCSharp", project);
        Assert.DoesNotContain("VideoLAN.LibVLC.Windows", project);
    }

    [Fact]
    public void App_ShouldRegisterWindowsMediaPlayer()
    {
        var source = File.ReadAllText(FindRepoFile(
            "native-windows",
            "src",
            "SeleneNative",
            "App.xaml.cs"));

        Assert.Contains("AddWindowsMediaPlayer", source);
        Assert.DoesNotContain("AddLibVlcMediaPlayer", source);
    }

    [Fact]
    public void NativeVideoPlayerView_ShouldUseMediaPlayerElement()
    {
        var source = File.ReadAllText(FindRepoFile(
            "native-windows",
            "src",
            "SeleneNative",
            "Views",
            "NativeVideoPlayerView.xaml.cs"));

        Assert.Contains("MediaPlayerElement", source);
        Assert.Contains("WindowsMediaPlayer", source);
        Assert.DoesNotContain("LibVLCSharp", source);
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

        throw new FileNotFoundException(
            "Could not find repository file.",
            Path.Combine(relativeParts));
    }
}
