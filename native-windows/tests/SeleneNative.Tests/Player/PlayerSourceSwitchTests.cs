using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

/// <summary>Switching between sources of one work, and requests that finish out of order.</summary>
public sealed class PlayerSourceSwitchTests
{
    private static SearchResult SourceA(int episodes = 5) =>
        PlayerTestData.Result(source: "src-a", id: "a1", episodes: episodes, titled: true);

    private static SearchResult SourceB(int episodes = 5) =>
        PlayerTestData.Result(source: "src-b", id: "b1", episodes: episodes, titled: true);

    private static (PlayerViewModel Vm, FakeMediaPlayer Player) PlayingEpisode(
        SearchResult result,
        int index,
        params SearchResult[] others)
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        vm.Open(result, index, others);
        player.SimulateOpened();
        return (vm, player);
    }

    [Fact]
    public void Open_ShouldKeepOnlySameWorkCandidates()
    {
        var current = PlayerTestData.Result(title: "庆余年 第二季", source: "src-a", id: "a1", year: "2024");
        var sameWork = PlayerTestData.Result(title: "庆余年第2季", source: "src-b", id: "b1", year: "2024");
        var otherSeason = PlayerTestData.Result(title: "庆余年 第一季", source: "src-b", id: "b0", year: "2019");
        var otherYear = PlayerTestData.Result(title: "庆余年 第二季", source: "src-c", id: "c1", year: "2021");
        var unrelated = PlayerTestData.Result(title: "庆余年之帝王业", source: "src-d", id: "d1");
        var duplicate = PlayerTestData.Result(title: "庆余年 第二季", source: "src-b", id: "b1", year: "2024");
        var vm = new PlayerViewModel(() => new FakeMediaPlayer());

        vm.Open(current, 0, [current, sameWork, otherSeason, otherYear, unrelated, duplicate]);

        Assert.Equal([current, sameWork], vm.SourceCandidates);
    }

    [Fact]
    public async Task SwitchSource_ShouldPlayTheSameEpisodeAtTheSamePosition()
    {
        var a = SourceA();
        var b = SourceB();
        var (vm, player) = PlayingEpisode(a, 2, b);
        player.SimulatePosition(321);

        var switched = await vm.SwitchSourceAsync(b, provider: null);

        Assert.True(switched);
        Assert.Same(b, vm.CurrentResult);
        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.Equal(b.Episodes[2], vm.CurrentEpisodeUrl);
        Assert.Equal(b.Episodes[2], player.LoadedUrls[^1]);
        Assert.Equal(321, vm.PendingSeekTime);
        Assert.False(vm.IsSwitchingSource);
        Assert.Null(vm.Notice);

        player.SimulateOpened();

        Assert.Equal(321, player.Position);
        Assert.Null(vm.PendingSeekTime);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
        Assert.Equal([a, b], vm.SourceCandidates);
    }

    [Fact]
    public async Task SwitchSource_ShouldStayPaused_WhenPlaybackWasPaused()
    {
        var a = SourceA();
        var b = SourceB();
        var (vm, player) = PlayingEpisode(a, 1, b);
        player.SimulatePosition(200);
        vm.Pause();

        Assert.True(await vm.SwitchSourceAsync(b, provider: null));

        Assert.False(player.PlayRequested);
        Assert.False(vm.IsPlaybackActive);

        player.SimulateOpened();

        Assert.Equal(MediaPlaybackState.Paused, vm.PlaybackState);
        Assert.Equal(200, player.Position);
        Assert.Null(vm.PendingSeekTime);
    }

    [Fact]
    public async Task SwitchSource_ShouldKeepThePlaybackRate()
    {
        var a = SourceA();
        var b = SourceB();
        var (vm, player) = PlayingEpisode(a, 0, b);
        vm.SetPlaybackRate(1.25);

        await vm.SwitchSourceAsync(b, provider: null);
        player.SimulateOpened();

        Assert.Equal(1.25, player.PlaybackRate);
        Assert.Equal(1.25, vm.PlaybackRate);
    }

    [Fact]
    public async Task SwitchSource_ShouldMatchByEpisodeName_WhenTheSourcesOrderEpisodesDifferently()
    {
        var a = SourceA(episodes: 3);
        var b = SourceB(episodes: 3);
        b.Episodes.Reverse();
        b.EpisodeTitles.Reverse();
        var (vm, player) = PlayingEpisode(a, 0, b);

        Assert.True(await vm.SwitchSourceAsync(b, provider: null));

        // "第1集" is the last entry of the reversed source.
        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.EndsWith("/ep1.m3u8", player.LoadedUrls[^1]);
        Assert.Equal("第1集", vm.CurrentEpisodeTitle);
    }

    [Fact]
    public async Task SwitchSource_ShouldKeepTheCurrentSource_WhenTheTargetLacksTheEpisode()
    {
        var a = SourceA(episodes: 5);
        var b = SourceB(episodes: 3);
        var (vm, player) = PlayingEpisode(a, 4, b);
        player.SimulatePosition(77);

        var switched = await vm.SwitchSourceAsync(b, provider: null);

        Assert.False(switched);
        Assert.Same(a, vm.CurrentResult);
        Assert.Equal(4, vm.CurrentEpisodeIndex);
        Assert.Equal(a.Episodes[4], vm.CurrentEpisodeUrl);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
        Assert.Equal(77, vm.PlayTime);
        Assert.Contains("第5集", vm.Notice);
        Assert.Contains("已保留当前来源", vm.Notice);
        Assert.False(vm.IsSwitchingSource);
    }

    [Fact]
    public async Task SwitchSource_ShouldLoadDetailOnDemand_WhenTheCandidateHasNoEpisodes()
    {
        var a = SourceA();
        var stub = new SearchResult { Id = "b1", Source = "src-b", SourceName = "src-b", Title = a.Title };
        var detail = SourceB();
        var provider = new StubContentProvider { OnDetail = (_, _) => Task.FromResult<SearchResult?>(detail) };
        var (vm, player) = PlayingEpisode(a, 3, stub);

        Assert.Empty(provider.DetailRequests);

        Assert.True(await vm.SwitchSourceAsync(stub, provider));

        Assert.Equal(("src-b", "b1"), Assert.Single(provider.DetailRequests));
        Assert.Same(detail, vm.CurrentResult);
        Assert.Equal(3, vm.CurrentEpisodeIndex);
        Assert.Equal(detail.Episodes[3], player.LoadedUrls[^1]);

        // The loaded detail replaces the placeholder, so it is not fetched again.
        Assert.Equal([a, detail], vm.SourceCandidates);
    }

    [Fact]
    public async Task SwitchSource_ShouldKeepTheCurrentSource_WhenDetailFails()
    {
        var a = SourceA();
        var stub = new SearchResult { Id = "b1", Source = "src-b", SourceName = "备用线路", Title = a.Title };
        var provider = new StubContentProvider
        {
            OnDetail = (_, _) => Task.FromException<SearchResult?>(new HttpRequestException("连接超时")),
        };
        var (vm, player) = PlayingEpisode(a, 2, stub);
        player.SimulatePosition(150);

        var switched = await vm.SwitchSourceAsync(stub, provider);

        Assert.False(switched);
        Assert.Same(a, vm.CurrentResult);
        Assert.Equal(2, vm.CurrentEpisodeIndex);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
        Assert.Equal(150, vm.PlayTime);
        Assert.Contains("备用线路", vm.Notice);
        Assert.Contains("连接超时", vm.Notice);
        Assert.Null(vm.PlaybackError);
        Assert.False(vm.IsSwitchingSource);
    }

    [Fact]
    public async Task SwitchSource_ShouldKeepTheCurrentSource_WhenDetailIsEmpty()
    {
        var a = SourceA();
        var stub = new SearchResult { Id = "b1", Source = "src-b", SourceName = "src-b", Title = a.Title };
        var provider = new StubContentProvider();
        var (vm, player) = PlayingEpisode(a, 0, stub);

        Assert.False(await vm.SwitchSourceAsync(stub, provider));

        Assert.Same(a, vm.CurrentResult);
        Assert.Single(player.LoadedUrls);
        Assert.NotNull(vm.Notice);
    }

    [Fact]
    public async Task SwitchSource_ToTheCurrentSource_ShouldDoNothing()
    {
        var a = SourceA();
        var (vm, player) = PlayingEpisode(a, 1);

        Assert.True(await vm.SwitchSourceAsync(a, provider: null));

        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
    }

    [Fact]
    public async Task StaleSourceSwitch_ShouldNotOverrideANewerEpisodeChoice()
    {
        var a = SourceA();
        var stub = new SearchResult { Id = "b1", Source = "src-b", SourceName = "src-b", Title = a.Title };
        var detail = new TaskCompletionSource<SearchResult?>();
        var provider = new StubContentProvider { OnDetail = (_, _) => detail.Task };
        var (vm, player) = PlayingEpisode(a, 0, stub);

        var pendingSwitch = vm.SwitchSourceAsync(stub, provider);
        Assert.True(vm.IsSwitchingSource);

        // While the detail is loading the viewer picks another episode.
        vm.PlayEpisode(3);
        Assert.False(vm.IsSwitchingSource);

        detail.SetResult(SourceB());

        Assert.False(await pendingSwitch);
        Assert.Same(a, vm.CurrentResult);
        Assert.Equal(3, vm.CurrentEpisodeIndex);
        Assert.Equal(a.Episodes[3], player.LoadedUrls[^1]);
        Assert.Equal(2, player.LoadedUrls.Count);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task StaleSourceSwitch_ShouldNotRestartPlayback_AfterThePlayerWasClosed()
    {
        var a = SourceA();
        var stub = new SearchResult { Id = "b1", Source = "src-b", SourceName = "src-b", Title = a.Title };
        var detail = new TaskCompletionSource<SearchResult?>();
        var provider = new StubContentProvider { OnDetail = (_, _) => detail.Task };
        var (vm, player) = PlayingEpisode(a, 0, stub);

        var pendingSwitch = vm.SwitchSourceAsync(stub, provider);
        await vm.CloseAsync();
        detail.SetResult(SourceB());

        Assert.False(await pendingSwitch);
        Assert.Null(vm.CurrentResult);
        Assert.Null(vm.CurrentEpisodeUrl);
        Assert.Single(player.LoadedUrls);
        Assert.Equal(MediaPlaybackState.Stopped, player.State);
    }

    [Fact]
    public async Task OlderOfTwoSourceSwitches_ShouldLose()
    {
        var a = SourceA();
        var stubB = new SearchResult { Id = "b1", Source = "src-b", SourceName = "src-b", Title = a.Title };
        var stubC = new SearchResult { Id = "c1", Source = "src-c", SourceName = "src-c", Title = a.Title };
        var detailB = new TaskCompletionSource<SearchResult?>();
        var detailC = new TaskCompletionSource<SearchResult?>();
        var provider = new StubContentProvider
        {
            OnDetail = (source, _) => source == "src-b" ? detailB.Task : detailC.Task,
        };
        var (vm, player) = PlayingEpisode(a, 1, stubB, stubC);
        var resultC = PlayerTestData.Result(source: "src-c", id: "c1", titled: true);

        var first = vm.SwitchSourceAsync(stubB, provider);
        var second = vm.SwitchSourceAsync(stubC, provider);

        // The newer request finishes first; the older one finishes afterwards.
        detailC.SetResult(resultC);
        detailB.SetResult(SourceB());

        Assert.True(await second);
        Assert.False(await first);
        Assert.Same(resultC, vm.CurrentResult);
        Assert.Equal(resultC.Episodes[1], player.LoadedUrls[^1]);
        Assert.Equal(2, player.LoadedUrls.Count);
        Assert.False(vm.IsSwitchingSource);
    }

    [Fact]
    public async Task StaleHistoryResume_ShouldNotOverrideANewerPlayRequest()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        var detail = new TaskCompletionSource<SearchResult?>();
        var provider = new StubContentProvider { OnDetail = (_, _) => detail.Task };
        var record = new PlayRecord
        {
            Id = "src-a+a1",
            Source = "src-a",
            Title = "旧记录",
            EpisodeNumber = 2,
            PlayTime = 400,
            SearchTitle = "旧记录",
        };

        var resume = vm.LoadDetailAndPlayAsync(record, provider);

        // Before the history detail arrives the viewer starts something else.
        var other = PlayerTestData.Result(title: "新片", source: "src-z", id: "z9");
        vm.Open(other, 0);
        detail.SetResult(SourceA());
        await resume;

        Assert.Same(other, vm.CurrentResult);
        Assert.Equal(0, vm.CurrentEpisodeIndex);
        Assert.Equal(other.Episodes[0], player.LoadedUrls[^1]);
        Assert.Single(player.LoadedUrls);
        Assert.Null(vm.PendingSeekTime);
        Assert.Null(vm.PlaybackError);
    }

    [Fact]
    public async Task StaleHistoryResumeFailure_ShouldNotReportAnErrorOnTheNewerItem()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        var detail = new TaskCompletionSource<SearchResult?>();
        var provider = new StubContentProvider { OnDetail = (_, _) => detail.Task };
        var record = new PlayRecord { Id = "src-a+a1", Source = "src-a", Title = "旧记录", EpisodeNumber = 1 };

        var resume = vm.LoadDetailAndPlayAsync(record, provider);
        vm.Open(PlayerTestData.Result(title: "新片", id: "z9"), 0);
        detail.SetException(new HttpRequestException("服务端错误"));
        await resume;

        Assert.Null(vm.PlaybackError);
        Assert.Equal("新片", vm.CurrentResult?.Title);
    }

    [Fact]
    public async Task HistoryResume_ShouldFindOtherSourcesLazily_AndOnlyOnce()
    {
        var a = SourceA();
        var b = SourceB();
        var otherWork = PlayerTestData.Result(title: "完全不同的片", source: "src-c", id: "c1");
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var vm = new PlayerViewModel(() => player);
        var provider = new StubContentProvider
        {
            OnDetail = (_, _) => Task.FromResult<SearchResult?>(a),
            OnSearch = _ => Task.FromResult<IReadOnlyList<SearchResult>>([a, b, otherWork]),
        };
        var record = new PlayRecord { Id = "src-a+a1", Source = "src-a", Title = a.Title, EpisodeNumber = 1 };

        await vm.LoadDetailAndPlayAsync(record, provider);

        // Resuming from history does not search until sources are asked for.
        Assert.Empty(provider.SearchQueries);
        Assert.Equal([a], vm.SourceCandidates);

        await vm.LoadSourceCandidatesAsync(provider);
        await vm.LoadSourceCandidatesAsync(provider);

        Assert.Equal(a.Title, Assert.Single(provider.SearchQueries));
        Assert.Equal([a, b], vm.SourceCandidates);
        Assert.False(vm.IsLoadingSources);
    }

    [Fact]
    public async Task CandidateSearch_ShouldNotLeakIntoADifferentWork()
    {
        var a = SourceA();
        var search = new TaskCompletionSource<IReadOnlyList<SearchResult>>();
        var provider = new StubContentProvider { OnSearch = _ => search.Task };
        var (vm, _) = PlayingEpisode(a, 0);

        var loading = vm.LoadSourceCandidatesAsync(provider);
        Assert.True(vm.IsLoadingSources);

        var other = PlayerTestData.Result(title: "新片", source: "src-z", id: "z9");
        vm.Open(other, 0);
        search.SetResult([a, SourceB()]);
        await loading;

        Assert.Equal([other], vm.SourceCandidates);
        Assert.False(vm.IsLoadingSources);
    }

    [Fact]
    public async Task CandidateSearchFailure_ShouldLeavePlaybackAlone()
    {
        var a = SourceA();
        var provider = new StubContentProvider
        {
            OnSearch = _ => Task.FromException<IReadOnlyList<SearchResult>>(new HttpRequestException("超时")),
        };
        var (vm, player) = PlayingEpisode(a, 0);

        await vm.LoadSourceCandidatesAsync(provider);

        Assert.Equal([a], vm.SourceCandidates);
        Assert.Contains("超时", vm.Notice);
        Assert.Null(vm.PlaybackError);
        Assert.Equal(MediaPlaybackState.Playing, vm.PlaybackState);
        Assert.Single(player.LoadedUrls);
        Assert.False(vm.IsLoadingSources);
    }
}
