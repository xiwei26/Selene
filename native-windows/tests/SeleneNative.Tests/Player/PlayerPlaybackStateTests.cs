using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

/// <summary>Engine state, volume, speed, seeking and resume as seen through the coordinator.</summary>
public sealed class PlayerPlaybackStateTests
{
    private static (PlayerViewModel Vm, FakeMediaPlayer Player) Playing(double length = 600, int episodes = 5)
    {
        var player = new FakeMediaPlayer { LengthSeconds = length };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(episodes: episodes), 0);
        player.SimulateOpened();
        return (vm, player);
    }

    // ---- real engine state ----

    [Fact]
    public void OpenedMedia_ShouldNotBeReportedAsPlaying_UntilTheEnginePlays()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/a.m3u8", PlayerTestData.Result(), 0);

        Assert.Equal(MediaPlaybackState.Opening, vm.PlaybackState);
        Assert.True(vm.IsLoading);
        Assert.Equal("正在加载", vm.StatusText);

        // Opened without a play request: ready, but paused.
        player.SimulateOpened();

        Assert.Equal(MediaPlaybackState.Paused, vm.PlaybackState);
        Assert.False(vm.IsPlaybackActive);
        Assert.False(vm.IsLoading);
        Assert.Equal(600, vm.TotalTime);
        Assert.Equal("已暂停", vm.StatusText);

        vm.Play();

        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
        Assert.True(vm.IsPlaybackActive);
        Assert.Equal("正在播放", vm.StatusText);
    }

    [Fact]
    public void PlaybackState_ShouldNotify_ForEveryEngineTransition()
    {
        var (vm, player) = Playing(episodes: 1);
        var states = new List<MediaPlaybackState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.PlaybackState)) states.Add(vm.PlaybackState);
        };

        // The engine is driven directly, as native transport controls do.
        player.Pause();
        player.SimulateState(MediaPlaybackState.Buffering);
        player.Play();
        player.SimulateEnded();

        Assert.Equal(
            [MediaPlaybackState.Paused, MediaPlaybackState.Buffering, MediaPlaybackState.Playing, MediaPlaybackState.Ended],
            states);
    }

    [Fact]
    public void StatusText_ShouldFollowBufferingErrorAndEnd()
    {
        var (vm, player) = Playing(episodes: 1);

        player.SimulateState(MediaPlaybackState.Buffering);
        Assert.Equal("正在缓冲", vm.StatusText);
        Assert.True(vm.IsLoading);
        Assert.True(vm.IsPlaybackActive);

        player.SimulateError("网络中断");
        Assert.Equal("播放出错", vm.StatusText);
        Assert.Equal("网络中断", vm.PlaybackError);
        Assert.False(vm.IsLoading);

        vm.Retry();
        player.SimulateOpened();
        Assert.Null(vm.PlaybackError);
        Assert.Equal("正在播放", vm.StatusText);

        player.SimulateEnded();
        Assert.Equal("播放完毕", vm.StatusText);
        Assert.False(vm.IsPlaybackActive);
    }

    [Fact]
    public void PauseMadeByNativeControls_ShouldBeToggledBackToPlaying()
    {
        var (vm, player) = Playing();

        player.Pause();
        Assert.False(vm.IsPlaybackActive);

        vm.TogglePlayPause();

        Assert.Equal(MediaPlaybackState.Playing, player.State);
    }

    // ---- volume and mute ----

    [Theory]
    [InlineData(0.35, 0.35, 35)]
    [InlineData(1.7, 1.0, 100)]
    [InlineData(-0.2, 0.0, 0)]
    public void SetVolume_ShouldClampToEngineRange(double requested, double expected, int percent)
    {
        var (vm, player) = Playing();

        vm.SetVolume(requested);

        Assert.Equal(expected, player.Volume, 3);
        Assert.Equal(expected, vm.Volume, 3);
        Assert.Equal(percent, vm.VolumePercent);
    }

    [Fact]
    public void AdjustVolume_ShouldStepByFivePercentAndStayInRange()
    {
        var (vm, player) = Playing();
        vm.SetVolume(0.5);

        vm.AdjustVolume(PlayerViewModel.VolumeStep);
        Assert.Equal(55, vm.VolumePercent);

        vm.AdjustVolume(-PlayerViewModel.VolumeStep);
        vm.AdjustVolume(-PlayerViewModel.VolumeStep);
        Assert.Equal(45, vm.VolumePercent);

        vm.SetVolume(0.98);
        vm.AdjustVolume(PlayerViewModel.VolumeStep);
        Assert.Equal(1.0, player.Volume, 3);

        vm.SetVolume(0.02);
        vm.AdjustVolume(-PlayerViewModel.VolumeStep);
        Assert.Equal(0.0, player.Volume, 3);
    }

    [Fact]
    public void Mute_ShouldKeepTheVolume_AndUnmuteShouldRestoreIt()
    {
        var (vm, player) = Playing();
        vm.SetVolume(0.4);

        vm.ToggleMute();

        Assert.True(player.IsMuted);
        Assert.True(vm.IsMuted);
        Assert.Equal(0.4, vm.Volume, 3);
        Assert.Equal(40, vm.VolumePercent);

        vm.ToggleMute();

        Assert.False(player.IsMuted);
        Assert.False(vm.IsMuted);
        Assert.Equal(0.4, player.Volume, 3);
    }

    [Fact]
    public void Unmute_ShouldRestoreTheLastAudibleVolume_WhenVolumeWasTurnedDownToZero()
    {
        var (vm, player) = Playing();
        vm.SetVolume(0.6);
        vm.SetVolume(0);
        vm.SetMuted(true);

        vm.SetMuted(false);

        Assert.False(player.IsMuted);
        Assert.Equal(0.6, player.Volume, 3);
        Assert.Equal(60, vm.VolumePercent);
    }

    [Fact]
    public void RaisingTheVolumeWhileMuted_ShouldUnmute()
    {
        var (vm, player) = Playing();
        vm.SetVolume(0.3);
        vm.SetMuted(true);

        vm.AdjustVolume(PlayerViewModel.VolumeStep);

        Assert.False(vm.IsMuted);
        Assert.Equal(35, vm.VolumePercent);

        vm.SetMuted(true);
        vm.AdjustVolume(-PlayerViewModel.VolumeStep);

        // Lowering does not unmute.
        Assert.True(vm.IsMuted);
        Assert.Equal(30, vm.VolumePercent);
    }

    [Fact]
    public void VolumeChangedByNativeControls_ShouldReachTheViewModel()
    {
        var (vm, player) = Playing();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        player.Volume = 0.25;
        player.IsMuted = true;

        Assert.Equal(0.25, vm.Volume, 3);
        Assert.Equal(25, vm.VolumePercent);
        Assert.True(vm.IsMuted);
        Assert.Contains(nameof(PlayerViewModel.Volume), notified);
        Assert.Contains(nameof(PlayerViewModel.VolumePercent), notified);
        Assert.Contains(nameof(PlayerViewModel.IsMuted), notified);
    }

    // ---- playback speed ----

    [Fact]
    public void PlaybackRates_ShouldOfferExactlyTheSupportedSteps()
    {
        Assert.Equal([0.5, 0.75, 1, 1.25, 1.5, 2], PlayerViewModel.PlaybackRates);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void SetPlaybackRate_ShouldApplyEveryOfferedStep(double rate)
    {
        var (vm, player) = Playing();

        Assert.True(vm.SetPlaybackRate(rate));

        Assert.Equal(rate, player.PlaybackRate);
        Assert.Equal(rate, vm.PlaybackRate);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    [InlineData(1.1)]
    public void SetPlaybackRate_ShouldRejectRatesThatAreNotOffered(double rate)
    {
        var (vm, player) = Playing();

        Assert.False(vm.SetPlaybackRate(rate));

        Assert.Equal(1, player.PlaybackRate);
        Assert.Equal(1, vm.PlaybackRate);
        Assert.NotNull(vm.PlaybackRateUnavailableReason(rate));
    }

    [Fact]
    public void SetPlaybackRate_ShouldExplainAndRefuse_WhenTheMediaCannotPlayThatRate()
    {
        var (vm, player) = Playing();
        player.UnsupportedRates.Add(2);

        Assert.Null(vm.PlaybackRateUnavailableReason(1.5));
        Assert.Equal("当前视频不支持该倍速", vm.PlaybackRateUnavailableReason(2));
        Assert.False(vm.SetPlaybackRate(2));
        Assert.Equal(1, player.PlaybackRate);
    }

    [Fact]
    public void PlaybackRate_ShouldSurviveAnEpisodeChange_EvenThoughTheEngineResetsIt()
    {
        var (vm, player) = Playing();
        vm.SetPlaybackRate(1.5);

        vm.PlayNextEpisode();

        // Loading the next item drops the engine back to normal speed.
        Assert.Equal(1, player.PlaybackRate);

        player.SimulateOpened();

        Assert.Equal(1.5, player.PlaybackRate);
        Assert.Equal(1.5, vm.PlaybackRate);
    }

    [Fact]
    public void PlaybackRateChosenBeforeTheMediaOpens_ShouldApplyOnceItOpens()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0);

        Assert.True(vm.SetPlaybackRate(1.25));
        Assert.Equal(1, player.PlaybackRate);

        player.SimulateOpened();

        Assert.Equal(1.25, player.PlaybackRate);
        Assert.Equal(1.25, vm.PlaybackRate);
    }

    [Fact]
    public void PlaybackRateChangedByTheEngine_ShouldBeShownAndKeptForLaterEpisodes()
    {
        var (vm, player) = Playing();

        player.PlaybackRate = 2;
        Assert.Equal(2, vm.PlaybackRate);

        vm.PlayNextEpisode();
        player.SimulateOpened();

        Assert.Equal(2, player.PlaybackRate);
    }

    // ---- seeking ----

    [Fact]
    public void CanSeek_ShouldComeFromTheEngine_NotFromTheDuration()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600, Seekable = false };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0);

        // A known duration is not seekability.
        Assert.False(vm.CanSeek);

        player.SimulateOpened();

        Assert.Equal(600, vm.TotalTime);
        Assert.False(vm.CanSeek);
        Assert.False(vm.SeekTo(120));
        Assert.False(vm.SeekBy(PlayerViewModel.SeekStepSeconds));
        Assert.Empty(player.Seeks);
        Assert.Equal(0, vm.PlayTime);
    }

    [Fact]
    public void CanSeek_ShouldBeFalse_UntilTheMediaIsOpened()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0);

        Assert.False(vm.CanSeek);
        Assert.False(vm.SeekTo(30));

        player.SimulateOpened();

        Assert.True(vm.CanSeek);
    }

    [Fact]
    public void SeekBy_ShouldMoveTenSecondsAndClampAtBothEnds()
    {
        var (vm, player) = Playing(length: 100);
        player.SimulatePosition(50);

        Assert.True(vm.SeekBy(PlayerViewModel.SeekStepSeconds));
        Assert.Equal(60, player.Position, 3);

        Assert.True(vm.SeekBy(-PlayerViewModel.SeekStepSeconds));
        Assert.Equal(50, player.Position, 3);

        player.SimulatePosition(4);
        vm.SeekBy(-PlayerViewModel.SeekStepSeconds);
        Assert.Equal(0, player.Position, 3);

        player.SimulatePosition(96);
        vm.SeekBy(PlayerViewModel.SeekStepSeconds);
        Assert.Equal(100, player.Position, 3);
        Assert.Equal(100, vm.PlayTime, 3);
    }

    // ---- resume ----

    [Fact]
    public void Resume_ShouldWaitForALateDuration_AndApplyExactlyOnce()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 0 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0, resumeSeconds: 240);

        // Opened and playing, but the duration is still unknown.
        player.SimulateOpened();
        player.SimulatePosition(1);

        Assert.Equal(240, vm.PendingSeekTime);
        Assert.Empty(player.Seeks);

        player.SimulateDuration(600);

        Assert.Null(vm.PendingSeekTime);
        Assert.Equal([240], player.Seeks);
        Assert.Equal(240, vm.PlayTime);

        // Later notifications must not seek again.
        player.SimulateDuration(600);
        player.SimulatePosition(241);
        player.Pause();
        player.Play();

        Assert.Equal([240], player.Seeks);
    }

    [Fact]
    public void Resume_ShouldStayPending_WhileTheMediaCannotSeek()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600, Seekable = false };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0, resumeSeconds: 240);

        player.SimulateOpened();
        player.SimulatePosition(2);

        Assert.Equal(240, vm.PendingSeekTime);
        Assert.Empty(player.Seeks);

        // The engine becomes seekable later (for example once enough is buffered).
        player.Seekable = true;
        player.SimulateDuration(600);

        Assert.Null(vm.PendingSeekTime);
        Assert.Equal([240], player.Seeks);
    }

    [Fact]
    public void Resume_ShouldBeAppliedWhenTheMediaOpensPaused()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.ReplaceItem("https://example.com/a.m3u8", PlayerTestData.Result(), 0);
        vm.PendingSeekTime = 90;

        player.SimulateOpened();

        Assert.Equal(MediaPlaybackState.Paused, vm.PlaybackState);
        Assert.Null(vm.PendingSeekTime);
        Assert.Equal([90], player.Seeks);
    }

    [Fact]
    public void NewWork_ShouldNotInheritThePreviousResumePosition()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(title: "旧片", id: "old"), 0, resumeSeconds: 300);

        // The first work never became ready; a different work is opened.
        vm.Open(PlayerTestData.Result(title: "新片", id: "new"), 0);

        Assert.Null(vm.PendingSeekTime);

        player.SimulateOpened();
        player.SimulatePosition(1);

        Assert.Empty(player.Seeks);
        Assert.Equal(1, vm.PlayTime);
    }

    [Fact]
    public void EpisodeChange_ShouldNotInheritThePreviousResumePosition()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0, resumeSeconds: 300);

        vm.PlayEpisode(1);
        player.SimulateOpened();

        Assert.Null(vm.PendingSeekTime);
        Assert.Empty(player.Seeks);
    }

    [Fact]
    public void Retry_ShouldKeepTheEpisodeAndRestoreThePosition()
    {
        var (vm, player) = Playing();
        vm.PlayEpisode(2);
        player.SimulateOpened();
        player.SimulatePosition(123);
        player.SimulateError("解码失败");

        vm.Retry();

        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.Equal(123, vm.PendingSeekTime);
        Assert.Null(vm.PlaybackError);
        Assert.Equal(player.LoadedUrls[^2], player.LoadedUrls[^1]);

        player.SimulateOpened();

        Assert.Null(vm.PendingSeekTime);
        Assert.Equal(123, player.Position);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
    }

    [Fact]
    public void Retry_ShouldKeepAResumePositionThatWasNeverApplied()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(PlayerTestData.Result(), 0, resumeSeconds: 300);
        player.SimulateError("打不开");

        vm.Retry();
        player.SimulateOpened();

        Assert.Equal([300], player.Seeks);
    }
}
