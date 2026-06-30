using Xunit;

namespace SeleneNative.Tests.Shell;

public sealed class MainWindowPlaybackHistoryTests
{
    [Fact]
    public void PlayerClose_ShouldPersistCurrentRecordBeforeStoppingPlayback()
    {
        var source = File.ReadAllText(FindRepoFile("native-windows", "src", "SeleneNative", "MainWindow.xaml.cs"));
        var handlerStart = source.IndexOf("private async void OnPlayerCloseRequested", StringComparison.Ordinal);
        Assert.True(handlerStart >= 0, "OnPlayerCloseRequested should be an async handler.");

        var handlerEnd = source.IndexOf("private async Task OnPlayerSaveRecordAsync", handlerStart, StringComparison.Ordinal);
        Assert.True(handlerEnd > handlerStart, "Could not find the end of OnPlayerCloseRequested.");

        var handler = source[handlerStart..handlerEnd];
        var persistIndex = handler.IndexOf("await _playerPage.PersistCurrentRecordAsync()", StringComparison.Ordinal);
        var stopIndex = handler.IndexOf("_playerViewModel.Stop()", StringComparison.Ordinal);

        Assert.True(persistIndex >= 0, "Player close should persist the active play record.");
        Assert.True(stopIndex >= 0, "Player close should stop playback.");
        Assert.True(persistIndex < stopIndex, "Player close must persist before Stop clears playback state.");
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
