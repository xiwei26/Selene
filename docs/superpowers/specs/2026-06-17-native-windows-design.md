# Native Windows 设计

## 概述

为Selene项目构建一个独立的原生Windows客户端，使用WinUI 3和C#实现。该版本将包含与native-macos版本完全一致的所有22个功能，提供完整的Windows原生体验。

## 技术栈

- **UI框架**: WinUI 3 (Windows App SDK)
- **编程语言**: C# 12
- **项目系统**: MSBuild / .NET 8
- **视频播放**: libVLCSharp (基于VLC媒体播放器)
- **状态管理**: MVVM模式，使用CommunityToolkit.Mvvm
- **网络**: HttpClient
- **序列化**: System.Text.Json
- **存储**: ApplicationData.LocalSettings / 本地文件系统

## 项目结构

```
native-windows/
├── SeleneNative.sln
├── Directory.Build.props
├── global.json
├── src/
│   └── SeleneNative/
│       ├── SeleneNative.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── Models/
│       │   ├── AggregatedSearchResult.cs
│       │   ├── APIError.cs
│       │   ├── BangumiItem.cs
│       │   ├── DoubanMovie.cs
│       │   ├── FavoriteItem.cs
│       │   ├── LiveModels.cs
│       │   ├── LoginSession.cs
│       │   ├── PlayRecord.cs
│       │   ├── SearchResource.cs
│       │   ├── SearchResult.cs
│       │   └── SearchSuggestion.cs
│       ├── Services/
│       │   ├── BangumiAPIClient.cs
│       │   ├── CacheService.cs
│       │   ├── ContentFilterService.cs
│       │   ├── ContentProvider.cs
│       │   ├── DLNADiscoveryService.cs
│       │   ├── DoubanAPIClient.cs
│       │   ├── LiveService.cs
│       │   ├── M3U8Service.cs
│       │   ├── ServerAPIClient.cs
│       │   ├── SSESearchClient.cs
│       │   ├── SubscriptionService.cs
│       │   └── VersionService.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── LoginViewModel.cs
│       │   ├── SearchViewModel.cs
│       │   ├── DetailViewModel.cs
│       │   ├── PlayerViewModel.cs
│       │   ├── HomeViewModel.cs
│       │   ├── CategoryViewModel.cs
│       │   ├── LiveViewModel.cs
│       │   ├── FavoritesViewModel.cs
│       │   ├── HistoryViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Views/
│       │   ├── MainWindow.xaml
│       │   ├── MainWindow.xaml.cs
│       │   ├── LoginPage.xaml
│       │   ├── LoginPage.xaml.cs
│       │   ├── MainPage.xaml
│       │   ├── MainPage.xaml.cs
│       │   ├── SearchResultsPage.xaml
│       │   ├── SearchResultsPage.xaml.cs
│       │   ├── DetailPage.xaml
│       │   ├── DetailPage.xaml.cs
│       │   ├── PlayerPage.xaml
│       │   ├── PlayerPage.xaml.cs
│       │   ├── HomePage.xaml
│       │   ├── HomePage.xaml.cs
│       │   ├── CategoryPage.xaml
│       │   ├── CategoryPage.xaml.cs
│       │   ├── LivePage.xaml
│       │   ├── LivePage.xaml.cs
│       │   ├── LivePlayerPage.xaml
│       │   ├── LivePlayerPage.xaml.cs
│       │   ├── FavoritesPage.xaml
│       │   ├── FavoritesPage.xaml.cs
│       │   ├── HistoryPage.xaml
│       │   ├── HistoryPage.xaml.cs
│       │   ├── SettingsPage.xaml
│       │   └── SettingsPage.xaml.cs
│       ├── Helpers/
│       │   ├── URLNormalizer.cs
│       │   ├── JsonHelper.cs
│       │   └── NetworkHelper.cs
│       ├── Converters/
│       │   ├── BoolToVisibilityConverter.cs
│       │   ├── StringToImageSourceConverter.cs
│       │   └── ProgressToColorConverter.cs
│       └── Assets/
│           ├── Logo.ico
│           ├── Logo.png
│           └── ...
└── tests/
    └── SeleneNative.Tests/
        ├── SeleneNative.Tests.csproj
        ├── Models/
        ├── Services/
        └── ViewModels/
```

## 架构设计

### 分层架构

```
┌─────────────────────────────────────┐
│           Views (XAML)              │
├─────────────────────────────────────┤
│         ViewModels (MVVM)           │
├─────────────────────────────────────┤
│           Services                  │
├─────────────────────────────────────┤
│            Models                   │
└─────────────────────────────────────┘
```

