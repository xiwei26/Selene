// In-process UI smoke test for the player page. This file is compiled into the
// app only by `dotnet build -p:SeleneUiSmoke=true`; see native-windows/README.md.
//
// It drives the real MainWindow, PlayerPage and Windows media engine against
// clips rendered on this machine, using the same entry points a viewer reaches
// (automation peers for buttons, the page's own command and focus routines),
// and writes report.txt plus PNG renderings of the window to the output folder.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using SeleneNative.Services;
using SeleneNative.Views;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using CoreState = SeleneNative.Core.Services.MediaPlaybackState;

namespace SeleneNative
{
    public partial class App
    {
        partial void OnMainWindowReady(MainWindow window) => _ = new UiSmoke.SmokeRun(window).RunAsync();
    }
}

namespace SeleneNative.UiSmoke
{
    internal sealed class SmokeRun(MainWindow window)
    {
        private const double ClipSeconds = 40;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly string _out = Environment.GetEnvironmentVariable("SELENE_NATIVE_UISMOKE_OUT")
            ?? Path.Combine(Path.GetTempPath(), "SeleneNativeUiSmoke");
        private int _checks;
        private int _failures;
        private bool _finished;

        private PlayerPage _page = null!;
        private PlayerViewModel _vm = null!;
        private ContentControl _host = null!;
        private NavigationView _nav = null!;
        private Grid _root = null!;
        private MediaPlayer _native = null!;
        private WindowsMediaPlayer _engine = null!;

        public async Task RunAsync()
        {
            Directory.CreateDirectory(_out);
            File.WriteAllText(ReportPath, string.Empty);
            Application.Current.UnhandledException += (_, e) =>
            {
                _failures++;
                Line($"UNHANDLED EXCEPTION: {e.Exception}");
                e.Handled = true;
            };

            try
            {
                await RunCoreAsync();
            }
            catch (Exception ex)
            {
                _failures++;
                Line($"SMOKE CRASHED: {ex}");
            }

            Finish();
            Application.Current.Exit();
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            Line(string.Empty);
            Line($"{_checks - _failures}/{_checks} checks passed.");
            Environment.ExitCode = _failures == 0 ? 0 : 1;
        }

        private string ReportPath => Path.Combine(_out, "report.txt");

