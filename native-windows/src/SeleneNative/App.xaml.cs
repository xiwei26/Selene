using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using SeleneNative.Services;

namespace SeleneNative;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow(Services);
        _mainWindow.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Media engine
        services.AddWindowsMediaPlayer();

        // Persistence / caching
        services.AddSingleton<ISessionStore>(_ => new SessionStore());
        services.AddSingleton<IPlayRecordStore>(_ => new PlayRecordStore());
        services.AddSingleton<IDoubanClient>(_ => new DoubanClient());
        services.AddSingleton<IBangumiClient>(_ => new BangumiClient());
        services.AddSingleton<ICacheService>(_ => new CacheService());
        services.AddSingleton<IVersionService>(_ => new VersionService());
        services.AddSingleton<IThemeService>(_ => new ThemeService());

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<LiveViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AdminViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<DetailViewModel>();
        services.AddSingleton<CategoryViewModel>();

        return services.BuildServiceProvider();
    }
}
