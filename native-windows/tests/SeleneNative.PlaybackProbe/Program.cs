using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Dispatching;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using SeleneNative.Services;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;
using CoreState = SeleneNative.Core.Services.MediaPlaybackState;

namespace SeleneNative.PlaybackProbe;

/// <summary>
/// Drives the real <see cref="WindowsMediaPlayer"/> and <see cref="PlayerViewModel"/>
/// against clips rendered on this machine, on a dispatcher thread like the app's
/// UI thread, and reports what the Windows media engine actually does.
/// </summary>
internal static class Program
{
    private const double ClipSeconds = 12;
    private static int _failures;
    private static int _checks;

    private static int Main()
    {
        var controller = DispatcherQueueController.CreateOnDedicatedThread();
        var queue = controller.DispatcherQueue;
        var done = new TaskCompletionSource<int>();
        queue.TryEnqueue(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
            try
            {
                await RunAsync();
                done.SetResult(_failures == 0 ? 0 : 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PROBE CRASHED: {ex}");
                done.SetResult(2);
            }
        });

        var exitCode = done.Task.GetAwaiter().GetResult();
        Console.WriteLine();
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed.");
        return exitCode;
    }

    private static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SeleneNativePlaybackProbe");
        Directory.CreateDirectory(directory);
        Console.WriteLine($"Input: {ClipSeconds:0}-second solid-colour MP4 clips rendered locally into {directory}");

        var clips = new List<string>();
        var colours = new[] { Color.FromArgb(255, 190, 40, 40), Color.FromArgb(255, 40, 90, 190), Color.FromArgb(255, 40, 150, 80) };
        for (var index = 0; index < colours.Length; index++)
        {
            clips.Add(await RenderClipAsync(directory, $"probe-ep{index + 1}.mp4", colours[index]));
        }

        var sourceA = Work("src-a", "a1", clips);
        var sourceB = Work("src-b", "b1", clips);
        var saved = new List<PlayRecord>();