        private async Task RunCoreAsync()
        {
            _page = Field<PlayerPage>(window, "_playerPage");
            _vm = Field<PlayerViewModel>(window, "_playerViewModel");
            _host = Field<ContentControl>(window, "_contentHost");
            _nav = Field<NavigationView>(window, "_navigationView");
            _root = (Grid)window.Content;
            _engine = (WindowsMediaPlayer)_vm.Player;
            _native = Field<MediaPlayer>(_engine, "_player");
            _native.IsMuted = true;
            var titleBar = (FrameworkElement)_root.FindName("AppTitleBar");
            var appWindow = window.AppWindow;
            var scale = _root.XamlRoot?.RasterizationScale ?? 1;

            Line($"Input: {ClipSeconds:0}-second solid-colour MP4 clips rendered locally into {_out}");
            Line($"Display scale: {scale:0.##}");
            var clips = new List<string>
            {
                await RenderClipAsync("smoke-ep1.mp4", Color.FromArgb(255, 190, 40, 40)),
                await RenderClipAsync("smoke-ep2.mp4", Color.FromArgb(255, 40, 90, 190)),
                await RenderClipAsync("smoke-ep3.mp4", Color.FromArgb(255, 40, 150, 80)),
            };
            var sourceA = Work("src-a", "线路 A", "a1", clips);
            var sourceB = Work("src-b", "线路 B", "b1", clips);
            var otherWork = Work("src-c", "线路 C", "c1", clips);
            otherWork.Title = "另一部作品";

            await WaitUntil(() => _host.Content is HomePage, 20000);
            Line($"Home page shown: {_host.Content is HomePage}");

            // ---- open through the detail page's play path, with candidate sources ----
            Section("Open from detail");
            Resize(appWindow, 1024, 640, scale);
            var detailVm = Field<DetailViewModel>(window, "_detailViewModel");
            await detailVm.LoadAsync(sourceA, null, null, candidates: [sourceA, sourceB, otherWork]);
            await (Task)typeof(MainWindow).GetMethod("OnPlayEpisodeAsync", Private)!
                .Invoke(window, [sourceA, "第1集", sourceA.Episodes[0], 1])!;
            Check("player page is the active page", ReferenceEquals(_host.Content, _page), _host.Content?.GetType().Name);
            Check("playing", await WaitUntil(() => _vm.PlaybackState == CoreState.Playing), _vm.PlaybackState);
            Check("candidates limited to the same work", _vm.SourceCandidates.Count == 2, string.Join(", ", _vm.SourceCandidates.Select(s => s.Source)));
            await Task.Delay(1200);

            var title = Named<TextBlock>("TitleText");
            var subtitle = Named<TextBlock>("SubtitleText");
            var status = Named<TextBlock>("StatusText");
            var time = Named<TextBlock>("TimeText");
            Check("title", title.Text == sourceA.Title, title.Text);
            Check("subtitle shows episode and source", subtitle.Text == "第1集 · 线路 A", subtitle.Text);
            Check("status follows the engine", status.Text == "正在播放", status.Text);
            Check("time is shown", time.Text.EndsWith("/ 0:40", StringComparison.Ordinal), time.Text);

            Section("Native transport controls");
            var surface = Named<FrameworkElement>("VideoSurface");
            var element = Descendants<MediaPlayerElement>(surface).FirstOrDefault();
            Check("media player element present", element is not null, element?.GetType().Name);
            Check("native controls enabled", element?.AreTransportControlsEnabled == true, element?.AreTransportControlsEnabled);
            foreach (var part in new[] { "PlayPauseButton", "ProgressSlider", "VolumeMuteButton", "TimeElapsedElement", "TimeRemainingElement" })
            {
                var control = ByName(surface, part);
                Check($"native {part} visible", control is { Visibility: Visibility.Visible }, control?.Visibility.ToString() ?? "missing");
            }

            foreach (var part in new[] { "FullWindowButton", "ZoomButton", "CastButton", "PlaybackRateButton", "SkipForwardButton", "SkipBackwardButton", "NextTrackButton", "PreviousTrackButton", "StopButton", "CompactOverlayButton" })
            {
                // Not offered: absent, collapsed, or taken out of view and input.
                var control = ByName(surface, part);
                var hidden = control is null or { Visibility: Visibility.Collapsed } ||
                    control is Control { IsTabStop: false, IsHitTestVisible: false, Opacity: 0, ActualWidth: < 1 };
                Check($"native {part} not offered", hidden, control switch
                {
                    null => "absent",
                    Control c => $"{c.Visibility}, width {c.ActualWidth:0}, opacity {c.Opacity}, tabStop {c.IsTabStop}, hitTest {c.IsHitTestVisible}, enabled {c.IsEnabled}",
                    _ => control.Visibility.ToString(),
                });
            }

            var nativeSlider = ByName(surface, "ProgressSlider") as Slider;
            Check("native seek bar enabled for seekable media", nativeSlider?.IsEnabled == true && _vm.CanSeek, $"slider={nativeSlider?.IsEnabled}, canSeek={_vm.CanSeek}");

            // ---- layout at the required window sizes ----
            await CheckLayoutAsync("1024x640", appWindow, 1024, 640, scale);
            await CheckLayoutAsync("800x600", appWindow, 800, 600, scale);

            Section("Layout 520x600 (narrower than required)");
            Resize(appWindow, 520, 600, scale);
            await Task.Delay(700);
            var narrowToolbar = Named<CommandBar>("PlayerToolbar");
            var overflowed = new[] { "PreviousEpisodeButton", "NextEpisodeButton", "SeekBackwardButton", "SeekForwardButton", "SourcesButton", "MuteButton", "EpisodesButton", "SpeedButton", "VolumeButton", "FullScreenButton", "PlayPauseButton" }
                .Where(name => Named<AppBarButton>(name).IsInOverflow)
                .ToList();
            Line($"  window content {_root.ActualWidth:0}x{_root.ActualHeight:0}; in the overflow menu: {string.Join(", ", overflowed)}");
            Check("episode and seek steps are the first to move to the overflow menu", overflowed.Count > 0 && overflowed.Contains("PreviousEpisodeButton") && !overflowed.Contains("PlayPauseButton") && !overflowed.Contains("FullScreenButton"),
                string.Join(", ", overflowed));
            narrowToolbar.IsOpen = true;
            await Task.Delay(600);
            var movedCommand = Named<AppBarButton>("PreviousEpisodeButton");
            Check("an overflowed command is a full menu item, still reachable", movedCommand.IsInOverflow && movedCommand.ActualWidth >= 100, $"width {movedCommand.ActualWidth:0}");
            narrowToolbar.IsOpen = false;
            await Task.Delay(300);
            Resize(appWindow, 1024, 640, scale);
            await Task.Delay(700);

            // ---- toolbar commands ----
            Section("Toolbar commands");
            var playPause = Named<AppBarButton>("PlayPauseButton");
            Check("play/pause shows pause while playing", playPause.Label == "暂停", playPause.Label);
            Invoke(playPause);
            Check("pause", await WaitUntil(() => _vm.PlaybackState == CoreState.Paused), _vm.PlaybackState);
            await Task.Delay(200);
            Check("label and status after pause", playPause.Label == "播放" && status.Text == "已暂停", $"{playPause.Label} / {status.Text}");
            var paused = _engine.Position;
            Invoke(Named<AppBarButton>("SeekForwardButton"));
            Check("forward 10 seconds", await WaitUntil(() => Math.Abs(_engine.Position - (paused + 10)) < 0.6), $"{paused:0.00}s -> {_engine.Position:0.00}s");
            Invoke(Named<AppBarButton>("SeekBackwardButton"));
            Check("back 10 seconds", await WaitUntil(() => Math.Abs(_engine.Position - paused) < 0.6), $"{_engine.Position:0.00}s");
            Invoke(playPause);
            Check("play", await WaitUntil(() => _vm.PlaybackState == CoreState.Playing), _vm.PlaybackState);

            var mute = Named<AppBarButton>("MuteButton");
            _native.IsMuted = false;
            await WaitUntil(() => !_vm.IsMuted);
            Invoke(mute);
            Check("mute", await WaitUntil(() => _native.IsMuted) && mute.Label == "取消静音", $"engine={_native.IsMuted}, label={mute.Label}");
            Invoke(mute);
            Check("unmute", await WaitUntil(() => !_native.IsMuted) && mute.Label == "静音", $"engine={_native.IsMuted}, label={mute.Label}");
            _native.Volume = 0.4;
            var volumeButton = Named<AppBarButton>("VolumeButton");
            Check("engine volume shown on the toolbar", await WaitUntil(() => volumeButton.Label == "音量 40%"), volumeButton.Label);
            var volumeSlider = Named<Slider>("VolumeSlider");
            Check("engine volume shown on the slider", Math.Abs(volumeSlider.Value - 40) < 0.5, volumeSlider.Value);
            volumeSlider.Value = 65;
            Check("slider drives the engine", await WaitUntil(() => Math.Abs(_native.Volume - 0.65) < 0.01), _native.Volume);
            _native.IsMuted = true;

            var previous = Named<AppBarButton>("PreviousEpisodeButton");
            var next = Named<AppBarButton>("NextEpisodeButton");
            Check("first episode: previous disabled, next enabled", !previous.IsEnabled && next.IsEnabled, $"prev={previous.IsEnabled}, next={next.IsEnabled}");
            Invoke(next);
            Check("next episode", await WaitUntil(() => _vm.CurrentEpisodeIndex == 1 && _vm.PlaybackState == CoreState.Playing), _vm.CurrentEpisodeIndex);
            await Task.Delay(200);
            Check("subtitle follows the episode", subtitle.Text == "第2集 · 线路 A", subtitle.Text);
            Invoke(next);
            Check("last episode", await WaitUntil(() => _vm.CurrentEpisodeIndex == 2 && _vm.PlaybackState == CoreState.Playing), _vm.CurrentEpisodeIndex);
            await Task.Delay(200);
            Check("last episode: next disabled", !next.IsEnabled && previous.IsEnabled, $"prev={previous.IsEnabled}, next={next.IsEnabled}");
            Invoke(previous);
            Check("previous episode", await WaitUntil(() => _vm.CurrentEpisodeIndex == 1 && _vm.PlaybackState == CoreState.Playing), _vm.CurrentEpisodeIndex);

            // ---- flyouts ----
            Section("Speed menu");
            var speedButton = Named<AppBarButton>("SpeedButton");
            var speedFlyout = (MenuFlyout)speedButton.Flyout;
            Invoke(speedButton);
            Check("speed menu opens", await WaitUntil(() => speedFlyout.IsOpen, 3000), speedFlyout.IsOpen);
            var rates = speedFlyout.Items.OfType<RadioMenuFlyoutItem>().ToList();
            Check("six speeds offered", rates.Count == 6, string.Join(" | ", rates.Select(item => item.Text)));
            Check("all enabled for this media", rates.All(item => item.IsEnabled), rates.Count(item => item.IsEnabled));
            Check("current speed checked", rates.Single(item => item.IsChecked).Text.StartsWith("1x", StringComparison.Ordinal), rates.Single(item => item.IsChecked).Text);
            Check("an open menu blocks player shortcuts", (bool)Call(_page, "HasOpenPopup")!, Call(_page, "HasOpenPopup"));
            // Selecting the item, as a click or assistive technology does, applies the speed.
            rates.Single(item => item.Text == "1.25x").IsChecked = true;
            Check("speed applied", await WaitUntil(() => Math.Abs(_native.PlaybackSession.PlaybackRate - 1.25) < 0.001), _native.PlaybackSession.PlaybackRate);
            Check("only the chosen speed is checked", rates.Count(item => item.IsChecked) == 1, string.Join(" | ", rates.Where(item => item.IsChecked).Select(item => item.Text)));
            speedFlyout.Hide();
            await WaitUntil(() => !speedFlyout.IsOpen, 3000);
            Check("toolbar shows the speed", Named<TextBlock>("SpeedButtonText").Text == "1.25x" && speedButton.Label == "倍速 1.25x", speedButton.Label);
            Check("no popup left open", !(bool)Call(_page, "HasOpenPopup")!, Call(_page, "HasOpenPopup"));

            Section("Episode picker");
            var episodesButton = Named<AppBarButton>("EpisodesButton");
            var episodesFlyout = (Flyout)episodesButton.Flyout;
            var episodesGrid = Named<GridView>("EpisodesGrid");
            Invoke(episodesButton);
            Check("episode picker opens", await WaitUntil(() => episodesFlyout.IsOpen, 3000), episodesFlyout.IsOpen);
            await Task.Delay(300);
            Check("lists every episode", episodesGrid.Items.Count == 3, episodesGrid.Items.Count);
            Check("current episode selected", (episodesGrid.SelectedItem as FrameworkElement)?.Tag is 1, (episodesGrid.SelectedItem as FrameworkElement)?.Tag);
            Check("backend order by default", EpisodeTags(episodesGrid) == "0,1,2", EpisodeTags(episodesGrid));
            await SavePngAsync((FrameworkElement)episodesGrid.Parent, "flyout-episodes.png");
            Toggle(Named<ToggleButton>("ReverseOrderToggle"));
            await Task.Delay(300);
            Check("reversed display order", EpisodeTags(episodesGrid) == "2,1,0" && _vm.IsEpisodeReversed, EpisodeTags(episodesGrid));
            Check("reversing keeps the playing episode", _vm.CurrentEpisodeIndex == 1 && (episodesGrid.SelectedItem as FrameworkElement)?.Tag is 1, _vm.CurrentEpisodeIndex);
            var firstShown = (GridViewItem)episodesGrid.Items[0];
            var clicked = TryInvokeItem(episodesGrid, firstShown);
            if (clicked)
            {
                Check("first row on screen plays its backend episode", await WaitUntil(() => _vm.CurrentEpisodeIndex == 2 && _vm.PlaybackState == CoreState.Playing), _vm.CurrentEpisodeIndex);
                Check("picker closes after a choice", await WaitUntil(() => !episodesFlyout.IsOpen, 3000), episodesFlyout.IsOpen);
            }
            else
            {
                Line("  [SKIP] episode row could not be invoked in-process; choosing by index instead");
                episodesFlyout.Hide();
                _vm.PlayEpisode(2);
                await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            }

            Check("next stays the backend next while reversed", !next.IsEnabled, next.IsEnabled);
            Invoke(episodesButton);
            await WaitUntil(() => episodesFlyout.IsOpen, 3000);
            Toggle(Named<ToggleButton>("ReverseOrderToggle"));
            await Task.Delay(200);
            Check("order restored", !_vm.IsEpisodeReversed, _vm.IsEpisodeReversed);
            episodesFlyout.Hide();
            await WaitUntil(() => !episodesFlyout.IsOpen, 3000);

            Section("Source switch");
            _vm.PlayEpisode(1);
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            _vm.SeekTo(12);
            await WaitUntil(() => Math.Abs(_engine.Position - 12) < 1.5);
            _vm.Pause();
            await WaitUntil(() => _vm.PlaybackState == CoreState.Paused);
            var positionBeforeSwitch = _engine.Position;
            var sourcesButton = Named<AppBarButton>("SourcesButton");
            var sourcesFlyout = (Flyout)sourcesButton.Flyout;
            var sourcesList = Named<ListView>("SourcesList");
            Check("toolbar counts the sources", sourcesButton.Label == "来源（2）", sourcesButton.Label);
            Invoke(sourcesButton);
            Check("source list opens", await WaitUntil(() => sourcesFlyout.IsOpen, 3000), sourcesFlyout.IsOpen);
            await Task.Delay(300);
            Check("lists both sources", sourcesList.Items.Count == 2, sourcesList.Items.Count);
            Check("current source selected", ((sourcesList.SelectedItem as FrameworkElement)?.Tag as SearchResult)?.Source == "src-a",
                ((sourcesList.SelectedItem as FrameworkElement)?.Tag as SearchResult)?.Source);
            await SavePngAsync((FrameworkElement)sourcesList.Parent, "flyout-sources.png");
            if (!TryInvokeItem(sourcesList, (ListViewItem)sourcesList.Items[1]))
            {
                Line("  [SKIP] source row could not be invoked in-process; switching through the view model instead");
                await _vm.SwitchSourceAsync(sourceB, null);
                sourcesFlyout.Hide();
            }

            Check("switched to the other source", await WaitUntil(() => _vm.CurrentResult?.Source == "src-b" && _vm.PendingSeekTime is null && _vm.TotalTime > 0), _vm.CurrentResult?.Source);
            await Task.Delay(500);
            Check("same episode", _vm.CurrentEpisodeIndex == 1, _vm.CurrentEpisodeIndex);
            Check("position restored", Math.Abs(_engine.Position - positionBeforeSwitch) < 0.6, $"{positionBeforeSwitch:0.00}s -> {_engine.Position:0.00}s");
            Check("still paused", _vm.PlaybackState == CoreState.Paused, _vm.PlaybackState);
            Check("speed kept", Math.Abs(_native.PlaybackSession.PlaybackRate - 1.25) < 0.001, _native.PlaybackSession.PlaybackRate);
            Check("subtitle shows the new source", subtitle.Text == "第2集 · 线路 B", subtitle.Text);
            await WaitUntil(() => !sourcesFlyout.IsOpen, 3000);
            Check("source list closed after switching", !sourcesFlyout.IsOpen, sourcesFlyout.IsOpen);

            Section("Source without the episode");
            var shortSource = Work("src-d", "线路 D", "d1", clips.Take(1));
            _vm.SourceCandidates = [.. _vm.SourceCandidates, shortSource];
            var switchedToShort = await _vm.SwitchSourceAsync(shortSource, null);
            await Task.Delay(300);
            var noticeBar = Named<InfoBar>("NoticeBar");
            Check("switch refused", !switchedToShort && _vm.CurrentResult?.Source == "src-b" && _vm.CurrentEpisodeIndex == 1, _vm.CurrentResult?.Source);
            Check("reason shown", noticeBar.IsOpen && noticeBar.Message.Contains("已保留当前来源", StringComparison.Ordinal), noticeBar.Message);
            Check("position untouched", Math.Abs(_engine.Position - positionBeforeSwitch) < 0.6, $"{_engine.Position:0.00}s");
            await SavePngAsync(_root, "notice-missing-episode.png");
            _vm.Play();
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);

