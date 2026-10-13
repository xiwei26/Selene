using System.Globalization;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

/// <summary>
/// Player page: the video with native transport controls, a fixed toolbar for
/// the commands the native controls do not offer, and a scrollable info area.
/// All playback state lives in <see cref="PlayerViewModel"/>; this page only
/// renders it and forwards commands.
/// </summary>
public sealed partial class PlayerPage : UserControl
{
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string VolumeGlyph = "";
    private const string MuteGlyph = "";
    private const string FullScreenGlyph = "";
    private const string BackToWindowGlyph = "";
    private static readonly Thickness WindowedVideoMargin = new(28, 0, 28, 0);
    private static readonly Thickness WindowedMessageMargin = new(40, 12, 40, 0);
    private static readonly Thickness FullScreenMessageMargin = new(40, 64, 40, 0);

    private readonly DispatcherTimer _overlayTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _osdTimer = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private PlayerViewModel? _viewModel;
    private SearchResult? _seed;
    private IContentProvider? _provider;
    private PlayerMetadataViewModel? _metadata;
    private string? _metadataKey;
    private string? _loadingMetadataKey;
    private int _metadataRequestId;
    private bool _isSyncingControls;
    private bool _isSubscribed;
    private bool _showInfo = true;
    private bool _pointerOverToolbar;
    private Windows.Foundation.Point? _lastPointerPosition;
    private string? _shownError;
    private string? _shownNotice;

    /// <summary>Whether the window is in full screen. The shell owns the window; this mirrors it.</summary>
    public bool IsFullScreen { get; private set; }

    public event EventHandler? CloseRequested;

    /// <summary>Asks the shell to enter (true) or leave (false) full screen.</summary>
    public event EventHandler<bool>? FullScreenRequested;

    /// <summary>Raised when the page leaves the visual tree.</summary>
    public event EventHandler? Left;

    public PlayerPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        BackButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        ErrorRetryButton.Click += (_, _) => _viewModel?.Retry();
        ErrorBar.Closed += (_, _) => _shownError = _viewModel?.PlaybackError;
        NoticeBar.Closed += (_, _) => _shownNotice = _viewModel?.Notice;

        PreviousEpisodeButton.Click += (s, _) => Run(s, vm => vm.PlayPreviousEpisode());
        NextEpisodeButton.Click += (s, _) => Run(s, vm => vm.PlayNextEpisode());
        SeekBackwardButton.Click += (s, _) => Run(s, vm => vm.SeekBy(-PlayerViewModel.SeekStepSeconds));
        SeekForwardButton.Click += (s, _) => Run(s, vm => vm.SeekBy(PlayerViewModel.SeekStepSeconds));
        PlayPauseButton.Click += (s, _) => Run(s, vm => vm.TogglePlayPause());
        MuteButton.Click += (s, _) => Run(s, vm => vm.ToggleMute());
        FlyoutMuteButton.Click += (_, _) => _viewModel?.ToggleMute();
        RetryButton.Click += (s, _) => Run(s, vm => vm.Retry());
        FullScreenButton.Click += (s, _) =>
        {
            ReturnFocusAfterPointer(s);
            RequestFullScreen(!IsFullScreen);
        };
        // Toggles react to the checked state, not to Click, so assistive technology
        // that flips them through the toggle pattern has the same effect as a click.
        AutoNextToggle.Checked += OnAutoNextToggled;
        AutoNextToggle.Unchecked += OnAutoNextToggled;
        InfoToggle.Checked += OnInfoToggled;
        InfoToggle.Unchecked += OnInfoToggled;
        ReverseOrderToggle.Checked += OnReverseOrderToggled;
        ReverseOrderToggle.Unchecked += OnReverseOrderToggled;

        VolumeSlider.ValueChanged += OnVolumeSliderValueChanged;
        SpeedFlyout.Opening += (_, _) => BuildSpeedMenu();
        EpisodesFlyout.Opening += (_, _) => BuildEpisodeGrid();
        EpisodesGrid.ItemClick += OnEpisodeItemClick;
        SourcesFlyout.Opening += OnSourcesFlyoutOpening;
        SourcesList.ItemClick += OnSourceItemClick;
        foreach (var flyout in new FlyoutBase[] { VolumeFlyout, SpeedFlyout, EpisodesFlyout, SourcesFlyout })
        {
            flyout.Closed += OnFlyoutClosed;
        }

        VideoSurface.VideoTapped += (_, _) => FocusVideo();
        VideoSurface.VideoDoubleTapped += (_, _) => RequestFullScreen(!IsFullScreen);

