using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly PlayerViewModel _playerViewModel;
    private readonly IPlayRecordStore _playRecordStore;
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
    private CategoryPage _categoryPage = null!;
    private DetailPage _detailPage = null!;
    private PlayerPage _playerPage = null!;

    public HomeViewModel Home { get; }
    public LoginViewModel Login { get; }
    public SearchViewModel SearchVM { get; }
    public FavoritesViewModel Favorites { get; }
    public HistoryViewModel History { get; }
    public LiveViewModel Live { get; }
    public SettingsViewModel Settings { get; }

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
        _playerViewModel = _services.GetRequiredService<PlayerViewModel>();
        _detailViewModel = _services.GetRequiredService<DetailViewModel>();
        _categoryViewModel = _services.GetRequiredService<CategoryViewModel>();
        _playRecordStore = _services.GetRequiredService<IPlayRecordStore>();
        _doubanClient = _services.GetRequiredService<IDoubanClient>();
        _bangumiClient = _services.GetRequiredService<IBangumiClient>();

        InitializeComponent();
        Root.Children.Add(BuildShell());
        Activated += OnFirstActivated;
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
        _categoryPage = new CategoryPage();
        _detailPage = new DetailPage();
        _playerPage = new PlayerPage();
        _playerPage.Bind(_playerViewModel);
        _playerPage.CloseRequested += OnPlayerCloseRequested;
        _playerPage.SaveRecordRequested += OnPlayerSaveRecordAsync;
        _detailPage.PlayRequested += OnPlayEpisodeAsync;
        _homePage.PlayRecordClicked += OnHomePlayRecordClicked;
        _homePage.DoubanMovieClicked += OnHomeDoubanMovieClicked;
        _homePage.BangumiItemClicked += OnHomeBangumiItemClicked;
        _categoryPage.DoubanMovieClicked += OnHomeDoubanMovieClicked;
        _categoryPage.BangumiItemClicked += OnHomeBangumiItemClicked;
        _loginPage.SessionChanged += OnLoginSessionChangedAsync;

        _contentHost = new ContentControl();

        _navigationView = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            OpenPaneLength = 220,
            CompactPaneLength = 56,
            Header = "首页",
        };
        _navigationView.MenuItems.Add(NavItem("首页", "home", isSelected: true));
        _navigationView.MenuItems.Add(NavItem("搜索", "search"));
        _navigationView.MenuItems.Add(NavItem("电影", "movies"));
        _navigationView.MenuItems.Add(NavItem("电视剧", "tv"));
        _navigationView.MenuItems.Add(NavItem("动漫", "anime"));
        _navigationView.MenuItems.Add(NavItem("综艺", "shows"));
        _navigationView.MenuItems.Add(NavItem("直播", "live"));
        _navigationView.MenuItems.Add(new NavigationViewItemSeparator());
        _navigationView.MenuItems.Add(NavItem("登录", "login"));
        _navigationView.MenuItems.Add(NavItem("收藏", "favorites"));
        _navigationView.MenuItems.Add(NavItem("历史", "history"));
        _navigationView.MenuItems.Add(NavItem("设置", "settings"));
        _navigationView.ItemInvoked += OnNavigationItemInvoked;
        _navigationView.Content = _contentHost;
        return _navigationView;
    }

    private static NavigationViewItem NavItem(string title, string tag, bool isSelected = false)
    {
        return new NavigationViewItem
        {
            Content = title,
            Tag = tag,
            IsSelected = isSelected,
        };
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        await Login.LoadAsync();
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
        if (_navigationView is not null)
        {
            _navigationView.Header = PageTitle(page);
        }

        var provider = Login.CreateProvider();
        switch (page)
        {
            case "home":
                await Home.LoadAsync(provider);
                _homePage.Build(Home);
                _contentHost!.Content = _homePage;
                break;
            case "login":
                _loginPage.Build(Login);
                _contentHost!.Content = _loginPage;
                break;
            case "search":
                _searchPage.DetailRequested += OnSearchDetailRequested;
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
                _favoritesPage.Build(Favorites, provider);
                _contentHost!.Content = _favoritesPage;
                break;
            case "history":
                await History.LoadAsync(provider);
                _historyPage.Build(History, provider);
                _contentHost!.Content = _historyPage;
                break;
            case "settings":
                _settingsPage.Build(Settings);
                _contentHost!.Content = _settingsPage;
                break;
            case "live":
                await ShowLiveAsync(provider);
                break;
            case "movies":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "movie", reset: true);
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "tv":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "tv", reset: true);
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "anime":
                await _categoryViewModel.LoadAnimeAsync(_bangumiClient, reset: true);
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            case "shows":
                await _categoryViewModel.LoadMoviesAsync(_doubanClient, "shows", reset: true);
                _categoryPage.Build(_categoryViewModel, _doubanClient, _bangumiClient);
                _contentHost!.Content = _categoryPage;
                break;
            default:
                _contentHost!.Content = UiHelpers.PageHeader(PageTitle(page), "这个模块还在迁移中。");
                break;
        }
    }

    private async Task ShowLiveAsync(IContentProvider? provider)
    {
        await Live.LoadSourcesAsync(provider);
        _livePage.Build(Live, provider);
        _contentHost!.Content = _livePage;
    }

    private async Task OnLoginSessionChangedAsync()
    {
        await ShowPageAsync("home");
    }

    private async Task OnPlayEpisodeAsync(SearchResult detail, string episodeTitle, string episodeUrl, int episodeNumber)
    {
        _contentHost!.Content = _playerPage;
        await _playerPage.OpenAsync(detail, episodeTitle, episodeUrl, episodeNumber);
    }

    private void OnPlayerCloseRequested(object? sender, EventArgs e)
    {
        _playerViewModel.Stop();
        ShowPage("home");
    }

    private async Task OnPlayerSaveRecordAsync(PlayRecord record)
    {
        var provider = Login.CreateProvider();
        if (provider is not null)
        {
            try
            {
                await provider.SavePlayRecordAsync(record);
            }
            catch
            {
                // Ignore remote save failures; persist locally as a fallback.
            }
        }

        try
        {
            await _playRecordStore.SaveAsync(record);
        }
        catch
        {
            // Local persistence is best-effort; missing the write should not block the close path.
        }
    }

    public async void ShowDetail(SearchResult result, IContentProvider? provider)
    {
        _detailPage.PlayRequested -= OnPlayEpisodeAsync;
        _detailPage.PlayRequested += OnPlayEpisodeAsync;
        await _detailViewModel.LoadAsync(result, provider, _doubanClient);
        _detailPage.Build(_detailViewModel, provider);
        _contentHost!.Content = _detailPage;
    }

    private void OnSearchDetailRequested(SearchResult result, IContentProvider? provider)
    {
        ShowDetail(result, provider);
    }

    private async void OnHomePlayRecordClicked(PlayRecord record)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;

        _contentHost!.Content = _playerPage;
        await _playerPage.WaitUntilVideoSurfaceReadyAsync();
        await _playerViewModel.LoadDetailAndPlayAsync(record, provider);
    }

    private async void OnHomeDoubanMovieClicked(DoubanMovie movie, string category)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;

        try
        {
            var results = await provider.SearchAsync(movie.Title);
            if (results.Count > 0)
            {
                var match = results.FirstOrDefault(r => 
                    r.Title.Equals(movie.Title, StringComparison.OrdinalIgnoreCase)) 
                    ?? results[0];
                ShowDetail(match, provider);
            }
            else
            {
                ShowPage("search");
                await _searchPage.SearchAndRenderAsync(movie.Title);
            }
        }
        catch
        {
            ShowPage("search");
        }
    }

    private async void OnHomeBangumiItemClicked(BangumiItem item)
    {
        var provider = Login.CreateProvider();
        if (provider is null) return;

        try
        {
            var results = await provider.SearchAsync(item.DisplayTitle);
            if (results.Count > 0)
            {
                var match = results.FirstOrDefault(r => 
                    r.Title.Equals(item.DisplayTitle, StringComparison.OrdinalIgnoreCase)) 
                    ?? results[0];
                ShowDetail(match, provider);
            }
            else
            {
                ShowPage("search");
                await _searchPage.SearchAndRenderAsync(item.DisplayTitle);
            }
        }
        catch
        {
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
        "favorites" => "收藏",
        "history" => "历史",
        "settings" => "设置",
        _ => "Selene",
    };
}