### 数据流

```
用户操作 → View → ViewModel → Service → API/存储
                                    ↓
                              ViewModel → View → UI更新
```

### 依赖注入

使用Microsoft.Extensions.DependencyInjection进行依赖注入：

```csharp
// App.xaml.cs
public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<ContentProvider, ServerAPIClient>();
        services.AddSingleton<CacheService>();
        services.AddSingleton<SSESearchClient>();
        services.AddSingleton<DoubanAPIClient>();
        services.AddSingleton<BangumiAPIClient>();
        services.AddSingleton<LiveService>();
        services.AddSingleton<SubscriptionService>();
        services.AddSingleton<M3U8Service>();
        services.AddSingleton<DLNADiscoveryService>();
        services.AddSingleton<VersionService>();
        services.AddSingleton<ContentFilterService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<DetailViewModel>();
        services.AddTransient<PlayerViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<CategoryViewModel>();
        services.AddTransient<LiveViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
```

## 功能模块设计

### 1. 登录模块

**功能**:
- 服务器URL输入
- 用户名/密码登录
- 会话管理（Cookie持久化）
- 本地模式支持（隐藏入口）

**实现**:
- LoginViewModel处理登录逻辑
- 使用ApplicationData.LocalSettings存储会话信息
- 支持自动登录（如果Cookie有效）

### 2. 搜索模块

**功能**:
- 实时搜索建议
- SSE流式搜索
- 搜索结果聚合
- 搜索历史管理
- 过滤器（来源、年份、类型）

**实现**:
- SearchViewModel管理搜索状态
- SSESearchClient处理SSE连接
- AggregatedSearchResult实现结果聚合
- 使用增量更新UI

### 3. 详情模块

**功能**:
- 显示视频详情
- 豆瓣信息集成
- 选集功能
- 收藏功能

**实现**:
- DetailViewModel管理详情数据
- DoubanAPIClient获取豆瓣信息
- 支持多数据源切换

### 4. 播放模块

**功能**:
- 视频播放（libVLCSharp）
- 播放进度保存/恢复
- 多数据源切换
- 画中画支持
- DLNA投屏

**实现**:
- PlayerViewModel管理播放状态
- libVLCSharp提供播放能力
- 定时保存播放进度（10秒间隔）
- DLNADiscoveryService发现设备

### 5. 首页模块

**功能**:
- 继续观看
- 热门电影/电视剧
- 番组日历
- 热门综艺

**实现**:
- HomeViewModel聚合多个数据源
- 使用缓存提高加载速度
- 支持下拉刷新

### 6. 分类模块

**功能**:
- 电影/电视剧/动漫/综艺分类浏览
- 豆瓣数据驱动
- 网格布局

**实现**:
- CategoryViewModel管理分类数据
- DoubanAPIClient获取分类数据
- 支持分页加载

### 7. 直播模块

**功能**:
- 直播源管理
- 频道列表
- EPG节目单
- 直播播放

**实现**:
- LiveViewModel管理直播状态
- LiveService获取直播数据
- 支持M3U解析和EPG XML解析

### 8. 用户数据模块

**功能**:
- 收藏管理
- 播放历史
- 搜索历史

**实现**:
- FavoritesViewModel/HistoryViewModel
- 使用服务器API同步数据
- 支持本地缓存

### 9. 设置模块

**功能**:
- 主题切换（浅色/深色/跟随系统）
- 版本检查
- 关于信息
- 退出登录

**实现**:
- SettingsViewModel管理设置
- 使用ApplicationData.LocalSettings存储设置
- 支持实时主题切换

## 模型设计

### 搜索结果模型

```csharp
public class SearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("poster")]
    public string Poster { get; set; } = string.Empty;

    [JsonPropertyName("episodes")]
    public List<string> Episodes { get; set; } = new();

    [JsonPropertyName("episodes_titles")]
    public List<string> EpisodeTitles { get; set; } = new();

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("source_name")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    [JsonPropertyName("year")]
    public string Year { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Description { get; set; }

    [JsonPropertyName("type_name")]
    public string? TypeName { get; set; }

    [JsonPropertyName("douban_id")]
    public int? DoubanId { get; set; }
}
```

### 播放记录模型

