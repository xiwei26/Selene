using System.Xml.Linq;
using Xunit;

namespace SeleneNative.Tests.Shell;

/// <summary>
/// Structural checks on the player page markup: which commands sit on the fixed
/// toolbar, and that every icon-only command is named for pointer and assistive
/// technology users. Behaviour is covered by the Core player tests.
/// </summary>
public sealed class PlayerPageControlsTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument LoadPlayerPage() =>
        XDocument.Load(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "PlayerPage.xaml"));

    private static string? Name(XElement element) => element.Attribute(X + "Name")?.Value;

    [Theory]
    [InlineData("PlayPauseButton")]
    [InlineData("MuteButton")]
    [InlineData("VolumeButton")]
    [InlineData("SpeedButton")]
    [InlineData("EpisodesButton")]
    [InlineData("SourcesButton")]
    [InlineData("FullScreenButton")]
    [InlineData("PreviousEpisodeButton")]
    [InlineData("NextEpisodeButton")]
    [InlineData("SeekBackwardButton")]
    [InlineData("SeekForwardButton")]
    public void Toolbar_ShouldCarryEveryFrequentCommandAsAPrimaryCommand(string name)
    {
        var toolbar = LoadPlayerPage().Descendants(Xaml + "CommandBar").Single();

        // Direct children are primary commands; they never hide behind the info area's scrolling.
        Assert.Contains(toolbar.Elements(Xaml + "AppBarButton"), button => Name(button) == name);
    }

    [Fact]
    public void Toolbar_ShouldStayOutsideTheScrollingInfoArea()
    {
        var page = LoadPlayerPage();
        var toolbar = page.Descendants(Xaml + "CommandBar").Single();
        var infoArea = page.Descendants(Xaml + "ScrollViewer").Single(viewer => Name(viewer) == "BottomScrollViewer");

        Assert.DoesNotContain(toolbar.Ancestors(), ancestor => ancestor.Name == Xaml + "ScrollViewer");
        Assert.True(toolbar.IsBefore(infoArea), "The toolbar should sit between the video and the info area.");

        // Playback is not duplicated below: no second seek bar or pause button to scroll to.
        Assert.Empty(infoArea.Descendants(Xaml + "Slider"));
        Assert.Empty(infoArea.Descendants(Xaml + "Button"));
        Assert.Empty(infoArea.Descendants(Xaml + "AppBarButton"));
    }

    [Fact]
    public void EveryToolbarCommand_ShouldHaveAChineseLabelAndToolTip()
    {
        var commands = LoadPlayerPage()
            .Descendants()
            .Where(element => element.Name == Xaml + "AppBarButton" || element.Name == Xaml + "AppBarToggleButton")
            .ToList();

        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            var name = Name(command);

            // The label is the accessible name and the text shown in the overflow menu.
            Assert.True(ContainsChinese(command.Attribute("Label")?.Value), $"{name} needs a Chinese Label.");
            Assert.True(
                ContainsChinese(command.Attribute("ToolTipService.ToolTip")?.Value),
                $"{name} needs a Chinese ToolTip.");
        }
    }

    [Fact]
    public void EveryPlainButton_ShouldHaveAnAccessibleName()
    {
        var buttons = LoadPlayerPage()
            .Descendants()
            .Where(element => element.Name == Xaml + "Button" || element.Name == Xaml + "ToggleButton")
            .ToList();

        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
        {
            Assert.True(
                ContainsChinese(button.Attribute("AutomationProperties.Name")?.Value),
                $"{Name(button)} needs AutomationProperties.Name.");
        }
    }

    [Fact]
    public void Sliders_ShouldBeNamedForAssistiveTechnology()
    {
        foreach (var slider in LoadPlayerPage().Descendants(Xaml + "Slider"))
        {
            Assert.True(
                ContainsChinese(slider.Attribute("AutomationProperties.Name")?.Value),
                $"{Name(slider)} needs AutomationProperties.Name.");
        }
    }

    [Fact]
    public void VideoSurface_ShouldUseTheNativeTransportControls()
    {
        var source = File.ReadAllText(FindRepoFile(
            "native-windows", "src", "SeleneNative", "Views", "NativeVideoPlayerView.xaml.cs"));

        Assert.Contains("AreTransportControlsEnabled = true", source);
        Assert.DoesNotContain("AreTransportControlsEnabled = false", source);

        // Speed and skip buttons with different semantics are not shown twice.
        Assert.Contains("IsPlaybackRateButtonVisible = false", source);
        Assert.Contains("IsSkipForwardButtonVisible = false", source);
    }

    [Fact]
    public void FullScreen_ShouldBeOwnedByTheWindowPresenter()
    {
        var window = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));
        var page = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "Views", "PlayerPage.xaml.cs"));

        Assert.Contains("AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)", window);

        // The page asks; it does not switch presenters or re-parent the video itself.
        Assert.DoesNotContain("SetPresenter", page);
        Assert.DoesNotContain("IsFullWindow = true", page);
    }

    private static bool ContainsChinese(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Any(ch => ch is >= '一' and <= '鿿');

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