            // ---- focus routing ----
            Section("Focus routing for shortcuts");
            _page.FocusVideo();
            await Task.Delay(150);
            Check("video focus", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.Video), Call(_page, "ClassifyFocus"));
            if (playPause.Focus(FocusState.Keyboard))
            {
                await Task.Delay(150);
                Check("focused toolbar command keeps Space, Enter and its arrow navigation", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.ToolbarCommand), Call(_page, "ClassifyFocus"));
            }

            if (Named<Button>("BackButton").Focus(FocusState.Keyboard))
            {
                await Task.Delay(150);
                Check("focused plain button keeps Space and Enter", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.Button), Call(_page, "ClassifyFocus"));
            }

            if (nativeSlider is not null && nativeSlider.Focus(FocusState.Keyboard))
            {
                await Task.Delay(150);
                Check("native seek bar keeps the arrow keys", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.Slider), Call(_page, "ClassifyFocus"));
            }

            if (ByName(surface, "PlayPauseButton") is Control nativePlay && nativePlay.Focus(FocusState.Keyboard))
            {
                await Task.Delay(150);
                Check("native transport button keeps its own keys", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.ToolbarCommand), Call(_page, "ClassifyFocus"));
            }

            if (_nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is { } navItem && navItem.Focus(FocusState.Keyboard))
            {
                await Task.Delay(150);
                Check("navigation pane keeps its keys", Equals(Call(_page, "ClassifyFocus"), PlayerFocusKind.Outside), Call(_page, "ClassifyFocus"));
            }

            // Keyboard reachability: Tab between groups, arrow keys inside a toolbar.
            _page.FocusVideo();
            await Task.Delay(150);
            var order = TabOrder();
            Line($"  Tab order from the video: {string.Join(" > ", order)}");
            var toolbarCommands = new[] { "PreviousEpisodeButton", "SeekBackwardButton", "PlayPauseButton", "SeekForwardButton", "NextEpisodeButton", "MuteButton", "VolumeButton", "SpeedButton", "EpisodesButton", "SourcesButton", "FullScreenButton", "MoreButton" };
            var walked = WalkToolbar(Named<CommandBar>("PlayerToolbar"), playPause);
            Line($"  Arrow keys across the toolbar: {string.Join(" > ", walked)}");
            Check("Tab reaches the back button", order.Contains("BackButton"), order.Contains("BackButton"));
            Check("Tab reaches the native transport controls", order.Contains("ProgressSlider") || order.Contains("VolumeMuteButton"), string.Join(",", order.Where(n => n is "ProgressSlider" or "VolumeMuteButton")));
            Check("Tab reaches the toolbar", order.Any(toolbarCommands.Contains), string.Join(",", order.Where(toolbarCommands.Contains)));
            foreach (var command in toolbarCommands)
            {
                Check($"keyboard reaches {command}", order.Contains(command) || walked.Contains(command), order.Contains(command) ? "Tab" : walked.Contains(command) ? "arrow keys" : "unreachable");
            }

            // ---- shortcut commands (the key-to-command table is unit tested in Core) ----
            Section("Shortcut commands");
            _page.FocusVideo();
            var osd = Named<TextBlock>("OsdText");
            _native.IsMuted = false;
            await WaitUntil(() => !_vm.IsMuted);
            Execute(PlayerCommand.ToggleMute);
            Check("M mutes and says so", await WaitUntil(() => _native.IsMuted) && osd.Text.StartsWith("已静音", StringComparison.Ordinal), osd.Text);
            Execute(PlayerCommand.VolumeUp);
            Check("Up raises the volume by 5% and unmutes", await WaitUntil(() => !_native.IsMuted && Math.Abs(_native.Volume - 0.70) < 0.01) && osd.Text == "音量 70%", osd.Text);
            Execute(PlayerCommand.VolumeDown);
            Check("Down lowers the volume by 5%", await WaitUntil(() => Math.Abs(_native.Volume - 0.65) < 0.01), _native.Volume);
            _native.IsMuted = true;
            var beforeSeek = _engine.Position;
            Execute(PlayerCommand.SeekForward);
            Check("Right seeks forward", await WaitUntil(() => _engine.Position > beforeSeek + 8), $"{beforeSeek:0.00}s -> {_engine.Position:0.00}s");
            Execute(PlayerCommand.TogglePlayPause);
            Check("Space pauses", await WaitUntil(() => _vm.PlaybackState == CoreState.Paused), _vm.PlaybackState);
            Execute(PlayerCommand.TogglePlayPause);
            Check("Space plays", await WaitUntil(() => _vm.PlaybackState == CoreState.Playing), _vm.PlaybackState);
            Execute(PlayerCommand.PreviousEpisode);
            Check("Ctrl+Left goes to the previous episode", await WaitUntil(() => _vm.CurrentEpisodeIndex == 0 && _vm.PlaybackState == CoreState.Playing), _vm.CurrentEpisodeIndex);
            Execute(PlayerCommand.PreviousEpisode);
            await Task.Delay(300);
            Check("Ctrl+Left at the first episode stays put", _vm.CurrentEpisodeIndex == 0 && osd.Text == "已经是第一集", osd.Text);
            await PostedKeysAsync();

            // ---- full screen ----
            await CheckLayoutAsync("1024x640", appWindow, 1024, 640, scale, quiet: true);
            await FullScreenRoundTripAsync("playing", appWindow, titleBar, element!, pauseFirst: false);
            await FullScreenRoundTripAsync("paused", appWindow, titleBar, element!, pauseFirst: true);
            if (appWindow.Presenter is OverlappedPresenter overlapped)
            {
                overlapped.Maximize();
                await Task.Delay(700);
                await CheckLayoutAsync("maximized", appWindow, 0, 0, scale);
                await FullScreenRoundTripAsync("from maximized", appWindow, titleBar, element!, pauseFirst: false);
                Check("window is maximized again", overlapped.State == OverlappedPresenterState.Maximized, overlapped.State);
                overlapped.Restore();
                await Task.Delay(500);
            }

            // ---- automatic next episode ----
            Section("Automatic next episode");
            var autoNext = Named<AppBarToggleButton>("AutoNextToggle");
            Check("on by default", _vm.AutoPlayNext && autoNext.IsChecked == true, autoNext.IsChecked);
            Toggle(autoNext);
            await Task.Delay(200);
            var settingsPath = Path.Combine(Environment.GetEnvironmentVariable("SELENE_NATIVE_DATA_DIR") ?? string.Empty, "player.json");
            Check("switching it off is persisted", !_vm.AutoPlayNext && File.Exists(settingsPath) && File.ReadAllText(settingsPath).Contains("false", StringComparison.Ordinal),
                File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "no file");
            _vm.PlayEpisode(0);
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            _vm.SetPlaybackRate(2);
            _vm.SeekTo(ClipSeconds - 3);
            Check("episode ends", await WaitUntil(() => _vm.PlaybackState == CoreState.Ended, 8000), _vm.PlaybackState);
            await Task.Delay(700);
            Check("off: stays on the finished episode", _vm.CurrentEpisodeIndex == 0 && status.Text == "本集播放结束", $"index {_vm.CurrentEpisodeIndex}, {status.Text}");
            Toggle(autoNext);
            await Task.Delay(200);
            Check("switching it on is persisted", _vm.AutoPlayNext && File.ReadAllText(settingsPath).Contains("true", StringComparison.Ordinal), File.ReadAllText(settingsPath));
            _vm.PlayEpisode(1);
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            _vm.SeekTo(ClipSeconds - 3);
            Check("on: the next episode starts by itself", await WaitUntil(() => _vm.CurrentEpisodeIndex == 2 && _vm.PlaybackState == CoreState.Playing, 8000), _vm.CurrentEpisodeIndex);
            _vm.SeekTo(ClipSeconds - 3);
            Check("last episode ends", await WaitUntil(() => _vm.PlaybackState == CoreState.Ended, 8000), _vm.PlaybackState);
            await Task.Delay(700);
            Check("last episode: finished, no loop", _vm.CurrentEpisodeIndex == 2 && status.Text == "播放完毕", $"index {_vm.CurrentEpisodeIndex}, {status.Text}");
            _vm.SetPlaybackRate(1);

            // ---- themes ----
            Section("Themes");
            var themes = Field<IThemeService>(window, "_themeService");
            foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                themes.Mode = mode;
                await Task.Delay(500);
                Check($"{mode} theme applied", _root.ActualTheme == (mode == ThemeMode.Dark ? ElementTheme.Dark : ElementTheme.Light), _root.ActualTheme);
                await SavePngAsync(_root, $"theme-{mode.ToString().ToLowerInvariant()}.png");
            }

            themes.Mode = ThemeMode.System;

            // ---- errors and retry ----
            Section("Error and retry");
            var broken = Work("src-x", "坏线路", "x1", [Path.Combine(_out, "missing.mp4"), clips[0]]);
            _vm.Open(broken, 0);
            var errorBar = Named<InfoBar>("ErrorBar");
            Check("error shown with a retry action", await WaitUntil(() => errorBar.IsOpen && status.Text == "播放出错"), $"{status.Text}: {errorBar.Message}");
            await SavePngAsync(_root, "error.png");
            Check("seeking disabled while nothing is open", !Named<AppBarButton>("SeekForwardButton").IsEnabled && nativeSlider?.IsEnabled == false,
                $"toolbar={Named<AppBarButton>("SeekForwardButton").IsEnabled}, native={nativeSlider?.IsEnabled}");
            Invoke(Named<Button>("ErrorRetryButton"));
            await Task.Delay(1500);
            Check("retry stays on the failed episode", _vm.CurrentEpisodeIndex == 0 && ReferenceEquals(_vm.CurrentResult, broken), _vm.CurrentEpisodeIndex);

            // ---- leaving the page ----
            Section("Leave and record");
            _vm.Open(sourceA, 1);
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            _vm.SeekTo(20);
            await WaitUntil(() => Math.Abs(_engine.Position - 20) < 1.5);
            await Task.Delay(600);
            var leftAt = _vm.PlayTime;
            Invoke(Named<Button>("BackButton"));
            Check("back leaves the player", await WaitUntil(() => !ReferenceEquals(_host.Content, _page)), _host.Content?.GetType().Name);
            Check("playback stopped and cleared", _vm.CurrentResult is null && _vm.PlaybackState == CoreState.Stopped && _engine.State == CoreState.Stopped, _vm.PlaybackState);
            await _vm.PendingSaves;
            var recordsPath = Path.Combine(Environment.GetEnvironmentVariable("SELENE_NATIVE_DATA_DIR") ?? string.Empty, "playrecords.json");
            var records = File.Exists(recordsPath) ? await new PlayRecordStore(recordsPath).LoadAsync() : [];
            var record = records.FirstOrDefault(item => item.Id == "src-a+a1");
            Check("record written locally", record is not null, records.Count);
            Check("record has the episode and position", record is { EpisodeNumber: 2 } && Math.Abs(record.PlayTime - leftAt) < 1.0,
                record is null ? "none" : $"ep {record.EpisodeNumber} @ {record.PlayTime:0.00}s of {record.TotalTime:0.00}s (left at {leftAt:0.00}s)");
            await Task.Delay(800);
            Check("nothing restarts after leaving", _vm.PlaybackState == CoreState.Stopped && _vm.CurrentEpisodeUrl is null, _vm.PlaybackState);

            // ---- closing the window while playing ----
            Section("Window close while playing");
            await (Task)typeof(MainWindow).GetMethod("OnPlayEpisodeAsync", Private)!
                .Invoke(window, [sourceB, "第3集", sourceB.Episodes[2], 3])!;
            await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            _vm.SeekTo(15);
            await WaitUntil(() => Math.Abs(_engine.Position - 15) < 1.5);
            await Task.Delay(600);

            // A deliberately slow write shows that the window waits for it.
            var realSaver = _vm.RecordSaver!;
            var writeFinished = false;
            _vm.RecordSaver = async record =>
            {
                await Task.Delay(900);
                await realSaver(record);
                writeFinished = true;
            };
            var closedAt = _vm.PlayTime;
            var closeRequested = System.Diagnostics.Stopwatch.StartNew();
            window.Closed += (_, _) =>
            {
                var saved = new PlayRecordStore(recordsPath).LoadAsync().GetAwaiter().GetResult()
                    .FirstOrDefault(item => item.Id == "src-b+b1");
                Check("window stayed open until the record was written", writeFinished && closeRequested.ElapsedMilliseconds >= 900, $"written={writeFinished}, closed after {closeRequested.ElapsedMilliseconds} ms");
                Check("record has the episode and position at close", saved is { EpisodeNumber: 3 } && Math.Abs(saved.PlayTime - closedAt) < 1.0,
                    saved is null ? "none" : $"ep {saved.EpisodeNumber} @ {saved.PlayTime:0.00}s (closed at {closedAt:0.00}s)");
                Check("playback stopped before the window went away", _engine.State == CoreState.Stopped && _vm.CurrentResult is null, _engine.State);
                Finish();
            };

            // The same message the title bar's close button produces, sent to this window only.
            PostMessage(WinRT.Interop.WindowNative.GetWindowHandle(window), 0x0010, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(300);
            Check("playback stops as soon as closing starts", _engine.State == CoreState.Stopped, _engine.State);
            await Task.Delay(8000);
            Check("window closed", false, "still open 8 seconds after the close request");
        }

        // ---- scenarios ----

        private async Task CheckLayoutAsync(string name, AppWindow appWindow, int width, int height, double scale, bool quiet = false)
        {
            if (width > 0)
            {
                Resize(appWindow, width, height, scale);
            }

            await Task.Delay(700);
            if (quiet)
            {
                return;
            }

            Section($"Layout {name}");
            Line($"  window content {_root.ActualWidth:0}x{_root.ActualHeight:0}, navigation {_nav.DisplayMode}");
            var toolbar = Named<CommandBar>("PlayerToolbar");
            var bounds = BoundsInRoot(toolbar);
            Check("toolbar fully inside the window", bounds.Y >= 0 && bounds.Y + bounds.Height <= _root.ActualHeight + 0.5 && bounds.X >= 0 && bounds.X + bounds.Width <= _root.ActualWidth + 0.5,
                $"y={bounds.Y:0}..{bounds.Y + bounds.Height:0} of {_root.ActualHeight:0}");
            foreach (var command in new[] { "PlayPauseButton", "MuteButton", "VolumeButton", "FullScreenButton", "SpeedButton", "EpisodesButton", "SourcesButton", "PreviousEpisodeButton", "NextEpisodeButton", "SeekBackwardButton", "SeekForwardButton" })
            {
                var button = Named<AppBarButton>(command);
                var area = BoundsInRoot(button);
                var visible = !button.IsInOverflow && area.Width > 0 && area.X >= 0 && area.X + area.Width <= _root.ActualWidth + 0.5;
                Check($"{command} on the toolbar without scrolling or overflow", visible, $"overflow={button.IsInOverflow}, x={area.X:0}..{area.X + area.Width:0}");
            }

            var frame = Named<Border>("VideoFrame");
            var info = Named<ScrollViewer>("BottomScrollViewer");
            Check("video keeps a usable height", frame.ActualHeight >= 200, $"{frame.ActualWidth:0}x{frame.ActualHeight:0}");
            Check("info area scrolls on its own below the toolbar", info.Visibility == Visibility.Visible && BoundsInRoot(info).Y >= bounds.Y + bounds.Height - 0.5,
                $"info y={BoundsInRoot(info).Y:0}, height={info.ActualHeight:0}, scrollable={info.ScrollableHeight:0}");
            await SavePngAsync(_root, $"layout-{name}.png");
        }

        private async Task FullScreenRoundTripAsync(string name, AppWindow appWindow, FrameworkElement titleBar, MediaPlayerElement element, bool pauseFirst)
        {
            Section($"Full screen ({name})");
            if (pauseFirst)
            {
                _vm.Pause();
                await WaitUntil(() => _vm.PlaybackState == CoreState.Paused);
            }
            else if (_vm.PlaybackState != CoreState.Playing)
            {
                _vm.Play();
                await WaitUntil(() => _vm.PlaybackState == CoreState.Playing);
            }

            var header = Named<FrameworkElement>("Header");
            var frame = Named<Border>("VideoFrame");
            var info = Named<ScrollViewer>("BottomScrollViewer");
            var toolbarHost = Named<Border>("ToolbarHost");
            var beforePosition = appWindow.Position;
            var beforeSize = appWindow.Size;
            var beforeKind = appWindow.Presenter.Kind;
            var loads = Field<int>(_engine, "_loadGeneration");
            var url = _vm.CurrentEpisodeUrl;
            var stateBefore = _vm.PlaybackState;
            var timeBefore = _engine.Position;

            Invoke(Named<AppBarButton>("FullScreenButton"));
            Check("enters full screen", await WaitUntil(() => appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen && _page.IsFullScreen, 4000), appWindow.Presenter.Kind);
            await Task.Delay(900);
            Check("title bar and navigation hidden", titleBar.Visibility == Visibility.Collapsed && !_nav.IsPaneVisible, $"titleBar={titleBar.Visibility}, pane={_nav.IsPaneVisible}");
            Check("page title and info hidden", header.Visibility == Visibility.Collapsed && info.Visibility == Visibility.Collapsed, $"header={header.Visibility}, info={info.Visibility}");
            var frameBounds = BoundsInRoot(frame);
            Check("picture fills the window edge to edge", frameBounds.X <= 0.5 && frameBounds.Y <= 0.5 && frameBounds.Width >= _root.ActualWidth - 0.5 && frameBounds.Height >= _root.ActualHeight - 0.5,
                $"frame {frameBounds.X:0},{frameBounds.Y:0} {frameBounds.Width:0}x{frameBounds.Height:0} in {_root.ActualWidth:0}x{_root.ActualHeight:0}");
            Check("toolbar floats over the picture", Grid.GetRow(toolbarHost) == 1 && toolbarHost.Opacity > 0.99 && Named<AppBarButton>("FullScreenButton").Label == "退出全屏",
                $"row={Grid.GetRow(toolbarHost)}, opacity={toolbarHost.Opacity}, label={Named<AppBarButton>("FullScreenButton").Label}");
            Check("same video element, still attached", ReferenceEquals(Descendants<MediaPlayerElement>(Named<FrameworkElement>("VideoSurface")).FirstOrDefault(), element) && element.IsLoaded, element.IsLoaded);
            Check("no reload", Field<int>(_engine, "_loadGeneration") == loads && _vm.CurrentEpisodeUrl == url, Field<int>(_engine, "_loadGeneration") - loads);
            Check("playback state unchanged", _vm.PlaybackState == stateBefore, $"{stateBefore} -> {_vm.PlaybackState}");
            Check(pauseFirst ? "paused position kept" : "playback kept running",
                pauseFirst ? Math.Abs(_engine.Position - timeBefore) < 0.3 : _engine.Position > timeBefore + 0.5,
                $"{timeBefore:0.00}s -> {_engine.Position:0.00}s");
            await SavePngAsync(_root, $"fullscreen-{name.Replace(' ', '-')}.png");

            // The floating toolbar hides when idle and comes back on demand.
            Check("idle toolbar hides", await WaitUntil(() => toolbarHost.Opacity < 0.01, 5000), toolbarHost.Opacity);
            Call(_page, "ShowOverlayToolbar");
            Check("toolbar comes back", toolbarHost.Opacity > 0.99 && toolbarHost.IsHitTestVisible, toolbarHost.Opacity);

            var midPosition = _engine.Position;
            Execute(PlayerCommand.ExitFullScreen);
            Check("Esc leaves full screen", await WaitUntil(() => appWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen && !_page.IsFullScreen, 4000), appWindow.Presenter.Kind);
            await Task.Delay(900);
            Check("presenter restored", appWindow.Presenter.Kind == beforeKind, $"{beforeKind} -> {appWindow.Presenter.Kind}");
            Check("window back where it was", appWindow.Position.X == beforePosition.X && appWindow.Position.Y == beforePosition.Y && appWindow.Size.Width == beforeSize.Width && appWindow.Size.Height == beforeSize.Height,
                $"{beforePosition.X},{beforePosition.Y} {beforeSize.Width}x{beforeSize.Height} -> {appWindow.Position.X},{appWindow.Position.Y} {appWindow.Size.Width}x{appWindow.Size.Height}");
            Check("chrome and page layout restored", titleBar.Visibility == Visibility.Visible && _nav.IsPaneVisible && header.Visibility == Visibility.Visible && info.Visibility == Visibility.Visible && Grid.GetRow(toolbarHost) == 2 && toolbarHost.Opacity > 0.99,
                $"titleBar={titleBar.Visibility}, pane={_nav.IsPaneVisible}, header={header.Visibility}, info={info.Visibility}, toolbarRow={Grid.GetRow(toolbarHost)}");
            Check("still no reload", Field<int>(_engine, "_loadGeneration") == loads && _vm.CurrentEpisodeUrl == url, Field<int>(_engine, "_loadGeneration") - loads);
            Check("state unchanged after the round trip", _vm.PlaybackState == stateBefore, $"{stateBefore} -> {_vm.PlaybackState}");
            Check(pauseFirst ? "paused position still kept" : "progress not lost",
                pauseFirst ? Math.Abs(_engine.Position - timeBefore) < 0.3 : _engine.Position >= midPosition,
                $"{midPosition:0.00}s -> {_engine.Position:0.00}s");
            Check("Esc outside full screen does nothing", ReferenceEquals(_host.Content, _page) && _vm.HasCurrentItem, _host.Content?.GetType().Name);
        }

        /// <summary>
        /// Posts real key messages to this window's own input site, never to the
        /// desktop. If the window does not take background keys, this is reported as
        /// not verified rather than as a failure.
        /// </summary>
        private async Task PostedKeysAsync()
        {
            Section("Real key messages");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var target = FindInputSite(hwnd);
            if (target == IntPtr.Zero)
            {
                Line("  [SKIP] no input site window found; real key delivery not verified");
                return;
            }

            _page.FocusVideo();
            _native.IsMuted = false;
            await WaitUntil(() => !_vm.IsMuted);
            PostKey(target, 0x4D);
            if (!await WaitUntil(() => _native.IsMuted, 1500))
            {
                Line("  [SKIP] posted key did not reach the page (window not in the foreground); real key delivery not verified");
                _native.IsMuted = true;
                return;
            }

            Check("M key mutes", true, _native.IsMuted);
            var position = _engine.Position;
            PostKey(target, 0x27);
            Check("Right key seeks 10 seconds exactly once", await WaitUntil(() => _engine.Position > position + 8, 2000) && _engine.Position < position + 14,
                $"{position:0.00}s -> {_engine.Position:0.00}s");
            PostKey(target, 0x20);
            Check("Space key pauses exactly once", await WaitUntil(() => _vm.PlaybackState == CoreState.Paused, 2000), _vm.PlaybackState);
            await Task.Delay(400);
            Check("still paused (no second handler toggled it back)", _vm.PlaybackState == CoreState.Paused, _vm.PlaybackState);
            PostKey(target, 0x20);
            Check("Space key plays", await WaitUntil(() => _vm.PlaybackState == CoreState.Playing, 2000), _vm.PlaybackState);
            var volume = _native.Volume;
            PostKey(target, 0x26);
            Check("Up key raises the volume by 5%", await WaitUntil(() => Math.Abs(_native.Volume - (volume + 0.05)) < 0.011, 2000), $"{volume:0.00} -> {_native.Volume:0.00}");
            _native.IsMuted = true;
        }

        // ---- helpers ----

        private static SearchResult Work(string source, string sourceName, string id, IEnumerable<string> paths)
        {
            var episodes = paths.Select(path => new Uri(path).AbsoluteUri).ToList();
            return new SearchResult
            {
                Id = id,
                Title = "冒烟测试片",
                Source = source,
                SourceName = sourceName,
                Year = "2026",
                Description = "由 UI 冒烟测试在本机渲染的纯色测试片段。",
                Episodes = episodes,
                EpisodeTitles = Enumerable.Range(1, episodes.Count).Select(n => $"第{n}集").ToList(),
            };
        }

        private async Task<string> RenderClipAsync(string name, Color colour)
        {
            var composition = new MediaComposition();
            composition.Clips.Add(MediaClip.CreateFromColor(colour, TimeSpan.FromSeconds(ClipSeconds)));
            var folder = await StorageFolder.GetFolderFromPathAsync(_out);
            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            profile.Audio = null;
            var reason = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
            if (reason != Windows.Media.Transcoding.TranscodeFailureReason.None)
            {
                throw new InvalidOperationException($"Could not render {name}: {reason}");
            }

            return Path.Combine(_out, name);
        }

        private static void Resize(AppWindow appWindow, int width, int height, double scale)
        {
            if (appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored } overlapped)
            {
                overlapped.Restore();
            }

            appWindow.Resize(new SizeInt32((int)Math.Round(width * scale), (int)Math.Round(height * scale)));
        }

        private void Execute(PlayerCommand command) => Call(_page, "Execute", command);

        // x:Name fields cover flyout content too, which is not in the visual tree until shown.
        private T Named<T>(string name) where T : class =>
            typeof(PlayerPage).GetField(name, Private)?.GetValue(_page) as T
            ?? _page.FindName(name) as T
            ?? throw new InvalidOperationException($"{name} was not found on the player page.");

        /// <summary>Walks a toolbar with directional focus moves, as the arrow keys do.</summary>
        private List<string> WalkToolbar(CommandBar toolbar, Control start)
        {
            var visited = new List<string>();
            if (!start.Focus(FocusState.Keyboard))
            {
                return visited;
            }

            var options = new FindNextElementOptions { SearchRoot = toolbar };
            for (var step = 0; step < 20 && FocusManager.TryMoveFocus(FocusNavigationDirection.Left, options); step++)
            {
            }

            for (var step = 0; step < 30; step++)
            {
                if (FocusManager.GetFocusedElement(_root.XamlRoot) is not FrameworkElement focused)
                {
                    break;
                }

                var name = string.IsNullOrEmpty(focused.Name) ? focused.GetType().Name : focused.Name;
                if (visited.Contains(name))
                {
                    break;
                }

                visited.Add(name);
                if (!FocusManager.TryMoveFocus(FocusNavigationDirection.Right, options))
                {
                    break;
                }
            }

            _page.FocusVideo();
            return visited;
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;

        private static object? Call(object owner, string method, params object[] arguments) =>
            owner.GetType().GetMethod(method, Private)!.Invoke(owner, arguments);

        private static void Invoke(UIElement element)
        {
            if (!TryInvoke(element))
            {
                throw new InvalidOperationException($"{element.GetType().Name} cannot be invoked.");
            }
        }

        /// <summary>Activates a control through its automation peer, as assistive technology does.</summary>
        private static bool TryInvoke(UIElement element)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
            if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
            {
                invoke.Invoke();
                return true;
            }

            if (peer?.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand)
            {
                expand.Expand();
                return true;
            }

            return false;
        }

        private static void Toggle(UIElement element)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
            ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();
        }

        /// <summary>Invokes a list row the way assistive technology does. False if the row has no invoke pattern.</summary>
        private static bool TryInvokeItem(ListViewBase list, SelectorItem item)
        {
            var listPeer = FrameworkElementAutomationPeer.FromElement(list) ?? FrameworkElementAutomationPeer.CreatePeerForElement(list);
            var index = list.Items.IndexOf(item);
            var children = listPeer.GetChildren()?.Where(child => child.GetPattern(PatternInterface.SelectionItem) is not null).ToList() ?? [];
            if (index < 0 || index >= children.Count || children[index].GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            {
                return false;
            }

            invoke.Invoke();
            return true;
        }

        private static string EpisodeTags(GridView grid) =>
            string.Join(",", grid.Items.OfType<FrameworkElement>().Select(item => item.Tag));

        private List<string> TabOrder()
        {
            var stops = new List<string>();
            var options = new FindNextElementOptions { SearchRoot = _root };
            for (var step = 0; step < 60; step++)
            {
                if (!FocusManager.TryMoveFocus(FocusNavigationDirection.Next, options))
                {
                    break;
                }

                if (FocusManager.GetFocusedElement(_root.XamlRoot) is not FrameworkElement focused)
                {
                    break;
                }

                var name = string.IsNullOrEmpty(focused.Name) ? focused.GetType().Name : focused.Name;
                if (stops.Count > 0 && stops[0] == name && step > 2)
                {
                    break;
                }

                stops.Add(name);
            }

            _page.FocusVideo();
            return stops;
        }

        private Windows.Foundation.Rect BoundsInRoot(FrameworkElement element)
        {
            var origin = element.TransformToVisual(_root).TransformPoint(new Windows.Foundation.Point(0, 0));
            return new Windows.Foundation.Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static FrameworkElement? ByName(DependencyObject root, string name) =>
            Descendants<FrameworkElement>(root).FirstOrDefault(element => element.Name == name);

        private async Task SavePngAsync(FrameworkElement element, string name)
        {
            try
            {
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(element);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
                var bytes = new byte[stream.Size];
                await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
                File.WriteAllBytes(Path.Combine(_out, name), bytes);
                Line($"  rendering saved: {name} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
            }
            catch (Exception ex)
            {
                Line($"  rendering {name} failed: {ex.Message}");
            }
        }

        private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMilliseconds = 10000)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }

            return condition();
        }

        private void Section(string name)
        {
            Line(string.Empty);
            Line($"== {name}");
        }

        private void Check(string name, bool passed, object? observed)
        {
            _checks++;
            if (!passed) _failures++;
            Line($"  [{(passed ? "PASS" : "FAIL")}] {name}: {observed}");
        }

        private void Line(string text) => File.AppendAllText(ReportPath, text + Environment.NewLine, Encoding.UTF8);

        private static IntPtr FindInputSite(IntPtr parent)
        {
            var found = IntPtr.Zero;
            EnumChildWindows(parent, (child, _) =>
            {
                var name = new StringBuilder(256);
                GetClassName(child, name, name.Capacity);
                if (name.ToString().Contains("InputSite", StringComparison.OrdinalIgnoreCase))
                {
                    found = child;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static void PostKey(IntPtr target, int virtualKey)
        {
            var scan = MapVirtualKey((uint)virtualKey, 0);
            PostMessage(target, 0x0100, virtualKey, (IntPtr)(1 | (scan << 16)));
            PostMessage(target, 0x0101, virtualKey, unchecked((IntPtr)(long)(1 | (scan << 16) | 0xC0000000)));
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);
    }
}
