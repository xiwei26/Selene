using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using System.Linq;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using SeleneNative.Views;

namespace SeleneNative;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private NavigationView? _navigationView;
    private ContentControl? _contentHost;
    private NavigationViewItem? _accountNavigationItem;
    private readonly IThemeService _themeService;
    private int _navigationGeneration;

    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayRecordPersistence _recordPersistence;
    private readonly DetailViewModel _detailViewModel;
    private readonly CategoryViewModel _categoryViewModel;
    private readonly IDoubanClient _doubanClient;
    private readonly IBangumiClient _bangumiClient;

    private HomePage _homePage = null!;
    private LoginPage _loginPage = null!;
    private SearchPage _searchPage = null!;
    private FavoritesPage _favoritesPage = null!;
    private HistoryPage _historyPage = null!;
    private SettingsPage _settingsPage = null!;
    private LivePage _livePage = null!;
    private LunaFeaturePage _lunaFeaturePage = null!;
    private CategoryPage _categoryPage = null!;
    private DetailPage _detailPage = null!;
    private PlayerPage _playerPage = null!;
    private AdminPage _adminPage = null!;

    // Full screen is owned by the window; the player page only asks for it.
    private bool _isPlayerFullScreen;
    private AppWindowPresenterKind _restorePresenterKind = AppWindowPresenterKind.Default;
    private OverlappedPresenterState _restoreWindowState = OverlappedPresenterState.Restored;
    private PointInt32 _restorePosition;
    private SizeInt32 _restoreSize;
    private bool _restorePaneOpen;
    private bool _closeRequested;
    private bool _closeConfirmed;

    public HomeViewModel Home { get; }
    public LoginViewModel Login { get; }
    public SearchViewModel SearchVM { get; }
    public FavoritesViewModel Favorites { get; }
    public HistoryViewModel History { get; }
    public LiveViewModel Live { get; }
    public SettingsViewModel Settings { get; }
    public AdminViewModel Admin { get; }

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        Home = _services.GetRequiredService<HomeViewModel>();
        Login = _services.GetRequiredService<LoginViewModel>();
        SearchVM = _services.GetRequiredService<SearchViewModel>();
        Favorites = _services.GetRequiredService<FavoritesViewModel>();
        History = _services.GetRequiredService<HistoryViewModel>();
        Live = _services.GetRequiredService<LiveViewModel>();
        Settings = _services.GetRequiredService<SettingsViewModel>();
        Admin = _services.GetRequiredService<AdminViewModel>();
        _playerViewModel = _services.GetRequiredService<PlayerViewModel>();
        _detailViewModel = _services.GetRequiredService<DetailViewModel>();
        _categoryViewModel = _services.GetRequiredService<CategoryViewModel>();
        _recordPersistence = new PlayRecordPersistence(_services.GetRequiredService<IPlayRecordStore>());
        _playerViewModel.RecordSaver = SavePlayRecordAsync;
        _doubanClient = _services.GetRequiredService<IDoubanClient>();
        _bangumiClient = _services.GetRequiredService<IBangumiClient>();
        _themeService = _services.GetRequiredService<IThemeService>();

        InitializeComponent();
        UiHelpers.InitializeThemeResources(Root);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var shell = BuildShell();
        Grid.SetRow(shell, 1);
        Root.Children.Add(shell);
        ApplyTheme(_themeService.Mode);
        _themeService.ModeChanged += OnThemeModeChanged;
        _settingsPage.ThemeChanged += OnThemeSelectionChanged;
        Root.ActualThemeChanged += OnActualThemeChanged;
        AppTitleBar.Loaded += OnTitleBarLoaded;
        AppTitleBar.PaneToggleRequested += OnPaneToggleRequested;
        Root.PreviewKeyDown += OnRootPreviewKeyDown;
        AppWindow.Closing += OnAppWindowClosing;
        AppWindow.Changed += OnAppWindowChanged;
        Closed += OnWindowClosed;
        Activated += OnFirstActivated;
        Activated += OnWindowActivated;
    }

    private NavigationView BuildShell()
    {
        _homePage = new HomePage();
        _loginPage = new LoginPage();
        _searchPage = new SearchPage();
        _favoritesPage = new FavoritesPage();
        _historyPage = new HistoryPage();
        _settingsPage = new SettingsPage();
        _livePage = new LivePage();
        _lunaFeaturePage = new LunaFeaturePage();
        _categoryPage = new CategoryPage();
        _detailPage = new DetailPage();
        _playerPage = new PlayerPage();
        _adminPage = new AdminPage();
        _playerPage.Bind(_playerViewModel);
        _playerPage.CloseRequested += OnPlayerCloseRequested;
        _playerPage.FullScreenRequested += OnPlayerFullScreenRequested;
        _playerPage.Left += OnPlayerPageLeft;
        _detailPage.PlayRequested += OnPlayEpisodeAsync;
        _homePage.PlayRecordClicked += OnHomePlayRecordClicked;
        _homePage.DoubanMovieClicked += OnHomeDoubanMovieClicked;
        _homePage.BangumiItemClicked += OnHomeBangumiItemClicked;
        _categoryPage.DoubanMovieClicked += OnHomeDoubanMovieClicked;
        _categoryPage.BangumiItemClicked += OnHomeBangumiItemClicked;
        _lunaFeaturePage.ShortDramaDetailRequested += OnShortDramaDetailRequested;
        _loginPage.SessionChanged += OnLoginSessionChangedAsync;
        _searchPage.DetailRequested += OnSearchDetailRequested;

        _contentHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        UiHelpers.SetThemeResource(_contentHost, Control.BackgroundProperty, "LayerFillColorDefaultBrush");

        _navigationView = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible = false,
            IsSettingsVisible = false,
            AlwaysShowHeader = false,
            OpenPaneLength = 240,
            CompactPaneLength = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _navigationView.MenuItems.Add(NavItem("首页", "home", Symbol.Home, isSelected: true));
        _navigationView.MenuItems.Add(NavItem("搜索", "search", Symbol.Find));
        _navigationView.MenuItems.Add(NavItem("电影", "movies", Symbol.Video));
        _navigationView.MenuItems.Add(NavItem("电视剧", "tv", Symbol.List));
        _navigationView.MenuItems.Add(NavItem("动漫", "anime", Symbol.AllApps));
        _navigationView.MenuItems.Add(NavItem("综艺", "shows", Symbol.Play));
        _navigationView.MenuItems.Add(NavItem("直播", "live", Symbol.Camera));
        _navigationView.MenuItems.Add(NavItem("短剧", "shortdrama", Symbol.Video));
        _navigationView.MenuItems.Add(NavItem("Bilibili", "bilibili", Symbol.World));
        _navigationView.MenuItems.Add(NavItem("YouTube", "youtube", Symbol.World));
        _navigationView.MenuItems.Add(NavItem("管理后台", "admin", Symbol.Admin));
        _navigationView.MenuItems.Add(new NavigationViewItemSeparator());
        _navigationView.MenuItems.Add(NavItem("收藏", "favorites", Symbol.Favorite));
        _navigationView.MenuItems.Add(NavItem("历史", "history", Symbol.Calendar));
        _accountNavigationItem = NavItem("登录", "login", Symbol.Contact);
        _navigationView.FooterMenuItems.Add(_accountNavigationItem);
        _navigationView.FooterMenuItems.Add(NavItem("设置", "settings", Symbol.Setting));
        _navigationView.ItemInvoked += OnNavigationItemInvoked;
        _navigationView.Content = _contentHost;

        UpdatePaneFooter();

        return _navigationView;
    }

    private static NavigationViewItem NavItem(string title, string tag, Symbol symbol, bool isSelected = false)
    {
        return new NavigationViewItem
        {
            Content = title,
            Tag = tag,
            Icon = new SymbolIcon(symbol),
            IsSelected = isSelected,
        };
    }

    private void UpdatePaneFooter()
    {
        if (_accountNavigationItem is null) return;
        _accountNavigationItem.Content = Login.Session?.Username ?? "登录";
        ToolTipService.SetToolTip(_accountNavigationItem,
            Login.Session is null ? "登录 LunaTV 服务端" : $"账户：{Login.Session.Username}");
    }

    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        if (_navigationView is not null)
        {
            _navigationView.IsPaneOpen = !_navigationView.IsPaneOpen;
        }
    }

    private void OnThemeSelectionChanged(string theme)
    {
        _themeService.Mode = theme switch
        {
            "浅色" => ThemeMode.Light,
            "深色" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }

    private void OnThemeModeChanged(ThemeMode mode)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyTheme(mode);
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => ApplyTheme(mode));
        }
    }

    private void ApplyTheme(ThemeMode mode)
    {
        Root.RequestedTheme = mode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        UpdateTitleBarColors();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => UpdateTitleBarColors();

    private void OnTitleBarLoaded(object sender, RoutedEventArgs args) => UpdateTitleBarColors();

    private void UpdateTitleBarColors()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        var titleBar = AppWindow.TitleBar;
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
        {
            titleBar.ForegroundColor = null;
            titleBar.BackgroundColor = null;
            titleBar.InactiveForegroundColor = null;
            titleBar.InactiveBackgroundColor = null;
            titleBar.ButtonForegroundColor = null;
            titleBar.ButtonBackgroundColor = null;
            titleBar.ButtonInactiveForegroundColor = null;
            titleBar.ButtonInactiveBackgroundColor = null;
            titleBar.ButtonHoverForegroundColor = null;
            titleBar.ButtonHoverBackgroundColor = null;
            titleBar.ButtonPressedForegroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            return;
        }

        var dark = Root.ActualTheme == ElementTheme.Dark;
        var foreground = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        var inactiveForeground = dark ? Color.FromArgb(255, 153, 153, 153) : Color.FromArgb(255, 102, 102, 102);
        titleBar.ForegroundColor = foreground;
        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = dark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0);
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = dark ? Color.FromArgb(15, 255, 255, 255) : Color.FromArgb(15, 0, 0, 0);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        ++_navigationGeneration;
        _themeService.ModeChanged -= OnThemeModeChanged;
        _settingsPage.ThemeChanged -= OnThemeSelectionChanged;
        Root.ActualThemeChanged -= OnActualThemeChanged;
        AppTitleBar.Loaded -= OnTitleBarLoaded;
        AppTitleBar.PaneToggleRequested -= OnPaneToggleRequested;
        Root.PreviewKeyDown -= OnRootPreviewKeyDown;
        AppWindow.Closing -= OnAppWindowClosing;
        AppWindow.Changed -= OnAppWindowChanged;
        Activated -= OnFirstActivated;
        Activated -= OnWindowActivated;
        Closed -= OnWindowClosed;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        // In full screen only the player is on screen. If full screen started while
        // the window was inactive, nothing in it may hold keyboard focus, and Esc and
        // the other shortcuts would go nowhere.
        if (_isPlayerFullScreen && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, _playerPage.EnsureKeyboardFocus);
        }
    }

    /// <summary>
    /// Closing waits for the playback record. The record is snapshotted and
    /// playback stopped at once; the window goes away when the write has finished,
    /// instead of leaving a write behind in a process that is already exiting.
    /// </summary>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed) return;

        var saving = LeavePlayer();
        if (saving.IsCompleted) return;

        args.Cancel = true;
        if (_closeRequested) return;
        _closeRequested = true;

        // The local copy is written first, so this bound only limits a slow server.
        await Task.WhenAny(saving, Task.Delay(TimeSpan.FromSeconds(5)));
        _closeConfirmed = true;
        Close();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // Something other than the player (for example a system shortcut) left full screen.
        if (args.DidPresenterChange &&
            _isPlayerFullScreen &&
            sender.Presenter.Kind != AppWindowPresenterKind.FullScreen)
        {
            _isPlayerFullScreen = false;
            ApplyFullScreenChrome(false);
        }
    }

    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Player shortcuts exist only while the player page is the active page.
        if (ReferenceEquals(_contentHost?.Content, _playerPage))
        {
            _playerPage.HandlePreviewKeyDown(e);
        }
    }

    private void OnPlayerFullScreenRequested(object? sender, bool fullScreen) => SetPlayerFullScreen(fullScreen);

    /// <summary>
    /// Real desktop full screen through the window presenter. The player page stays
    /// where it is in the visual tree, so the video element keeps its media player
    /// and playback is not interrupted; only the surrounding chrome is hidden.
    /// </summary>
    private void SetPlayerFullScreen(bool fullScreen)
    {
        if (_isPlayerFullScreen == fullScreen) return;
        if (fullScreen && !ReferenceEquals(_contentHost?.Content, _playerPage)) return;

        _isPlayerFullScreen = fullScreen;
        if (fullScreen)
        {
            _restorePresenterKind = AppWindow.Presenter.Kind;
            _restoreWindowState = AppWindow.Presenter is OverlappedPresenter overlapped
                ? overlapped.State
                : OverlappedPresenterState.Restored;
            _restorePosition = AppWindow.Position;
            _restoreSize = AppWindow.Size;
            _restorePaneOpen = _navigationView?.IsPaneOpen ?? false;
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            AppWindow.SetPresenter(_restorePresenterKind == AppWindowPresenterKind.FullScreen
                ? AppWindowPresenterKind.Default
                : _restorePresenterKind);
            RestoreWindowPlacement();
        }

        ApplyFullScreenChrome(fullScreen);
    }

    private void RestoreWindowPlacement()
    {
        if (AppWindow.Presenter is not OverlappedPresenter overlapped) return;

        if (_restoreWindowState == OverlappedPresenterState.Maximized)
        {
            if (overlapped.State != OverlappedPresenterState.Maximized)
            {
                overlapped.Maximize();
            }

            return;
        }

        if (_restoreWindowState != OverlappedPresenterState.Restored) return;

        if (overlapped.State != OverlappedPresenterState.Restored)
        {
            overlapped.Restore();
        }

        var position = AppWindow.Position;
        var size = AppWindow.Size;
        if (position.X != _restorePosition.X || position.Y != _restorePosition.Y ||
            size.Width != _restoreSize.Width || size.Height != _restoreSize.Height)
        {
            AppWindow.MoveAndResize(new RectInt32(
                _restorePosition.X,
                _restorePosition.Y,
                _restoreSize.Width,
                _restoreSize.Height));
        }
    }

    private void ApplyFullScreenChrome(bool fullScreen)
    {
        AppTitleBar.Visibility = fullScreen ? Visibility.Collapsed : Visibility.Visible;
        if (_navigationView is not null)
        {
            if (fullScreen)
            {
                _navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
                _navigationView.IsPaneVisible = false;
            }
            else
            {
                _navigationView.IsPaneVisible = true;
                _navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;

                // The display mode settles with the restored window size; a pane the
                // viewer had collapsed in the wide layout is collapsed again then.
                var paneWasOpen = _restorePaneOpen;
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (!_isPlayerFullScreen &&
                        _navigationView.DisplayMode == NavigationViewDisplayMode.Expanded &&
                        _navigationView.IsPaneOpen != paneWasOpen)
                    {
                        _navigationView.IsPaneOpen = paneWasOpen;
                    }
                });
            }
        }

        // Covers the content area's 1px top border, which would show above the picture.
        if (_contentHost is not null)
        {
            _contentHost.Margin = fullScreen ? new Thickness(0, -1, 0, 0) : new Thickness(0);
        }

        _playerPage.ApplyFullScreenLayout(fullScreen);
    }

    /// <summary>
    /// Leaves the player if it is active: restores the window, snapshots the
    /// playback record while the engine still holds the item, and stops playback.
    /// Returns the pending record write.
    /// </summary>
    private Task LeavePlayer()
    {
        SetPlayerFullScreen(false);
        return _playerViewModel.HasCurrentItem || ReferenceEquals(_contentHost?.Content, _playerPage)
            ? _playerViewModel.CloseAsync()
            : _playerViewModel.PendingSaves;
    }

    private void OnPlayerPageLeft(object? sender, EventArgs e)
    {
        // Safety net for any navigation that replaces the page without going through ShowPageAsync.
        if (!ReferenceEquals(_contentHost?.Content, _playerPage))
        {
            _ = LeavePlayer();
        }
    }

    private async Task SavePlayRecordAsync(PlayRecord record)
    {
        using var remoteTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _recordPersistence.SaveAsync(record, Login.CreateProvider(), remoteTimeout.Token);
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var navigationGeneration = _navigationGeneration;
        await Login.LoadAsync();
        ConfigureBackendClients();
        UpdatePaneFooter();
        if (navigationGeneration != _navigationGeneration) return;
        await ShowPageAsync("home");
    }

    private async void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string page)
        {
            await ShowPageAsync(page);
        }
    }

    private async void ShowPage(string page)
    {
        await ShowPageAsync(page);
    }

    private async Task ShowPageAsync(string page)
    {
        var navigationGeneration = ++_navigationGeneration;
        var recordSaved = LeavePlayer();
        if (_navigationView is not null)
        {
            _navigationView.SelectedItem = _navigationView.MenuItems
                .Concat(_navigationView.FooterMenuItems)
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag as string == page);
            if (_navigationView.DisplayMode != NavigationViewDisplayMode.Expanded)
            {
                _navigationView.IsPaneOpen = false;
            }
        }
        var provider = Login.CreateProvider();
        switch (page)
        {
            case "home":
                var homeLoading = new Border
                {
                    Padding = new Thickness(28),
                    Child = UiHelpers.PageHeader("首页", "正在加载推荐与观看记录…", isLoading: true),
                };
                _contentHost!.Content = homeLoading;

                // Continue-watching must already include what was just being played.
                await Task.WhenAny(recordSaved, Task.Delay(TimeSpan.FromSeconds(3)));
                if (navigationGeneration != _navigationGeneration) return;
                await Home.LoadAsync(provider);
                if (navigationGeneration != _navigationGeneration) return;
                _homePage.Build(Home);
                _contentHost.Content = _homePage;
                break;
            case "login":
                _loginPage.Build(Login);
                _contentHost!.Content = _loginPage;
                break;
            case "search":
                if (Login.Session is not null)
                {
                    _searchPage.Build(SearchVM, provider, Login.Session.ServerUrl, Login.Session.Cookie);
                }
                else
                {
                    _searchPage.Build(SearchVM, provider);
                }
                _contentHost!.Content = _searchPage;
                break;
            case "favorites":
                await Favorites.LoadAsync(provider);
                if (navigationGeneration != _navigationGeneration) return;
                _favoritesPage.Build(Favorites, provider);
                _contentHost!.Content = _favoritesPage;
                break;
            case "history":
                await History.LoadAsync(provider);
                if (navigationGeneration != _navigationGeneration) return;
                _historyPage.Build(History, provider);
                _contentHost!.Content = _historyPage;
                break;
            case "settings":
                _settingsPage.Build(Settings, Login.Session?.ServerUrl);
                _contentHost!.Content = _settingsPage;
                break;
            case "shortdrama":
            case "bilibili":
            case "youtube":
                await _lunaFeaturePage.BuildAsync(page, provider);
                if (navigationGeneration != _navigationGeneration) return;
                _contentHost!.Content = _lunaFeaturePage;
                break;
            case "admin":
                await _adminPage.BuildAsync(Admin, provider);
                if (navigationGeneration != _navigationGeneration) return;
                _contentHost!.Content = _adminPage;
                break;
            case "live":
                await ShowLiveAsync(provider, navigationGeneration);
                break;
            case "movies":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "movie", reset: true);
                if (navigationGeneration != _navigationGeneration) return;
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "tv":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "tv", reset: true);
                if (navigationGeneration != _navigationGeneration) return;
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "anime":
                await _categoryViewModel.LoadAnimeAsync(_bangumiClient, reset: true);
                if (navigationGeneration != _navigationGeneration) return;
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "shows":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "shows", reset: true);
                if (navigationGeneration != _navigationGeneration) return;
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            default:
                _contentHost!.Content = UiHelpers.PageHeader(PageTitle(page), "这个模块还在迁移中。");
                break;
        }
    }

    private async Task ShowLiveAsync(IContentProvider? provider, int navigationGeneration)
    {
        await Live.LoadSourcesAsync(provider);
        if (navigationGeneration != _navigationGeneration) return;
        _livePage.Build(Live, provider);
        _contentHost!.Content = _livePage;
    }

    private async Task OnLoginSessionChangedAsync()
    {
        ConfigureBackendClients();
        UpdatePaneFooter();
        await ShowPageAsync("home");
    }

    private void ConfigureBackendClients()
    {
        if (_doubanClient is DoubanClient doubanClient)
        {
            doubanClient.ConfigureBackend(Login.Session?.ServerUrl, Login.Session?.Cookie ?? string.Empty);
        }
    }

    private async Task OnPlayEpisodeAsync(SearchResult detail, string episodeTitle, string episodeUrl, int episodeNumber)
    {
        ++_navigationGeneration;
        var provider = Login.CreateProvider();

        // The other sources of this work travel with the play request, so the
        // player can switch source without searching again.
        var candidates = _detailViewModel.SourceCandidates;
        _playerPage.SetProvider(provider);
        _contentHost!.Content = _playerPage;
        await _playerPage.OpenAsync(detail, episodeNumber - 1, provider, candidates);
    }

    private async void OnPlayerCloseRequested(object? sender, EventArgs e)
    {
        // ShowPageAsync leaves the player first: record snapshot, stop, then home.
        await ShowPageAsync("home");
    }

    /// <summary>
    /// Shows the detail page. <paramref name="candidates"/> are the search results
    /// the item came from; the ones that are the same work become its sources.
    /// </summary>
    public async void ShowDetail(
        SearchResult result,
        IContentProvider? provider,
        IEnumerable<SearchResult>? candidates = null)
    {
        var navigationGeneration = ++_navigationGeneration;
        _detailPage.PlayRequested -= OnPlayEpisodeAsync;
        _detailPage.PlayRequested += OnPlayEpisodeAsync;
        await _detailViewModel.LoadAsync(result, provider, _doubanClient, candidates: candidates);
        if (navigationGeneration != _navigationGeneration) return;
        _detailPage.Build(_detailViewModel, provider);
        _contentHost!.Content = _detailPage;
    }

    private void OnSearchDetailRequested(
        SearchResult result,
        IContentProvider? provider,
        IReadOnlyList<SearchResult> candidates)
    {
        ShowDetail(result, provider, candidates);
    }

    private void OnShortDramaDetailRequested(SearchResult result, IContentProvider? provider)
    {
        ShowDetail(result, provider);
    }

    private async void OnHomePlayRecordClicked(PlayRecord record)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;

        var navigationGeneration = ++_navigationGeneration;
        _playerPage.SetProvider(provider);
        _contentHost!.Content = _playerPage;
        await _playerPage.WaitUntilVideoSurfaceReadyAsync();
        if (navigationGeneration != _navigationGeneration) return;
        await _playerViewModel.LoadDetailAndPlayAsync(record, provider);
        if (navigationGeneration != _navigationGeneration) return;
        await _playerPage.LoadCurrentMetadataAsync();
    }

    private async void OnHomeDoubanMovieClicked(DoubanMovie movie, string category)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;
        var navigationGeneration = ++_navigationGeneration;

        try
        {
            var results = await provider.SearchAsync(movie.Title);
            if (navigationGeneration != _navigationGeneration) return;
            if (results.Count > 0)
            {
                var match = results.FirstOrDefault(r =>
                    r.Title.Equals(movie.Title, StringComparison.OrdinalIgnoreCase))
                    ?? results[0];
                ShowDetail(match, provider, results);
            }
            else
            {
                ShowPage("search");
                await _searchPage.SearchAndRenderAsync(movie.Title);
            }
        }
        catch
        {
            if (navigationGeneration != _navigationGeneration) return;
            ShowPage("search");
        }
    }

    private async void OnHomeBangumiItemClicked(BangumiItem item)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;
        var navigationGeneration = ++_navigationGeneration;

        try
        {
            var results = await provider.SearchAsync(item.DisplayTitle);
            if (navigationGeneration != _navigationGeneration) return;
            if (results.Count > 0)
            {
                var match = results.FirstOrDefault(r =>
                    r.Title.Equals(item.DisplayTitle, StringComparison.OrdinalIgnoreCase))
                    ?? results[0];
                ShowDetail(match, provider, results);
            }
            else
            {
                ShowPage("search");
                await _searchPage.SearchAndRenderAsync(item.DisplayTitle);
            }
        }
        catch
        {
            if (navigationGeneration != _navigationGeneration) return;
            ShowPage("search");
        }
    }

    private static string PageTitle(string page) => page switch
    {
        "home" => "首页",
        "search" => "搜索",
        "movies" => "电影",
        "tv" => "电视剧",
        "anime" => "动漫",
        "shows" => "综艺",
        "live" => "直播",
        "login" => "登录",
        "admin" => "管理后台",
        "favorites" => "收藏",
        "history" => "历史",
        "settings" => "设置",
        _ => "Selene",
    };
}
