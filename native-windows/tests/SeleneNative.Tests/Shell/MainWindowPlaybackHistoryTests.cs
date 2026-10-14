using Xunit;

namespace SeleneNative.Tests.Shell;

/// <summary>
/// The shell cannot be instantiated in the Core test project, so these check the
/// wiring in source. The behaviour behind it (snapshot before stop, ordered
/// writes, local fallback) is covered by <c>PlayerRecordTests</c>.
/// </summary>
public sealed class MainWindowPlaybackHistoryTests
{
    [Fact]
    public void Shell_ShouldNeverStopPlaybackWithoutTakingTheRecordFirst()
    {
        var source = ReadMainWindow();

        // Snapshot-then-stop is one coordinator operation; the shell has no other way to stop.
        Assert.DoesNotContain("_playerViewModel.Stop()", source);
        Assert.Contains("_playerViewModel.CloseAsync()", source);
        Assert.Contains("_playerViewModel.RecordSaver = SavePlayRecordAsync", source);
    }

    [Theory]
    [InlineData("private async Task ShowPageAsync(string page)")]
    [InlineData("private async void OnAppWindowClosing(")]
    [InlineData("private void OnPlayerPageLeft(")]
    public void EveryExitPath_ShouldLeaveThePlayerThroughTheCoordinator(string signature)
    {
        var body = MethodBody(ReadMainWindow(), signature);

        Assert.Contains("LeavePlayer()", body);
    }

    [Fact]
    public void PlayerClose_ShouldGoHomeThroughTheSharedLeavePath()
    {
        var body = MethodBody(ReadMainWindow(), "private async void OnPlayerCloseRequested(");

        Assert.Contains("await ShowPageAsync(\"home\")", body);
    }

    [Fact]
    public void HomeNavigation_ShouldWaitForTheRecordBeforeLoadingContinueWatching()
    {
        var body = MethodBody(ReadMainWindow(), "private async Task ShowPageAsync(string page)");
        var waitIndex = body.IndexOf("Task.WhenAny(recordSaved", StringComparison.Ordinal);
        var loadIndex = body.IndexOf("await Home.LoadAsync(provider)", StringComparison.Ordinal);

        Assert.True(waitIndex >= 0, "Home navigation should wait for the pending record write.");
        Assert.True(loadIndex > waitIndex, "The record must be written before the home page loads records.");
    }

    [Fact]
    public void WindowClose_ShouldHoldTheWindowUntilTheRecordIsWritten()
    {
        var body = MethodBody(ReadMainWindow(), "private async void OnAppWindowClosing(");
        var leaveIndex = body.IndexOf("LeavePlayer()", StringComparison.Ordinal);
        var cancelIndex = body.IndexOf("args.Cancel = true", StringComparison.Ordinal);
        var waitIndex = body.IndexOf("await Task.WhenAny(saving", StringComparison.Ordinal);
        var closeIndex = body.IndexOf("Close();", StringComparison.Ordinal);

        Assert.True(leaveIndex >= 0 && cancelIndex > leaveIndex, "Closing should snapshot first, then hold the window.");
        Assert.True(waitIndex > cancelIndex, "Closing should wait for the record write.");
        Assert.True(closeIndex > waitIndex, "The window should close only after the wait.");
    }

    private static string ReadMainWindow() =>
        File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));

    /// <summary>Returns the text of the method that starts with <paramref name="signature"/>.</summary>
    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{signature}' in MainWindow.xaml.cs.");

        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[start..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Could not find the end of '{signature}'.");
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
