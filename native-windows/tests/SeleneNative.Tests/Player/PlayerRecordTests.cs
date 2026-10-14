using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

/// <summary>
/// Playback records are snapshotted before the played item changes, written in
/// order, and kept locally when the server cannot be reached.
/// </summary>
public sealed class PlayerRecordTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "SeleneNativePlayerRecordTests", Guid.NewGuid().ToString("N"));

    private static (PlayerViewModel Vm, FakeMediaPlayer Player, List<PlayRecord> Saved) Playing(
        SearchResult? result = null,
        int index = 0,
        params SearchResult[] others)
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var saved = new List<PlayRecord>();
        var vm = new PlayerViewModel(() => player)
        {
            RecordSaver = record =>
            {
                saved.Add(record);
                return Task.CompletedTask;
            },
        };
        vm.Open(result ?? PlayerTestData.Result(source: "src-a", id: "a1", titled: true), index, others);
        player.SimulateOpened();
        return (vm, player, saved);
    }

    [Fact]
    public void EpisodeChange_ShouldSaveTheOldEpisodeBeforeReplacingIt()
    {
        var (vm, player, saved) = Playing(index: 1);
        player.SimulatePosition(250);

        vm.PlayEpisode(3);

        var record = Assert.Single(saved);
        Assert.Equal("src-a+a1", record.Id);
        Assert.Equal(2, record.EpisodeNumber);
        Assert.Equal(250, record.PlayTime);
        Assert.Equal(600, record.TotalTime);

        // The new episode is current, and its own state starts clean.
        Assert.Equal(3, vm.CurrentEpisodeIndex);
        Assert.Equal(0, vm.PlayTime);
    }

    [Fact]
    public async Task SourceSwitch_ShouldSaveTheOldSourceBeforeReplacingIt()
    {
        var a = PlayerTestData.Result(source: "src-a", id: "a1", titled: true);
        var b = PlayerTestData.Result(source: "src-b", id: "b1", titled: true);
        var (vm, player, saved) = Playing(a, 2, b);
        player.SimulatePosition(180);

        await vm.SwitchSourceAsync(b, provider: null);

        var record = Assert.Single(saved);
        Assert.Equal("src-a+a1", record.Id);
        Assert.Equal("src-a", record.Source);
        Assert.Equal(3, record.EpisodeNumber);
        Assert.Equal(180, record.PlayTime);
        Assert.Same(b, vm.CurrentResult);
    }

    [Fact]
    public async Task FailedSourceSwitch_ShouldNotWriteARecord()
    {
        var a = PlayerTestData.Result(source: "src-a", id: "a1", episodes: 5, titled: true);
        var shorter = PlayerTestData.Result(source: "src-b", id: "b1", episodes: 2, titled: true);
        var (vm, _, saved) = Playing(a, 4, shorter);

        Assert.False(await vm.SwitchSourceAsync(shorter, provider: null));

        Assert.Empty(saved);
    }

    [Fact]
    public void OpeningAnotherWork_ShouldSaveThePreviousWorkFirst()
    {
        var (vm, player, saved) = Playing(index: 0);
        player.SimulatePosition(99);

        vm.Open(PlayerTestData.Result(title: "新片", source: "src-z", id: "z9"), 0);

        var record = Assert.Single(saved);
        Assert.Equal("src-a+a1", record.Id);
        Assert.Equal("测试剧", record.Title);
        Assert.Equal(99, record.PlayTime);
        Assert.Equal("新片", vm.CurrentResult?.Title);
    }

    [Fact]
    public async Task Close_ShouldSnapshotBeforeStopping_AndCompleteOnlyAfterTheWrite()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var write = new TaskCompletionSource();
        PlayRecord? captured = null;
        MediaPlaybackState? engineStateAtSnapshot = null;
        var vm = new PlayerViewModel(() => player);
        vm.RecordSaver = record =>
        {
            captured = record;
            engineStateAtSnapshot = player.State;
            return write.Task;
        };
        vm.Open(PlayerTestData.Result(source: "src-a", id: "a1"), 2);
        player.SimulateOpened();
        player.SimulatePosition(432);

        var closing = vm.CloseAsync();

        // The snapshot was taken while the engine still held the item.
        Assert.NotNull(captured);
        Assert.Equal(MediaPlaybackState.Playing, engineStateAtSnapshot);
        Assert.Equal(3, captured.EpisodeNumber);
        Assert.Equal(432, captured.PlayTime);
        Assert.Equal(600, captured.TotalTime);

        // Playback is already stopped and cleared, but closing waits for the write.
        Assert.Equal(MediaPlaybackState.Stopped, player.State);
        Assert.Null(vm.CurrentResult);
        Assert.False(closing.IsCompleted);

        write.SetResult();
        await closing;

        Assert.True(vm.PendingSaves.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Close_ShouldCompleteImmediately_WhenNothingIsPlaying()
    {
        var saved = new List<PlayRecord>();
        var vm = new PlayerViewModel(() => new FakeMediaPlayer())
        {
            RecordSaver = record =>
            {
                saved.Add(record);
                return Task.CompletedTask;
            },
        };

        var closing = vm.CloseAsync();

        Assert.True(closing.IsCompletedSuccessfully);
        await closing;
        Assert.Empty(saved);
    }

    [Fact]
    public async Task Records_ShouldBeWrittenInTheOrderTheyWereTaken()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var firstWrite = new TaskCompletionSource();
        var started = new List<int>();
        var finished = new List<int>();
        var vm = new PlayerViewModel(() => player);
        vm.RecordSaver = async record =>
        {
            started.Add(record.EpisodeNumber);
            if (record.EpisodeNumber == 1)
            {
                await firstWrite.Task;
            }

            finished.Add(record.EpisodeNumber);
        };
        vm.Open(PlayerTestData.Result(), 0);
        player.SimulateOpened();

        vm.PlayEpisode(1);
        player.SimulateOpened();
        var closing = vm.CloseAsync();

        // The second record waits for the slow first write instead of overtaking it.
        Assert.Equal([1], started);
        Assert.Empty(finished);

        firstWrite.SetResult();
        await closing;

        Assert.Equal([1, 2], started);
        Assert.Equal([1, 2], finished);
    }

    [Fact]
    public async Task FailingSaver_ShouldNotBreakPlaybackOrLaterWrites()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 600 };
        var attempts = new List<int>();
        var vm = new PlayerViewModel(() => player);
        vm.RecordSaver = record =>
        {
            attempts.Add(record.EpisodeNumber);
            return record.EpisodeNumber == 1
                ? Task.FromException(new IOException("磁盘已满"))
                : Task.CompletedTask;
        };
        vm.Open(PlayerTestData.Result(), 0);
        player.SimulateOpened();

        vm.PlayEpisode(1);
        player.SimulateOpened();
        await vm.CloseAsync();

        Assert.Equal([1, 2], attempts);
        Assert.Null(vm.PlaybackError);
    }

    [Fact]
    public void ItemThatNeverOpened_ShouldNotOverwriteTheRecord()
    {
        var (vm, player, saved) = Playing(index: 0);
        player.SimulatePosition(300);

        // Episode 4 is requested but fails to open; the viewer goes elsewhere.
        vm.PlayEpisode(3);
        player.SimulateError("打不开");
        vm.PlayEpisode(4);

        var record = Assert.Single(saved);
        Assert.Equal(1, record.EpisodeNumber);
        Assert.Equal(300, record.PlayTime);
    }

    [Fact]
    public async Task UnappliedResumePosition_ShouldBeKeptInTheRecord()
    {
        var player = new FakeMediaPlayer { LengthSeconds = 0 };
        var saved = new List<PlayRecord>();
        var vm = new PlayerViewModel(() => player)
        {
            RecordSaver = record =>
            {
                saved.Add(record);
                return Task.CompletedTask;
            },
        };
        var detail = PlayerTestData.Result(source: "src-a", id: "a1");
        var provider = new StubContentProvider { OnDetail = (_, _) => Task.FromResult<SearchResult?>(detail) };
        var history = new PlayRecord
        {
            Id = "src-a+a1",
            Source = "src-a",
            Title = detail.Title,
            EpisodeNumber = 2,
            PlayTime = 1500,
            TotalTime = 2700,
        };
        await vm.LoadDetailAndPlayAsync(history, provider);

        // The viewer leaves while the item is still loading.
        await vm.CloseAsync();

        var record = Assert.Single(saved);
        Assert.Equal(2, record.EpisodeNumber);
        Assert.Equal(1500, record.PlayTime);
        Assert.Equal(2700, record.TotalTime);
    }

    [Fact]
    public async Task Persistence_ShouldKeepTheLocalCopy_WhenTheServerFails()
    {
        var store = new PlayRecordStore(Path.Combine(_directory, "playrecords.json"));
        var persistence = new PlayRecordPersistence(store);
        var provider = new StubContentProvider
        {
            OnSavePlayRecord = _ => Task.FromException(new HttpRequestException("503")),
        };
        var record = NewRecord(episode: 4, playTime: 321);

        await persistence.SaveAsync(record, provider);

        var local = Assert.Single(await store.LoadAsync());
        Assert.Equal("src-a+a1", local.Id);
        Assert.Equal(4, local.EpisodeNumber);
        Assert.Equal(321, local.PlayTime);
        Assert.Empty(provider.SavedRecords);
    }

    [Fact]
    public async Task Persistence_ShouldWriteLocally_WhenSignedOut()
    {
        var store = new PlayRecordStore(Path.Combine(_directory, "playrecords.json"));

        await new PlayRecordPersistence(store).SaveAsync(NewRecord(episode: 1, playTime: 10), provider: null);

        Assert.Single(await store.LoadAsync());
    }

    [Fact]
    public async Task Persistence_ShouldWriteLocalBeforeTheServer_AndReplaceTheSameWork()
    {
        var store = new PlayRecordStore(Path.Combine(_directory, "playrecords.json"));
        var persistence = new PlayRecordPersistence(store);
        var localCountSeenByServer = -1;
        var provider = new StubContentProvider
        {
            OnSavePlayRecord = async _ => localCountSeenByServer = (await store.LoadAsync()).Count,
        };

        await persistence.SaveAsync(NewRecord(episode: 1, playTime: 10), provider);
        await persistence.SaveAsync(NewRecord(episode: 2, playTime: 20), provider);

        Assert.Equal(1, localCountSeenByServer);
        var local = Assert.Single(await store.LoadAsync());
        Assert.Equal(2, local.EpisodeNumber);
        Assert.Equal(2, provider.SavedRecords.Count);
    }

    [Fact]
    public async Task Persistence_ShouldStillReachTheServer_WhenTheLocalWriteFails()
    {
        var provider = new StubContentProvider();

        await new PlayRecordPersistence(new ThrowingStore()).SaveAsync(NewRecord(episode: 3, playTime: 5), provider);

        Assert.Equal(3, Assert.Single(provider.SavedRecords).EpisodeNumber);
    }

    private static PlayRecord NewRecord(int episode, double playTime) => new()
    {
        Id = "src-a+a1",
        Source = "src-a",
        Title = "测试剧",
        EpisodeNumber = episode,
        TotalEpisodes = 5,
        PlayTime = playTime,
        TotalTime = 600,
        SaveTime = DateTimeOffset.UtcNow,
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class ThrowingStore : IPlayRecordStore
    {
        public Task<IReadOnlyList<PlayRecord>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PlayRecord>>([]);

        public Task SaveAsync(PlayRecord record, CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("磁盘已满"));

        public Task SaveAllAsync(IEnumerable<PlayRecord> records, CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("磁盘已满"));
    }
}
