using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

/// <summary>Previous/next episode, display order, and automatic advance at the end of an episode.</summary>
public sealed class PlayerEpisodeNavigationTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(
        Path.GetTempPath(), "SeleneNativePlayerNavTests", Guid.NewGuid().ToString("N"));

    private static (PlayerViewModel Vm, FakeMediaPlayer Player) Playing(int episodes = 3, int startIndex = 0)
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(episodes: episodes), startIndex);
        player.SimulateOpened();
        return (vm, player);
    }

    [Fact]
    public void PreviousAndNext_ShouldBeDisabledAtTheBoundaries()
    {
        var (vm, player) = Playing(episodes: 3);

        Assert.False(vm.CanPlayPrevious);
        Assert.True(vm.CanPlayNext);
        Assert.False(vm.PlayPreviousEpisode());
        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);

        Assert.True(vm.PlayNextEpisode());
        Assert.True(vm.PlayNextEpisode());

        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.True(vm.CanPlayPrevious);
        Assert.False(vm.CanPlayNext);
        Assert.False(vm.PlayNextEpisode());
        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.Equal(3, player.LoadedUrls.Count);
    }

    [Fact]
    public void SingleEpisodeWork_ShouldHaveNoNeighbours()
    {
        var (vm, _) = Playing(episodes: 1);

        Assert.False(vm.CanPlayPrevious);
        Assert.False(vm.CanPlayNext);
    }

    [Fact]
    public void BoundaryState_ShouldNotify_WhenTheEpisodeChanges()
    {
        var (vm, _) = Playing(episodes: 2);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.PlayNextEpisode();

        Assert.Contains(nameof(PlayerViewModel.CanPlayNext), notified);
        Assert.Contains(nameof(PlayerViewModel.CanPlayPrevious), notified);
        Assert.Contains(nameof(PlayerViewModel.CurrentEpisodeTitle), notified);
    }

    [Fact]
    public void ReversedDisplayOrder_ShouldNotChangeEpisodeIdentity()
    {
        var (vm, player) = Playing(episodes: 4, startIndex: 1);

        vm.ToggleEpisodeOrder();

        Assert.Equal([3, 2, 1, 0], vm.OrderedEpisodeIndices);
        Assert.Equal(1, vm.CurrentEpisodeIndex);

        // "Next" is the next backend episode, not the next item on screen.
        Assert.True(vm.PlayNextEpisode());

        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.EndsWith("/ep3.m3u8", player.LoadedUrls[^1]);
        Assert.Equal(3, vm.MakePlayRecord()!.EpisodeNumber);

        // Picking the first row on screen plays the backend episode it shows.
        Assert.True(vm.PlayEpisode(vm.OrderedEpisodeIndices[0]));

        Assert.Equal(3, vm.CurrentEpisodeIndex);
        Assert.EndsWith("/ep4.m3u8", player.LoadedUrls[^1]);
        Assert.Equal(4, vm.MakePlayRecord()!.EpisodeNumber);
    }

    [Fact]
    public void PlayEpisode_ShouldRejectIndicesOutsideTheWork()
    {
        var (vm, player) = Playing(episodes: 3);

        Assert.False(vm.PlayEpisode(-1));
        Assert.False(vm.PlayEpisode(3));

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
    }

    [Fact]
    public void NaturalEnd_ShouldAdvanceToTheNextEpisode_ExactlyOnce()
    {
        var (vm, player) = Playing(episodes: 4);
        player.SimulatePosition(599);

        player.SimulateEnded();

        Assert.Equal(1, vm.CurrentEpisodeIndex);
        Assert.Equal(2, player.LoadedUrls.Count);
        Assert.True(player.PlayRequested);

        // Late and repeated end notifications for the finished item arrive while
        // the next one is still opening. They must not skip episodes.
        player.SimulateState(MediaPlaybackState.Ended);
        player.SimulateState(MediaPlaybackState.Ended);

        Assert.Equal(1, vm.CurrentEpisodeIndex);
        Assert.Equal(2, player.LoadedUrls.Count);

        // The next episode then plays to its own end and advances once more.
        player.SimulateOpened();
        player.SimulateEnded();
        player.SimulateState(MediaPlaybackState.Ended);

        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.Equal(3, player.LoadedUrls.Count);
    }

    [Fact]
    public void NaturalEnd_ShouldNotAdvance_WhenAutoPlayNextIsOff()
    {
        var (vm, player) = Playing(episodes: 3);
        vm.AutoPlayNext = false;

        player.SimulateEnded();

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Ended, vm.PlaybackState);
        Assert.Equal("本集播放结束", vm.StatusText);

        // Switching the option on afterwards does not act on an episode that already ended.
        vm.AutoPlayNext = true;

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
    }

    [Fact]
    public void LastEpisode_ShouldFinishWithoutLooping()
    {
        var (vm, player) = Playing(episodes: 2, startIndex: 1);

        player.SimulateEnded();
        player.SimulateState(MediaPlaybackState.Ended);

        Assert.Equal(1, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Ended, vm.PlaybackState);
        Assert.Equal("播放完毕", vm.StatusText);
        Assert.Equal(600, vm.PlayTime);
    }

    [Fact]
    public void ErrorOrStop_ShouldNotAdvance()
    {
        var (vm, player) = Playing(episodes: 3);

        player.SimulateError("网络中断");

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);

        vm.Stop();

        Assert.Single(player.LoadedUrls);
        Assert.Null(vm.CurrentResult);

        // An end notification that arrives after the stop is ignored.
        player.SimulateState(MediaPlaybackState.Ended);

        Assert.Single(player.LoadedUrls);
        Assert.Null(vm.CurrentEpisodeUrl);
    }

    [Fact]
    public void SeekingToTheEnd_ShouldFinishTheEpisodeWithoutAdvancing()
    {
        var (vm, player) = Playing(episodes: 3);

        vm.SeekTo(600);
        player.SimulateEnded();

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Ended, vm.PlaybackState);
    }

    [Fact]
    public void SeekToTheEndMadeByNativeControls_ShouldNotAdvanceEither()
    {
        var (vm, player) = Playing(episodes: 3);

        // The native seek bar sets the engine position without the view model.
        player.Position = 599.5;
        player.SimulateEnded();

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
    }

    [Fact]
    public void ReplayAfterAManualSeekToTheEnd_ShouldAdvanceWhenItEndsNaturally()
    {
        var (vm, player) = Playing(episodes: 3);
        vm.SeekTo(600);
        player.SimulateEnded();

        // The viewer seeks back and watches the rest.
        vm.SeekTo(300);
        vm.Play();
        player.SimulateEnded();

        Assert.Equal(1, vm.CurrentEpisodeIndex);
        Assert.Equal(2, player.LoadedUrls.Count);
    }

    [Fact]
    public void EndBeforeTheItemEverPlayed_ShouldBeIgnored()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(episodes: 3), 0);

        // Not opened yet: this can only be a stale notification.
        player.SimulateState(MediaPlaybackState.Ended);

        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
    }

    [Fact]
    public void AutoPlayNext_ShouldDefaultToOn_AndPersistAcrossRestarts()
    {
        var first = new PlayerViewModel(() => new FakeMediaPlayer(), new PlayerSettingsStore(_settingsDirectory));
        Assert.True(first.AutoPlayNext);

        first.AutoPlayNext = false;

        var restarted = new PlayerViewModel(() => new FakeMediaPlayer(), new PlayerSettingsStore(_settingsDirectory));
        Assert.False(restarted.AutoPlayNext);

        restarted.AutoPlayNext = true;

        Assert.True(new PlayerSettingsStore(_settingsDirectory).AutoPlayNext);
    }

    [Fact]
    public void PlayerSettingsStore_ShouldFallBackToTheDefault_WhenTheFileIsCorrupt()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(Path.Combine(_settingsDirectory, "player.json"), "{ not json");

        Assert.True(new PlayerSettingsStore(_settingsDirectory).AutoPlayNext);
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }
    }
}
