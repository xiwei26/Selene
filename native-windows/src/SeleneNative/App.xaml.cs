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
        var mainWindow = new MainWindow(Services);
        _mainWindow = mainWindow;
        mainWindow.Activate();
        OnMainWindowReady(mainWindow);
    }

    /// <summary>
    /// Implemented only in UI smoke-test builds (<c>-p:SeleneUiSmoke=true</c>);
    /// in every other build the call above is removed by the compiler.
    /// </summary>
    partial void OnMainWindowReady(MainWindow window);

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Media engine
        services.AddWindowsMediaPlayer();

        // SELENE_NATIVE_DATA_DIR moves every local file (session, records, settings,
        // cache) to another folder, so a test instance never reads or overwrites the
        // data of the installed app. Unset, the default per-user folder is used.
        var dataDirectory = Environment.GetEnvironmentVariable("SELENE_NATIVE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = null;
        }

        string? DataFile(string name) => dataDirectory is null ? null : Path.Combine(dataDirectory, name);

        // Persistence / caching
        services.AddSingleton<ISessionStore>(_ => new SessionStore(DataFile("session.json")));
        services.AddSingleton<IPlayRecordStore>(_ => new PlayRecordStore(DataFile("playrecords.json")));
        services.AddSingleton<IDoubanClient>(_ => new DoubanClient());
        services.AddSingleton<IBangumiClient>(_ => new BangumiClient());
        services.AddSingleton<ICacheService>(_ => new CacheService(DataFile("Cache")));
        services.AddSingleton<IVersionService>(_ => new VersionService(localAppDataPath: dataDirectory));
        services.AddSingleton<IThemeService>(_ => new ThemeService(dataDirectory));
        services.AddSingleton<IPlayerSettingsStore>(_ => new PlayerSettingsStore(dataDirectory));

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<LiveViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AdminViewModel>();
        services.AddSingleton(provider => new PlayerViewModel(
            provider.GetRequiredService<IMediaPlayer>(),
            provider.GetRequiredService<IPlayerSettingsStore>()));
        services.AddSingleton<DetailViewModel>();
        services.AddSingleton<CategoryViewModel>();

        return services.BuildServiceProvider();
    }
}