```csharp
public class PlayRecord
{
    public string Id => $"{Source}+{ItemId}";
    public string Source { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public int Index { get; set; }
    public int TotalEpisodes { get; set; }
    public int PlayTime { get; set; }
    public int TotalTime { get; set; }
    public long SaveTime { get; set; }
    public string SearchTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public double ProgressPercentage => TotalTime > 0 ? (double)PlayTime / TotalTime : 0;

    [JsonIgnore]
    public string FormattedPlayTime => TimeSpan.FromSeconds(PlayTime).ToString(@"hh\:mm\:ss");

    [JsonIgnore]
    public string FormattedTotalTime => TimeSpan.FromSeconds(TotalTime).ToString(@"hh\:mm\:ss");
}
```

## 服务设计

### ContentProvider接口

```csharp
public interface ContentProvider
{
    // 认证
    Task<LoginSession> LoginAsync(string username, string password);
    
    // 搜索
    Task<List<SearchResult>> SearchAsync(string query);
    Task<SearchResult?> DetailAsync(string source, string id);
    Task<List<SearchResource>> SearchResourcesAsync();
    
    // 收藏
    Task<List<FavoriteItem>> GetFavoritesAsync();
    Task AddFavoriteAsync(string source, string id, Dictionary<string, object> data);
    Task RemoveFavoriteAsync(string source, string id);
    
    // 播放记录
    Task<List<PlayRecord>> GetPlayRecordsAsync();
    Task SavePlayRecordAsync(PlayRecord record);
    Task DeletePlayRecordAsync(string source, string id);
    Task ClearPlayRecordsAsync();
    
    // 搜索历史
    Task<List<string>> GetSearchHistoryAsync();
    Task AddSearchHistoryAsync(string query);
    Task DeleteSearchHistoryAsync(string query);
    Task ClearSearchHistoryAsync();
    
    // 搜索建议
    Task<List<SearchSuggestion>> SearchSuggestionsAsync(string query);
    
    // 直播
    Task<List<LiveSource>> GetLiveSourcesAsync();
    Task<List<LiveChannel>> GetLiveChannelsAsync(string sourceKey);
    Task<EpgData?> GetLiveEPGAsync(string tvgId, string sourceKey);
    
    // SSE搜索
    Uri? GetSSESearchURL(string query);
}
```

### ServerAPIClient实现

```csharp
public class ServerAPIClient : ContentProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseURL;
    private readonly string _cookie;

    public ServerAPIClient(Uri baseURL, string cookie = "")
    {
        _baseURL = baseURL;
        _cookie = cookie;
        _httpClient = new HttpClient();
    }

    public async Task<LoginSession> LoginAsync(string username, string password)
    {
        // 实现登录逻辑
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        // 实现搜索逻辑
    }

    // ... 其他方法实现
}
```

## 状态管理

### MVVM模式

使用CommunityToolkit.Mvvm实现MVVM模式：

```csharp
public partial class SearchViewModel : ObservableObject
{
    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private List<SearchResult> _results = new();

    [ObservableProperty]
    private string? _errorMessage;

    private readonly ContentProvider _contentProvider;

    public SearchViewModel(ContentProvider contentProvider)
    {
        _contentProvider = contentProvider;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Results = await _contentProvider.SearchAsync(Query);
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
}
```

## 持久化设计

### 存储策略

| 数据类型 | 存储方式 | 位置 |
|---------|---------|------|
| 会话信息 | ApplicationData.LocalSettings | 本地设置 |
| 主题设置 | ApplicationData.LocalSettings | 本地设置 |
| 缓存数据 | 本地文件系统 | ApplicationData.LocalFolder |
| 搜索历史 | 服务器API | 服务器端 |
| 收藏记录 | 服务器API | 服务器端 |
| 播放记录 | 服务器API | 服务器端 |

### 缓存策略

