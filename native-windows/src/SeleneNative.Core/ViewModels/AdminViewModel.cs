using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class AdminViewModel : ObservableObject
{
    private IContentProvider? _provider;

    private static readonly string[] DefaultYouTubeRegions = ["US", "CN", "JP", "KR", "GB", "DE", "FR"];
    private static readonly string[] DefaultYouTubeCategories = ["Film & Animation", "Music", "Gaming", "News & Politics", "Entertainment"];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    // YouTube config
    [ObservableProperty]
    private bool _youTubeEnabled;

    [ObservableProperty]
    private bool _youTubeEnableDemo = true;

    [ObservableProperty]
    private string _youTubeApiKey = string.Empty;

    [ObservableProperty]
    private int _youTubeMaxResults = 25;

    [ObservableProperty]
    private List<string> _youTubeEnabledRegions = [.. YouTubeAdminConfig.DefaultRegions];

    [ObservableProperty]
    private List<string> _youTubeEnabledCategories = [.. YouTubeAdminConfig.DefaultCategories];

    // Bilibili config
    [ObservableProperty]
    private bool _bilibiliEnabled;

    [ObservableProperty]
    private string? _bilibiliLoginStatus;

    [ObservableProperty]
    private BilibiliAdminUserInfo? _bilibiliUserInfo;

    // Admin role
    [ObservableProperty]
    private string? _currentRole;

    public bool IsAdminOrOwner => CurrentRole is "admin" or "owner";

    // Available options for YouTube
    public static IReadOnlyList<RegionOption> AvailableRegions { get; } =
    [
        new("US", "美国"), new("CN", "中国"), new("JP", "日本"),
        new("KR", "韩国"), new("GB", "英国"), new("DE", "德国"),
        new("FR", "法国"), new("CA", "加拿大"), new("AU", "澳大利亚"),
        new("IN", "印度")
    ];

    public static IReadOnlyList<string> AvailableCategories { get; } =
    [
        "Film & Animation", "Autos & Vehicles", "Music",
        "Pets & Animals", "Sports", "Travel & Events",
        "Gaming", "People & Blogs", "Comedy",
        "Entertainment", "News & Politics", "Howto & Style",
        "Education", "Science & Technology", "Nonprofits & Activism"
    ];

    [RelayCommand]
    private async Task LoadAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        _provider = provider;
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        // Clear sensitive state before loading to prevent stale data leaks
        CurrentRole = null;
        YouTubeApiKey = string.Empty;
        YouTubeEnabled = false;
        YouTubeEnableDemo = true;
        YouTubeMaxResults = 25;
        YouTubeEnabledRegions = [.. DefaultYouTubeRegions];
        YouTubeEnabledCategories = [.. DefaultYouTubeCategories];
        BilibiliEnabled = false;
        BilibiliLoginStatus = null;
        BilibiliUserInfo = null;
        OnPropertyChanged(nameof(IsAdminOrOwner));

        try
        {
            var config = await _provider!.GetAdminConfigAsync(cancellationToken).ConfigureAwait(false);
            if (config is null)
            {
                ErrorMessage = "无法加载管理配置，请确认您拥有管理员权限。";
                return;
            }

            CurrentRole = config.Role;
            OnPropertyChanged(nameof(IsAdminOrOwner));

            // YouTube
            if (config.YouTubeConfig is not null)
            {
                YouTubeEnabled = config.YouTubeConfig.Enabled;
                YouTubeEnableDemo = config.YouTubeConfig.EnableDemo;
                YouTubeApiKey = config.YouTubeConfig.ApiKey;
                YouTubeMaxResults = config.YouTubeConfig.MaxResults;
                YouTubeEnabledRegions = [.. config.YouTubeConfig.EnabledRegions];
                YouTubeEnabledCategories = [.. config.YouTubeConfig.EnabledCategories];
            }

            // Bilibili
            if (config.BilibiliConfig is not null)
            {
                BilibiliEnabled = config.BilibiliConfig.Enabled;
                BilibiliLoginStatus = config.BilibiliConfig.LoginStatus;
                BilibiliUserInfo = config.BilibiliConfig.UserInfo;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveYouTubeAsync(CancellationToken cancellationToken = default)
    {
        if (_provider is null) return;
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        if (YouTubeEnabled && !YouTubeEnableDemo && string.IsNullOrWhiteSpace(YouTubeApiKey))
        {
            ErrorMessage = "请填写 YouTube API 密钥或启用演示模式";
            IsLoading = false;
            return;
        }

        if (YouTubeMaxResults < 1 || YouTubeMaxResults > 50)
        {
            ErrorMessage = "最大结果数应在 1-50 之间";
            IsLoading = false;
            return;
        }

        try
        {
            var config = new YouTubeAdminConfig
            {
                Enabled = YouTubeEnabled,
                ApiKey = YouTubeApiKey,
                EnableDemo = YouTubeEnableDemo,
                MaxResults = YouTubeMaxResults,
                EnabledRegions = YouTubeEnabledRegions,
                EnabledCategories = YouTubeEnabledCategories,
            };

            await _provider.SaveYouTubeConfigAsync(config, cancellationToken).ConfigureAwait(false);
            SuccessMessage = "YouTube 配置保存成功";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveBilibiliAsync(CancellationToken cancellationToken = default)
    {
        if (_provider is null) return;
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            await _provider.SaveBilibiliConfigAsync(BilibiliEnabled, cancellationToken).ConfigureAwait(false);
            SuccessMessage = "Bilibili 配置保存成功";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ToggleYouTubeRegion(string code)
    {
        if (YouTubeEnabledRegions.Contains(code))
        {
            YouTubeEnabledRegions = [.. YouTubeEnabledRegions.Where(r => r != code)];
        }
        else
        {
            YouTubeEnabledRegions = [.. YouTubeEnabledRegions, code];
        }
    }

    public void ToggleYouTubeCategory(string category)
    {
        if (YouTubeEnabledCategories.Contains(category))
        {
            YouTubeEnabledCategories = [.. YouTubeEnabledCategories.Where(c => c != category)];
        }
        else
        {
            YouTubeEnabledCategories = [.. YouTubeEnabledCategories, category];
        }
    }
}

public sealed record RegionOption(string Code, string Name);