        using var engine = new WindowsMediaPlayer();
        using var vm = new PlayerViewModel(engine)
        {
            RecordSaver = record =>
            {
                saved.Add(record);
                return Task.CompletedTask;
            },
        };
        var native = (MediaPlayer)typeof(WindowsMediaPlayer)
            .GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(engine)!;
        native.IsMuted = true;
        var states = new List<CoreState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.PlaybackState)) states.Add(vm.PlaybackState);
        };

        Section("Opened is not playing");
        vm.ReplaceItem(sourceA.Episodes[0], sourceA, 0);
        Check("state while opening", vm.PlaybackState == CoreState.Opening, vm.PlaybackState);
        Check("not seekable before the item opens", !vm.CanSeek, vm.CanSeek);
        await WaitUntil(() => vm.TotalTime > 0 && vm.PlaybackState != CoreState.Opening);
        await Task.Delay(400);
        Check("opened without a play request stays paused", vm.PlaybackState == CoreState.Paused, vm.PlaybackState);
        Check("never reported Playing", !states.Contains(CoreState.Playing), string.Join(">", states));
        Check("duration known", Math.Abs(vm.TotalTime - ClipSeconds) < 1, $"{vm.TotalTime:0.00}s");
        Check("engine reports seekable", vm.CanSeek, vm.CanSeek);
        Check("status text", vm.StatusText == "已暂停", vm.StatusText);

        Section("Play and pause follow the engine");
        vm.Play();
        Check("playing", await WaitUntil(() => vm.PlaybackState == CoreState.Playing), vm.PlaybackState);
        var before = vm.PlayTime;
        await Task.Delay(1200);
        Check("position advances", vm.PlayTime > before + 0.5, $"{before:0.00}s -> {vm.PlayTime:0.00}s");
        native.Pause();
        Check("pause made on the engine reaches the view model", await WaitUntil(() => vm.PlaybackState == CoreState.Paused), vm.PlaybackState);
        Check("primary action is play again", !vm.IsPlaybackActive, vm.IsPlaybackActive);
        vm.TogglePlayPause();
        Check("toggle resumes", await WaitUntil(() => vm.PlaybackState == CoreState.Playing), vm.PlaybackState);

        Section("Volume and mute");
        native.IsMuted = false;
        vm.SetVolume(0.35);
        Check("volume reaches the engine", Math.Abs(native.Volume - 0.35) < 0.01, native.Volume);
        native.Volume = 0.8;
        Check("volume changed on the engine reaches the view model", await WaitUntil(() => vm.VolumePercent == 80), vm.VolumePercent);
        vm.ToggleMute();
        Check("mute reaches the engine", native.IsMuted, native.IsMuted);
        Check("mute keeps the volume", vm.VolumePercent == 80 && Math.Abs(native.Volume - 0.8) < 0.01, vm.VolumePercent);
        native.IsMuted = false;
        Check("unmute on the engine reaches the view model", await WaitUntil(() => !vm.IsMuted), vm.IsMuted);
        vm.SetMuted(true);

        Section("Playback speed");
        foreach (var rate in PlayerViewModel.PlaybackRates)
        {
            var reason = vm.PlaybackRateUnavailableReason(rate);
            var applied = vm.SetPlaybackRate(rate);
            await Task.Delay(150);
            Check(
                $"{rate}x",
                applied && Math.Abs(native.PlaybackSession.PlaybackRate - rate) < 0.001,
                $"supported={reason is null}, engine={native.PlaybackSession.PlaybackRate}");
        }

        vm.SetPlaybackRate(1.5);

        Section("Seeking");
        Check("seek inside the clip", vm.SeekTo(6) && await WaitUntil(() => Math.Abs(engine.Position - 6) < 1.5), $"{engine.Position:0.00}s");
        vm.SeekBy(-PlayerViewModel.SeekStepSeconds);
        Check("seek back clamps at the start", await WaitUntil(() => engine.Position < 1.5), $"{engine.Position:0.00}s");
        vm.Pause();
        await WaitUntil(() => vm.PlaybackState == CoreState.Paused);
        native.PlaybackSession.Position = TimeSpan.FromSeconds(7);
        Check("seek made on the engine reaches the view model", await WaitUntil(() => Math.Abs(vm.PlayTime - 7) < 0.5), $"{vm.PlayTime:0.00}s");

        Section("Source switch keeps position, pause and speed");
        var loadsBefore = saved.Count;
        var switched = await vm.SwitchSourceAsync(sourceB, provider: null);
        Check("switch accepted", switched && ReferenceEquals(vm.CurrentResult, sourceB), vm.CurrentResult?.Source);
        Check("old source recorded first", saved.Count == loadsBefore + 1 && saved[^1].Source == "src-a" && Math.Abs(saved[^1].PlayTime - 7) < 0.6,
            saved.Count > 0 ? $"{saved[^1].Source} @ {saved[^1].PlayTime:0.00}s" : "none");
        await WaitUntil(() => vm.PendingSeekTime is null && vm.TotalTime > 0);
        await Task.Delay(500);
        Check("stays paused", vm.PlaybackState == CoreState.Paused, vm.PlaybackState);
        Check("position restored", Math.Abs(engine.Position - 7) < 0.6, $"{engine.Position:0.00}s");
        Check("speed kept", Math.Abs(native.PlaybackSession.PlaybackRate - 1.5) < 0.001, native.PlaybackSession.PlaybackRate);

        Section("Episode change keeps speed; resume applies once");
        var seeks = 0;
        engine.SeekCompleted += (_, _) => seeks++;
        vm.Open(sourceA, 1, resumeSeconds: 5);
        Check("resume pending until the item opens", vm.PendingSeekTime is 5, vm.PendingSeekTime);
        Check("playing after resume", await WaitUntil(() => vm.PlaybackState == CoreState.Playing && vm.PendingSeekTime is null), vm.PlaybackState);
        await Task.Delay(700);
        Check("resumed near the saved position", vm.PlayTime is > 4.5 and < 8, $"{vm.PlayTime:0.00}s");
        Check("resume seek applied exactly once", seeks == 1, seeks);
        Check("speed kept across the load", Math.Abs(native.PlaybackSession.PlaybackRate - 1.5) < 0.001, native.PlaybackSession.PlaybackRate);

        Section("Natural end advances once; the last episode finishes");
        vm.SetPlaybackRate(2);
        vm.AutoPlayNext = true;
        Check("advanced to the last episode", await WaitUntil(() => vm.CurrentEpisodeIndex == 2, 15000), vm.CurrentEpisodeIndex);
        Check("previous episode recorded as finished", saved[^1].EpisodeNumber == 2 && saved[^1].PlayTime > ClipSeconds - 1,
            $"ep {saved[^1].EpisodeNumber} @ {saved[^1].PlayTime:0.00}s");
        Check("last episode plays", await WaitUntil(() => vm.PlaybackState == CoreState.Playing), vm.PlaybackState);
        Check("last episode ends", await WaitUntil(() => vm.PlaybackState == CoreState.Ended, 15000), vm.PlaybackState);
        await Task.Delay(800);
        Check("no loop and no further episode", vm.CurrentEpisodeIndex == 2 && vm.PlaybackState == CoreState.Ended, $"index {vm.CurrentEpisodeIndex}, {vm.PlaybackState}");
        Check("status text", vm.StatusText == "播放完毕", vm.StatusText);

        Section("Seeking to the end does not advance");
        vm.PlayEpisode(0);
        await WaitUntil(() => vm.PlaybackState == CoreState.Playing);
        vm.SeekTo(ClipSeconds);
        Check("ended", await WaitUntil(() => vm.PlaybackState == CoreState.Ended), vm.PlaybackState);
        await Task.Delay(800);
        Check("stayed on the episode", vm.CurrentEpisodeIndex == 0, vm.CurrentEpisodeIndex);

        Section("Rapid episode changes keep only the last choice");
        vm.PlayEpisode(1);
        vm.PlayEpisode(2);
        vm.PlayEpisode(1);
        vm.PlayEpisode(0);
        Check("last choice plays", await WaitUntil(() => vm.PlaybackState == CoreState.Playing), vm.PlaybackState);
        await Task.Delay(1000);
        Check("still the last choice", vm.CurrentEpisodeIndex == 0 && vm.CurrentEpisodeUrl == sourceA.Episodes[0], vm.CurrentEpisodeIndex);
        Check("no error from the superseded loads", vm.PlaybackError is null, vm.PlaybackError ?? "none");
        Check("position belongs to the playing item", vm.PlayTime is > 0 and < 6, $"{vm.PlayTime:0.00}s");

        Section("Errors do not advance");
        var broken = Work("src-x", "x1", [new Uri(Path.Combine(directory, "missing.mp4")).AbsoluteUri, clips[0]]);
        vm.Open(broken, 0);
        Check("error reported", await WaitUntil(() => vm.PlaybackState == CoreState.Error && vm.PlaybackError is not null), vm.PlaybackError ?? "none");
        await Task.Delay(800);
        Check("stayed on the failed episode", vm.CurrentEpisodeIndex == 0 && ReferenceEquals(vm.CurrentResult, broken), vm.CurrentEpisodeIndex);
        Check("status text", vm.StatusText == "播放出错", vm.StatusText);

        Section("Close snapshots, stops and stays stopped");
        vm.Open(sourceA, 1);
        await WaitUntil(() => vm.PlaybackState == CoreState.Playing);
        await Task.Delay(900);
        var position = vm.PlayTime;
        await vm.CloseAsync();
        Check("record taken before the stop", saved[^1].Source == "src-a" && saved[^1].EpisodeNumber == 2 && Math.Abs(saved[^1].PlayTime - position) < 0.5,
            $"ep {saved[^1].EpisodeNumber} @ {saved[^1].PlayTime:0.00}s");
        Check("stopped", vm.PlaybackState == CoreState.Stopped && vm.CurrentResult is null, vm.PlaybackState);
        await Task.Delay(800);
        Check("no late engine state after the stop", vm.PlaybackState == CoreState.Stopped && vm.PlayTime == 0, $"{vm.PlaybackState}, {vm.PlayTime:0.00}s");
    }

    private static SearchResult Work(string source, string id, IEnumerable<string> urls)
    {
        var episodes = urls.Select(url => Uri.IsWellFormedUriString(url, UriKind.Absolute) ? url : new Uri(url).AbsoluteUri).ToList();
        return new SearchResult
        {
            Id = id,
            Title = "探针测试片",
            Source = source,
            SourceName = source,
            Episodes = episodes,
            EpisodeTitles = Enumerable.Range(1, episodes.Count).Select(n => $"第{n}集").ToList(),
        };
    }

    private static async Task<string> RenderClipAsync(string directory, string name, Color colour)
    {
        var path = Path.Combine(directory, name);
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(colour, TimeSpan.FromSeconds(ClipSeconds)));
        var folder = await StorageFolder.GetFolderFromPathAsync(directory);
        var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
        profile.Audio = null;
        var reason = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
        if (reason != Windows.Media.Transcoding.TranscodeFailureReason.None)
        {
            throw new InvalidOperationException($"Could not render {name}: {reason}");
        }

        return path;
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMilliseconds = 10000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (condition()) return true;
            await Task.Delay(50);
        }

        return condition();
    }

    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"== {name}");
    }

    private static void Check(string name, bool passed, object? observed)
    {
        _checks++;
        if (!passed) _failures++;
        Console.WriteLine($"  [{(passed ? "PASS" : "FAIL")}] {name}: {observed}");
    }
}