```csharp
public class CacheService
{
    private readonly string _cacheFolder;

    public CacheService()
    {
        _cacheFolder = ApplicationData.Current.LocalFolder.Path;
    }

    public async Task SaveAsync<T>(string key, T data, TimeSpan maxAge)
    {
        var filePath = GetCacheFilePath(key);
        var metaPath = GetMetaFilePath(key);

        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(filePath, json);

        var meta = new CacheMeta
        {
            SaveTime = DateTime.UtcNow,
            MaxAge = maxAge
        };
        var metaJson = JsonSerializer.Serialize(meta);
        await File.WriteAllTextAsync(metaPath, metaJson);
    }

    public async Task<T?> LoadAsync<T>(string key, TimeSpan maxAge)
    {
        var filePath = GetCacheFilePath(key);
        var metaPath = GetMetaFilePath(key);

        if (!File.Exists(filePath) || !File.Exists(metaPath))
            return default;

        var metaJson = await File.ReadAllTextAsync(metaPath);
        var meta = JsonSerializer.Deserialize<CacheMeta>(metaJson);

        if (meta == null || DateTime.UtcNow - meta.SaveTime > maxAge)
        {
            // 缓存过期
            File.Delete(filePath);
            File.Delete(metaPath);
            return default;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json);
    }

    private string GetCacheFilePath(string key) => Path.Combine(_cacheFolder, $"{key}.json");
    private string GetMetaFilePath(string key) => Path.Combine(_cacheFolder, $"{key}.meta");
}

public class CacheMeta
{
    public DateTime SaveTime { get; set; }
    public TimeSpan MaxAge { get; set; }
}
```

## 错误处理

### 错误类型

```csharp
public enum APIError
{
    InvalidURL,
    Unauthorized,
    ResponseError,
    NetworkTimeout,
    ParseError,
    NoResults,
    SSEConnectionFailed,
    PlaybackFailed
}

public class SeleneException : Exception
{
    public APIError ErrorType { get; }

    public SeleneException(APIError errorType, string message) : base(message)
    {
        ErrorType = errorType;
    }
}
```

### 错误处理策略

1. **网络错误**: 显示重试按钮，支持自动重试
2. **认证错误**: 清除会话，返回登录页面
3. **数据解析错误**: 显示友好错误信息
4. **播放错误**: 尝试切换数据源或显示错误信息

## 测试策略

### 单元测试

- 模型序列化/反序列化测试
- 服务层测试（使用Mock）
- ViewModel逻辑测试
- 工具类测试

### 集成测试

- API端点测试
- 数据流测试
- 状态管理测试

### UI测试

- 页面导航测试
- 用户交互测试
- 响应式布局测试

## 构建和部署

### 构建脚本

```powershell
# build.ps1
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "Building SeleneNative for Windows..."
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"

# 清理
dotnet clean src/SeleneNative/SeleneNative.csproj -c $Configuration -p:Platform=$Platform

# 构建
dotnet build src/SeleneNative/SeleneNative.csproj -c $Configuration -p:Platform=$Platform

# 发布
dotnet publish src/SeleneNative/SeleneNative.csproj -c $Configuration -p:Platform=$Platform -r win-$Platform --self-contained

Write-Host "Build completed!"
```

### 打包脚本

```powershell
# package.ps1
param(
    [string]$Version = "1.0.0"
)

Write-Host "Packaging SeleneNative v$Version..."

# 创建MSIX包
dotnet msbuild src/SeleneNative/SeleneNative.csproj /t:Publish /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageDir="artifacts\"

Write-Host "Package created in artifacts\ folder"
```

## 实施计划

### 阶段1：项目搭建 (1-2天)

1. 创建项目结构
2. 配置WinUI 3项目
3. 设置依赖注入
4. 创建基础模型

### 阶段2：核心功能 (3-5天)

1. 实现登录模块
2. 实现搜索模块
3. 实现详情模块
4. 实现播放模块

### 阶段3：扩展功能 (3-5天)

1. 实现首页模块
2. 实现分类模块
3. 实现直播模块
4. 实现用户数据模块

### 阶段4：完善功能 (2-3天)

1. 实现设置模块
2. 添加错误处理
3. 优化性能
4. 添加测试

### 阶段5：打包部署 (1-2天)

1. 创建构建脚本
2. 创建安装包
3. 测试部署

## 总结

本设计方案基于WinUI 3和C#构建原生Windows客户端，采用MVVM架构模式，提供与macOS版本完全一致的功能。通过合理的架构设计和模块化实现，可以确保代码的可维护性和可扩展性。

设计已准备好，请审阅。如有需要修改的地方，请告知。</think>设计文档已创建完成。现在我需要根据设计文档开始实现native-windows版本。让我更新todo列表，然后开始实现。<tool_call>
<function=todowrite>
<parameter=todos>[{"content": "探索项目结构和文档，了解native-macos版本实现", "priority": "high", "status": "completed"}, {"content": "理解跨平台方案和native版本的区别", "priority": "high", "status": "completed"}, {"content": "确定native-windows版本的技术方案", "priority": "high", "status": "completed"}, {"content": "设计native-windows版本架构", "priority": "medium", "status": "completed"}, {"content": "实现native-windows版本", "priority": "medium", "status": "in_progress"}]