        // Full screen: the toolbar floats over the picture and hides when idle.
        RootLayout.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        ToolbarHost.PointerEntered += (_, _) => _pointerOverToolbar = true;
        ToolbarHost.PointerExited += (_, _) => _pointerOverToolbar = false;
        ToolbarHost.GotFocus += (_, _) => ShowOverlayToolbar();
        _overlayTimer.Tick += (_, _) => HideOverlayToolbarIfIdle();
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
            OsdBorder.Visibility = Visibility.Collapsed;
        };
    }

    public void Bind(PlayerViewModel viewModel)
    {
        _viewModel = viewModel;
        VideoSurface.Player = viewModel.Player;
    }

    public void SetProvider(IContentProvider? provider)
    {
        if (!ReferenceEquals(_provider, provider))
        {
            _metadataKey = null;
            _loadingMetadataKey = null;
        }

        _provider = provider;
    }

    /// <summary>
    /// Starts <paramref name="detail"/> at a backend episode index.
    /// <paramref name="candidates"/> are other results that may be the same work.
    /// </summary>
    public async Task OpenAsync(
        SearchResult detail,
        int episodeIndex,
        IContentProvider? provider = null,
        IEnumerable<SearchResult>? candidates = null)
    {
        SetProvider(provider);
        _seed = detail;
        _metadata = null;
        _metadataKey = null;
        _loadingMetadataKey = null;
        TitleText.Text = detail.Title;
        RenderPlayerMetadata();
        _ = LoadPlayerMetadataAsync(detail);
        await WaitUntilVideoSurfaceReadyAsync();

        // The viewer may have navigated away while the surface was getting ready.
        if (_viewModel is null || XamlRoot is null)
        {
            return;
        }

        _viewModel.Open(detail, episodeIndex, candidates);
        SyncState();
        FocusVideo();
    }

    public Task LoadCurrentMetadataAsync()
    {
        var result = _viewModel?.CurrentResult ?? _seed;
        return result is null ? Task.CompletedTask : LoadPlayerMetadataAsync(result);
    }

    public Task WaitUntilVideoSurfaceReadyAsync()
    {
        return VideoSurface.WaitUntilReadyAsync();
    }

    /// <summary>Moves keyboard focus to the page so shortcuts act on the video.</summary>
    public void FocusVideo() => Focus(FocusState.Programmatic);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (!_isSubscribed)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isSubscribed = true;
        }

        _seed = _viewModel.CurrentResult ?? _seed;
        SyncState();
        FocusVideo();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && _isSubscribed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _isSubscribed = false;
        }

        _overlayTimer.Stop();
        _osdTimer.Stop();
        OsdBorder.Visibility = Visibility.Collapsed;
        Left?.Invoke(this, EventArgs.Empty);
    }

    // ---- commands ----

    private void Run(object sender, Action<PlayerViewModel> action)
    {
        if (_viewModel is not null)
        {
            action(_viewModel);
        }

        ReturnFocusAfterPointer(sender);
    }

    /// <summary>
    /// After a mouse or touch click, focus goes back to the video so Space keeps
    /// meaning play/pause. Keyboard users keep focus on the button they used.
    /// </summary>
    private void ReturnFocusAfterPointer(object? sender)
    {
        if (sender is Control { FocusState: not FocusState.Keyboard })
        {
            FocusVideo();
        }
    }

    private void OnAutoNextToggled(object sender, RoutedEventArgs e)
    {
        if (!_isSyncingControls && _viewModel is not null)
        {
            _viewModel.AutoPlayNext = AutoNextToggle.IsChecked == true;
        }
    }

    private void OnInfoToggled(object sender, RoutedEventArgs e)
    {
        _showInfo = InfoToggle.IsChecked == true;
        UpdateInfoVisibility();
    }

    private void OnReverseOrderToggled(object sender, RoutedEventArgs e)
    {
        // Rebuilding the grid sets the toggle again; only a real change gets here twice.
        if (_viewModel is not null && _viewModel.IsEpisodeReversed != (ReverseOrderToggle.IsChecked == true))
        {
            _viewModel.ToggleEpisodeOrder();
            BuildEpisodeGrid();
        }
    }

    private void OnFlyoutClosed(object? sender, object e)
    {
        if (XamlRoot is not null &&
            FocusManager.GetFocusedElement(XamlRoot) is Control { FocusState: not FocusState.Keyboard })
        {
            FocusVideo();
        }
    }

    private void RequestFullScreen(bool fullScreen)
    {
        FullScreenRequested?.Invoke(this, fullScreen);
    }

    private void Execute(PlayerCommand command)
    {
        var vm = _viewModel;
        if (vm is null) return;

        switch (command)
        {
            case PlayerCommand.TogglePlayPause:
                vm.TogglePlayPause();
                break;
            case PlayerCommand.SeekBackward:
                ShowOsd(vm.SeekBy(-PlayerViewModel.SeekStepSeconds) ? "后退 10 秒" : "当前视频不支持跳转");
                break;
            case PlayerCommand.SeekForward:
                ShowOsd(vm.SeekBy(PlayerViewModel.SeekStepSeconds) ? "前进 10 秒" : "当前视频不支持跳转");
                break;
            case PlayerCommand.VolumeUp:
                vm.AdjustVolume(PlayerViewModel.VolumeStep);
                ShowOsd(VolumeDescription(vm));
                break;
            case PlayerCommand.VolumeDown:
                vm.AdjustVolume(-PlayerViewModel.VolumeStep);
                ShowOsd(VolumeDescription(vm));
                break;
            case PlayerCommand.ToggleMute:
                vm.ToggleMute();
                ShowOsd(VolumeDescription(vm));
                break;
            case PlayerCommand.ToggleFullScreen:
                RequestFullScreen(!IsFullScreen);
                break;
            case PlayerCommand.ExitFullScreen:
                RequestFullScreen(false);
                break;
            case PlayerCommand.PreviousEpisode:
                if (!vm.PlayPreviousEpisode()) ShowOsd("已经是第一集");
                break;
            case PlayerCommand.NextEpisode:
                if (!vm.PlayNextEpisode()) ShowOsd("已经是最后一集");
                break;
        }

        // Bring the auto-hidden controls back so the effect of the key is visible.
        VideoSurface.ShowTransportControls();
        ShowOverlayToolbar();
    }

    private static string VolumeDescription(PlayerViewModel vm) =>
        vm.IsMuted ? $"已静音（音量 {vm.VolumePercent}%）" : $"音量 {vm.VolumePercent}%";

    // ---- keyboard ----

    /// <summary>
    /// Handles a key press while the player page is the active page. Called from the
    /// shell's tunnelling key event, before the focused control sees the key, so a
    /// press is handled either here or by that control, never by both.
    /// </summary>
    public void HandlePreviewKeyDown(KeyRoutedEventArgs e)
    {
        if (_viewModel is null || e.Handled || MapKey(e.Key) is not PlayerKey key)
        {
            return;
        }

        var command = PlayerShortcuts.Resolve(key, CurrentModifiers(), ClassifyFocus(), IsFullScreen, HasOpenPopup());
        if (command == PlayerCommand.None)
        {
            return;
        }

        e.Handled = true;

        // Holding an arrow keeps seeking or changing the volume; other keys act once per press.
        var repeats = command is PlayerCommand.SeekBackward or PlayerCommand.SeekForward
            or PlayerCommand.VolumeUp or PlayerCommand.VolumeDown;
        if (e.KeyStatus.WasKeyDown && !repeats)
        {
            return;
        }

        Execute(command);
    }

    private static PlayerKey? MapKey(VirtualKey key) => key switch
    {
        VirtualKey.Space => PlayerKey.Space,
        VirtualKey.Enter => PlayerKey.Enter,
        VirtualKey.Left => PlayerKey.Left,
        VirtualKey.Right => PlayerKey.Right,
        VirtualKey.Up => PlayerKey.Up,
        VirtualKey.Down => PlayerKey.Down,
        VirtualKey.M => PlayerKey.M,
        VirtualKey.F => PlayerKey.F,
        VirtualKey.F11 => PlayerKey.F11,
        VirtualKey.Escape => PlayerKey.Escape,
        _ => null,
    };

    private static PlayerKeyModifiers CurrentModifiers()
    {
        var modifiers = PlayerKeyModifiers.None;
        if (IsDown(VirtualKey.Control)) modifiers |= PlayerKeyModifiers.Control;
        if (IsDown(VirtualKey.Shift)) modifiers |= PlayerKeyModifiers.Shift;
        if (IsDown(VirtualKey.Menu)) modifiers |= PlayerKeyModifiers.Alt;
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) modifiers |= PlayerKeyModifiers.Windows;
        return modifiers;

        static bool IsDown(VirtualKey key) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    }

    private PlayerFocusKind ClassifyFocus()
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focused)
        {
            return PlayerFocusKind.Video;
        }

        var kind = PlayerFocusKind.Video;
        for (var node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this))
            {
                return kind;
            }

            // A button inside a command bar (this page's toolbar or the native
            // transport controls) is reached with the bar's own arrow-key navigation.
            if (kind == PlayerFocusKind.Button && node is CommandBar)
            {
                kind = PlayerFocusKind.ToolbarCommand;
            }

            if (kind != PlayerFocusKind.Video)
            {
                continue;
            }

            kind = node switch
            {
                TextBox or PasswordBox or RichEditBox or AutoSuggestBox or NumberBox => PlayerFocusKind.TextInput,
                ComboBox => PlayerFocusKind.Selector,
                Slider => PlayerFocusKind.Slider,
                ButtonBase or ToggleSwitch or SelectorItem => PlayerFocusKind.Button,
                _ => PlayerFocusKind.Video,
            };
        }

        // Focus is in the shell (navigation pane, title bar), not in the player.
        return PlayerFocusKind.Outside;
    }

    private bool HasOpenPopup()
    {
        if (XamlRoot is null)
        {
            return false;
        }

        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
        {
            // A tooltip is not something the viewer is operating.
            if (popup.Child is not ToolTip)
            {
                return true;
            }
        }

        return false;
    }

    // ---- full screen layout ----

    /// <summary>
    /// Applies the page side of full screen: only the picture remains, with the
    /// toolbar floating over it. The video element is never re-created or moved,
    /// so playback continues untouched.
    /// </summary>
    public void ApplyFullScreenLayout(bool fullScreen)
    {
        IsFullScreen = fullScreen;
        Header.Visibility = fullScreen ? Visibility.Collapsed : Visibility.Visible;
        VideoFrame.Margin = fullScreen ? new Thickness(0) : WindowedVideoMargin;
        MessageStack.Margin = fullScreen ? FullScreenMessageMargin : WindowedMessageMargin;

        Grid.SetRow(ToolbarHost, fullScreen ? 1 : 2);
        ToolbarHost.VerticalAlignment = fullScreen ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        ToolbarHost.RequestedTheme = fullScreen ? ElementTheme.Dark : ElementTheme.Default;
        ToolbarHost.Background = fullScreen ? new SolidColorBrush(Color.FromArgb(0xD9, 0x1F, 0x1F, 0x1F)) : null;
        UpdateInfoVisibility();

        FullScreenIcon.Glyph = fullScreen ? BackToWindowGlyph : FullScreenGlyph;
        FullScreenButton.Label = fullScreen ? "退出全屏" : "全屏";
        ToolTipService.SetToolTip(FullScreenButton, fullScreen ? "退出全屏 (Esc / F / F11)" : "全屏 (F / F11)");

        _lastPointerPosition = null;
        if (fullScreen)
        {
            ShowOverlayToolbar();
        }
        else
        {
            _overlayTimer.Stop();
            ToolbarHost.Opacity = 1;
            ToolbarHost.IsHitTestVisible = true;
        }

        FocusVideo();
    }

    private void UpdateInfoVisibility()
    {
        var visible = _showInfo && !IsFullScreen;
        BottomScrollViewer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        InfoRow.MaxHeight = visible ? 260 : 0;
        InfoRow.Height = visible ? new GridLength(0.45, GridUnitType.Star) : new GridLength(0);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!IsFullScreen)
        {
            return;
        }

        // Ignore the synthetic moves raised when content changes under a still pointer.
        var position = e.GetCurrentPoint(RootLayout).Position;
        if (_lastPointerPosition is { } last &&
            Math.Abs(last.X - position.X) < 2 &&
            Math.Abs(last.Y - position.Y) < 2)
        {
            return;
        }

        _lastPointerPosition = position;
        ShowOverlayToolbar();
    }

    private void ShowOverlayToolbar()
    {
        if (!IsFullScreen)
        {
            return;
        }

        ToolbarHost.Opacity = 1;
        ToolbarHost.IsHitTestVisible = true;
        _overlayTimer.Stop();
        _overlayTimer.Start();
    }

    private void HideOverlayToolbarIfIdle()
    {
        if (!IsFullScreen)
        {
            _overlayTimer.Stop();
            return;
        }

        // Stay while the viewer is using it: pointer over it, a menu open, or keyboard focus inside.
        if (_pointerOverToolbar || HasOpenPopup() || HasKeyboardFocusInToolbar())
        {
            return;
        }

        _overlayTimer.Stop();
        ToolbarHost.Opacity = 0;
        ToolbarHost.IsHitTestVisible = false;
    }

    private bool HasKeyboardFocusInToolbar()
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not Control { FocusState: FocusState.Keyboard } focused)
        {
            return false;
        }

        for (DependencyObject? node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ToolbarHost))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowOsd(string text)
    {
        OsdText.Text = text;
        OsdBorder.Visibility = Visibility.Visible;
        _osdTimer.Stop();
        _osdTimer.Start();
    }

    // ---- state rendering ----

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyPropertyChange(e.PropertyName);
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => ApplyPropertyChange(e.PropertyName));
        }
    }

    private void ApplyPropertyChange(string? propertyName)
    {
        if (_viewModel is null) return;

        SyncState();
        switch (propertyName)
        {
            case nameof(PlayerViewModel.CurrentResult):
                _seed = _viewModel.CurrentResult;
                if (_seed is null)
                {
                    _metadata = null;
                    _metadataKey = null;
                }

                RenderPlayerMetadata();
                _ = LoadCurrentMetadataAsync();
                RefreshOpenFlyouts();
                break;
            case nameof(PlayerViewModel.CurrentEpisodeIndex):
            case nameof(PlayerViewModel.IsEpisodeReversed):
                if (EpisodesFlyout.IsOpen) BuildEpisodeGrid();
                break;
            case nameof(PlayerViewModel.SourceCandidates):
            case nameof(PlayerViewModel.IsLoadingSources):
            case nameof(PlayerViewModel.IsSwitchingSource):
            case nameof(PlayerViewModel.Notice):
                if (SourcesFlyout.IsOpen) BuildSourceList();
                break;
        }
    }

    private void RefreshOpenFlyouts()
    {
        if (EpisodesFlyout.IsOpen) BuildEpisodeGrid();
        if (SourcesFlyout.IsOpen) BuildSourceList();
    }

    private void SyncState()
    {
        var vm = _viewModel;
        if (vm is null) return;

        _isSyncingControls = true;
        try
        {
            var result = vm.CurrentResult;
            TitleText.Text = result?.Title ?? _seed?.Title ?? string.Empty;
            SubtitleText.Text = result is null
                ? string.Empty
                : string.Join(" · ", new[] { vm.CurrentEpisodeTitle, result.SourceName }.Where(part => !string.IsNullOrWhiteSpace(part)));

            StatusText.Text = vm.StatusText;
            TimeText.Text = vm.HasCurrentItem
                ? $"{Format(vm.PlayTime)} / {(vm.TotalTime > 0 ? Format(vm.TotalTime) : "--:--")}"
                : string.Empty;

            var active = vm.IsPlaybackActive;
            PlayPauseIcon.Glyph = active ? PauseGlyph : PlayGlyph;
            PlayPauseButton.Label = active ? "暂停" : "播放";
            PlayPauseButton.IsEnabled = vm.HasCurrentItem;
            ToolTipService.SetToolTip(PlayPauseButton, active ? "暂停 (空格)" : "播放 (空格)");

            // Seeking is offered only when the engine can really seek this media.
            SeekBackwardButton.IsEnabled = vm.CanSeek;
            SeekForwardButton.IsEnabled = vm.CanSeek;
            SeekBackwardButton.Label = vm.CanSeek ? "后退 10 秒" : "后退 10 秒（当前视频不支持跳转）";
            SeekForwardButton.Label = vm.CanSeek ? "前进 10 秒" : "前进 10 秒（当前视频不支持跳转）";
            VideoSurface.SetSeekEnabled(vm.CanSeek);

            PreviousEpisodeButton.IsEnabled = vm.CanPlayPrevious;
            NextEpisodeButton.IsEnabled = vm.CanPlayNext;
            PreviousEpisodeButton.Label = vm.CanPlayPrevious || result is null ? "上一集" : "上一集（已经是第一集）";
            NextEpisodeButton.Label = vm.CanPlayNext || result is null ? "下一集" : "下一集（已经是最后一集）";

            var silent = vm.IsMuted || vm.VolumePercent == 0;
            MuteIcon.Glyph = silent ? MuteGlyph : VolumeGlyph;
            FlyoutMuteIcon.Glyph = silent ? MuteGlyph : VolumeGlyph;
            MuteButton.Label = vm.IsMuted ? "取消静音" : "静音";
            ToolTipService.SetToolTip(MuteButton, vm.IsMuted ? "取消静音 (M)" : "静音 (M)");
            ToolTipService.SetToolTip(FlyoutMuteButton, vm.IsMuted ? "取消静音 (M)" : "静音 (M)");
            AutomationProperties.SetName(FlyoutMuteButton, vm.IsMuted ? "取消静音" : "静音");
            VolumeButton.Label = $"音量 {vm.VolumePercent}%";
            ToolTipService.SetToolTip(VolumeButton, $"音量 {vm.VolumePercent}% (↑ / ↓)");
            if (Math.Abs(VolumeSlider.Value - vm.VolumePercent) > 0.5)
            {
                VolumeSlider.Value = vm.VolumePercent;
            }

            VolumeValueText.Text = $"{vm.VolumePercent}%";

            var rate = FormatRate(vm.PlaybackRate);
            SpeedButtonText.Text = rate;
            SpeedButton.Label = $"倍速 {rate}";
            ToolTipService.SetToolTip(SpeedButton, $"倍速：{rate}");

            AutoNextToggle.IsChecked = vm.AutoPlayNext;
            EpisodesButton.IsEnabled = result is { Episodes.Count: > 0 };
            SourcesButton.IsEnabled = result is not null;
            SourcesButton.Label = vm.SourceCandidates.Count > 1 ? $"来源（{vm.SourceCandidates.Count}）" : "来源";
            RetryButton.IsEnabled = vm.HasCurrentItem;

            // A message opens when it changes, so one the viewer closed stays closed.
            if (!string.Equals(vm.PlaybackError, _shownError, StringComparison.Ordinal))
            {
                _shownError = vm.PlaybackError;
                ErrorBar.Message = vm.PlaybackError ?? string.Empty;
                ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(vm.PlaybackError);
            }

            if (!string.Equals(vm.Notice, _shownNotice, StringComparison.Ordinal))
            {
                _shownNotice = vm.Notice;
                NoticeBar.Message = vm.Notice ?? string.Empty;
                NoticeBar.IsOpen = !string.IsNullOrWhiteSpace(vm.Notice);
            }
        }
        finally
        {
            _isSyncingControls = false;
        }
    }

    private void OnVolumeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingControls || _viewModel is null) return;

        // Dragging the volume up while muted is a request to hear it.
        if (_viewModel.IsMuted && e.NewValue > e.OldValue)
        {
            _viewModel.SetMuted(false);
        }

        _viewModel.SetVolume(e.NewValue / 100.0);
    }

    private void BuildSpeedMenu()
    {
        SpeedFlyout.Items.Clear();
        var vm = _viewModel;
        if (vm is null) return;

        foreach (var rate in PlayerViewModel.PlaybackRates)
        {
            // A speed the media cannot play stays listed but disabled, with the reason.
            var reason = vm.PlaybackRateUnavailableReason(rate);
            var label = rate == 1 ? "1x（正常）" : FormatRate(rate);
            var item = new RadioMenuFlyoutItem
            {
                Text = reason is null ? label : $"{label}（{reason}）",
                GroupName = "PlaybackRate",
                IsChecked = Math.Abs(vm.PlaybackRate - rate) < 0.001,
                IsEnabled = reason is null,
            };

            // Reacting to the checked state covers a click, the keyboard, and
            // assistive technology that selects the item without a click.
            item.RegisterPropertyChangedCallback(RadioMenuFlyoutItem.IsCheckedProperty, (_, _) =>
            {
                if (!item.IsChecked)
                {
                    return;
                }

                if (vm.SetPlaybackRate(rate))
                {
                    ShowOsd($"倍速 {FormatRate(rate)}");
                }

                SyncState();
            });
            SpeedFlyout.Items.Add(item);
        }
    }

    private void BuildEpisodeGrid()
    {
        EpisodesGrid.Items.Clear();
        var vm = _viewModel;
        var result = vm?.CurrentResult;
        if (vm is null || result is null) return;

        ReverseOrderToggle.IsChecked = vm.IsEpisodeReversed;
        EpisodesHeaderText.Text = $"选集（共 {result.Episodes.Count} 集）";

        // Display order may be reversed; every item still carries its backend index.
        GridViewItem? current = null;
        foreach (var index in vm.OrderedEpisodeIndices)
        {
            var title = SourceMatcher.EpisodeTitle(result, index);
            var isCurrent = index == vm.CurrentEpisodeIndex;
            var item = new GridViewItem
            {
                Tag = index,
                Width = 108,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new TextBlock
                {
                    Tag = index,
                    Text = title,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            ToolTipService.SetToolTip(item, title);
            AutomationProperties.SetName(item, isCurrent ? $"{title}，正在播放" : title);
            EpisodesGrid.Items.Add(item);
            if (isCurrent)
            {
                current = item;
            }
        }

        EpisodesGrid.SelectedItem = current;
        if (current is not null)
        {
            DispatcherQueue.TryEnqueue(() => EpisodesGrid.ScrollIntoView(current));
        }
    }

    private void OnEpisodeItemClick(object sender, ItemClickEventArgs e)
    {
        if ((e.ClickedItem as FrameworkElement)?.Tag is int index)
        {
            EpisodesFlyout.Hide();
            _viewModel?.PlayEpisode(index);
        }
    }

    private void OnSourcesFlyoutOpening(object? sender, object e)
    {
        BuildSourceList();

        // Other sources are looked up on demand, the first time they are asked for.
        _ = _viewModel?.LoadSourceCandidatesAsync(_provider);
    }

    private void BuildSourceList()
    {
        SourcesList.Items.Clear();
        var vm = _viewModel;
        if (vm?.CurrentResult is not { } current)
        {
            SourcesLoadingRing.IsActive = false;
            SourcesHintText.Text = "当前没有正在播放的内容。";
            return;
        }

        ListViewItem? selected = null;
        foreach (var candidate in vm.SourceCandidates)
        {
            var isCurrent = SourceMatcher.IsSameSource(candidate, current);
            var name = string.IsNullOrWhiteSpace(candidate.SourceName) ? candidate.Source : candidate.SourceName;
            var detail = candidate.Episodes.Count > 0 ? $"共 {candidate.Episodes.Count} 集" : "选择后加载集数";
            if (isCurrent)
            {
                detail = $"正在播放 · {detail}";
            }

            var panel = new StackPanel { Spacing = 2, Padding = new Thickness(0, 6, 0, 6), Tag = candidate };
            panel.Children.Add(new TextBlock
            {
                Text = name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = isCurrent ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            });
            panel.Children.Add(UiHelpers.SecondaryText(detail, 12));
            var item = new ListViewItem { Content = panel, Tag = candidate };
            AutomationProperties.SetName(item, $"{name}，{detail}");
            SourcesList.Items.Add(item);
            if (isCurrent)
            {
                selected = item;
            }
        }

        SourcesList.SelectedItem = selected;
        SourcesList.IsEnabled = !vm.IsSwitchingSource;
        SourcesLoadingRing.IsActive = vm.IsLoadingSources || vm.IsSwitchingSource;
        SourcesHintText.Text = SourceHint(vm);
    }

    private string SourceHint(PlayerViewModel vm)
    {
        if (vm.IsSwitchingSource) return "正在切换来源…";
        if (vm.IsLoadingSources) return "正在查找同一作品的其他来源…";
        if (!string.IsNullOrWhiteSpace(vm.Notice)) return vm.Notice;
        if (vm.SourceCandidates.Count > 1) return "切换后保留当前集数、播放进度和暂停状态；目标来源缺少这一集时不会切换。";
        return _provider is null ? "登录服务端后可以查找其他来源。" : "没有找到同一作品的其他来源。";
    }

    private async void OnSourceItemClick(object sender, ItemClickEventArgs e)
    {
        if (_viewModel is null || (e.ClickedItem as FrameworkElement)?.Tag is not SearchResult candidate)
        {
            return;
        }

        if (await _viewModel.SwitchSourceAsync(candidate, _provider))
        {
            SourcesFlyout.Hide();
        }
        else if (SourcesFlyout.IsOpen)
        {
            // The current source keeps playing; the list shows why nothing changed.
            BuildSourceList();
        }
    }

    // ---- metadata ----

    private async Task LoadPlayerMetadataAsync(SearchResult result)
    {
        var key = BuildMetadataKey(result);
        if (string.Equals(_metadataKey, key, StringComparison.Ordinal) &&
            (_metadata is not null || string.Equals(_loadingMetadataKey, key, StringComparison.Ordinal)))
        {
            RenderPlayerMetadata();
            return;
        }

        _metadataKey = key;
        _loadingMetadataKey = key;
        var requestId = ++_metadataRequestId;
        var metadata = new PlayerMetadataViewModel();
        await metadata.LoadAsync(result, _provider);
        if (requestId != _metadataRequestId)
        {
            return;
        }

        _metadata = metadata;
        _loadingMetadataKey = null;
        DispatcherQueue.TryEnqueue(RenderPlayerMetadata);
    }

    private void RenderPlayerMetadata()
    {
        InfoPanel.Children.Clear();

        var result = _metadata?.Result ?? _seed ?? _viewModel?.CurrentResult;
        if (result is null)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "详情",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        AddChip(chips, result.SourceName);
        AddChip(chips, result.Year);
        AddChip(chips, result.TypeName);

        var tmdb = _metadata?.TmdbBackdrop;
        if (tmdb?.Rating is double tmdbRating)
        {
            AddChip(chips, $"TMDB {tmdbRating:0.0}");
        }

        if (tmdb?.NumberOfSeasons is int seasons and > 0)
        {
            AddChip(chips, $"共 {seasons} 季");
        }

        if (!string.IsNullOrWhiteSpace(_metadata?.QuickInfo?.Rating))
        {
            AddChip(chips, $"豆瓣 {_metadata!.QuickInfo!.Rating}");
        }

        if (chips.Children.Count > 0)
        {
            stack.Children.Add(chips);
        }

        AddBackdropPreview(stack, tmdb);

        var overview = _metadata?.Overview ?? result.Description;
        if (!string.IsNullOrWhiteSpace(overview))
        {
            var overviewText = new TextBlock
            {
                Text = overview,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 1100,
            };
            UiHelpers.SetThemeResource(overviewText, TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            stack.Children.Add(overviewText);
        }

        if (_metadata?.QuickInfo is DoubanQuickInfo quickInfo)
        {
            stack.Children.Add(CreateQuickInfoPanel(quickInfo));
        }

        if (_metadata?.Comments.Count > 0)
        {
            stack.Children.Add(CreateCommentsPanel(_metadata.Comments));
        }

        if (_metadata?.Recommendations.Count > 0)
        {
            stack.Children.Add(CreateRecommendationsPanel(_metadata.Recommendations));
        }

        if (_provider is not null && _metadata is null)
        {
            var loadingText = new TextBlock { Text = "正在加载更多详情..." };
            UiHelpers.SetThemeResource(loadingText, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            stack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new ProgressRing { Width = 16, Height = 16, IsActive = true },
                    loadingText
                }
            });
        }

        InfoPanel.Children.Add(stack);
    }

    private static void AddBackdropPreview(StackPanel stack, TmdbBackdrop? tmdb)
    {
        var imageUrl = tmdb?.Logo ?? tmdb?.Poster ?? tmdb?.Backdrop;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        stack.Children.Add(new Image
        {
            Source = new BitmapImage(uri),
            Stretch = Stretch.Uniform,
            MaxHeight = 92,
            MaxWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
    }

    private static UIElement CreateQuickInfoPanel(DoubanQuickInfo quickInfo)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣详情",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        AddTextLine(stack, JoinLine("类型", quickInfo.Genres));
        AddTextLine(stack, JoinLine("导演", quickInfo.Directors));
        AddTextLine(stack, JoinLine("主演", quickInfo.Cast));
        AddTextLine(stack, string.IsNullOrWhiteSpace(quickInfo.Summary) ? null : quickInfo.Summary);
        return stack;
    }

    private static UIElement CreateCommentsPanel(IEnumerable<DoubanComment> comments)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣短评",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        foreach (var comment in comments.Take(3))
        {
            AddTextLine(
                stack,
                string.IsNullOrWhiteSpace(comment.Author)
                    ? comment.Content
                    : $"{comment.Author}: {comment.Content}");
        }

        return stack;
    }

    private static UIElement CreateRecommendationsPanel(IEnumerable<DoubanRecommendation> recommendations)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "相关推荐",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var item in recommendations.Take(8))
        {
            row.Children.Add(CreateChip(
                string.IsNullOrWhiteSpace(item.Rating) ? item.Title : $"{item.Title} {item.Rating}"));
        }

        stack.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = row,
        });
        return stack;
    }

    private static void AddTextLine(StackPanel stack, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1100,
        };
        UiHelpers.SetThemeResource(label, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        stack.Children.Add(label);
    }

    private static void AddChip(StackPanel chips, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        chips.Children.Add(CreateChip(text));
    }

    private static Border CreateChip(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 12 };
        UiHelpers.SetThemeResource(label, TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            BorderThickness = new Thickness(1),
            Child = label,
        };
        UiHelpers.SetThemeResource(chip, Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
        UiHelpers.SetThemeResource(chip, Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return chip;
    }

    private static string JoinLine(string label, IReadOnlyList<string> values)
    {
        return values.Count > 0 ? $"{label}: {string.Join("、", values)}" : string.Empty;
    }

    private static string BuildMetadataKey(SearchResult result)
    {
        return $"{result.Source}|{result.Id}|{result.Title}|{result.Year}|{result.DoubanId}";
    }

    private static string FormatRate(double rate) =>
        rate.ToString("0.##", CultureInfo.InvariantCulture) + "x";

    private static string Format(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
