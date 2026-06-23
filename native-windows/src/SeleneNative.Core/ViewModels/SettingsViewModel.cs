using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISessionStore _sessionStore;
    private readonly ICacheService _cacheService;
    private readonly IVersionService _versionService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private string _theme = "跟随系统";

    [ObservableProperty]
    private bool _openHomeOnLaunch = true;

    [ObservableProperty]
    private string _m3u8ProxyUrl = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _updateMessage;

    [ObservableProperty]
    private long _cacheSizeBytes;

    public SettingsViewModel(
        ISessionStore? sessionStore = null,
        ICacheService? cacheService = null,
        IVersionService? versionService = null,
        IThemeService? themeService = null)
    {
        _sessionStore = sessionStore ?? new SessionStore();
        _cacheService = cacheService ?? new CacheService();
        _versionService = versionService ?? new VersionService();
        _themeService = themeService ?? new ThemeService();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _themeService.Mode = Theme switch
            {
                "浅色" => ThemeMode.Light,
                "深色" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
            ErrorMessage = null;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        UpdateMessage = null;
        try
        {
            var info = await _versionService.CheckForUpdateAsync("1.0.0").ConfigureAwait(false);
            if (info is null)
            {
                UpdateMessage = "当前已是最新版本";
            }
            else
            {
                UpdateMessage = $"发现新版本 {info.Version}";
                if (!string.IsNullOrWhiteSpace(info.ReleaseNotes))
                {
                    UpdateMessage += $"\n{info.ReleaseNotes}";
                }
                _versionService.Dismiss(info.Version);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            await _cacheService.ClearExpiredAsync().ConfigureAwait(false);
            CacheSizeBytes = 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _sessionStore.ClearAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
