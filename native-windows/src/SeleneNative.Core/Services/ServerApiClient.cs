using System.Net.Http.Json;
using System.Text.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public sealed class ServerApiClient : IContentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string _cookie;

    public string BaseUrl => _baseUri.ToString().TrimEnd('/');

    public ServerApiClient(string baseUrl, string cookie = "", HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Server URL is required.", nameof(baseUrl));
        }

        _baseUri = new Uri(NormalizeBaseUrl(baseUrl));
        _cookie = cookie;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<LoginSession> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/login", includeCookie: false);
        request.Content = JsonContent.Create(new { username, password }, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "登录失败", cancellationToken).ConfigureAwait(false);

        return new LoginSession
        {
            ServerUrl = _baseUri.ToString().TrimEnd('/'),
            Username = username,
            Cookie = ExtractCookie(response)
        };
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/search",
            [new KeyValuePair<string, string>("q", query)],
            null,
            "搜索失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<SearchResult>(results);
        }

        return data.ValueKind == JsonValueKind.Array ? DeserializeArray<SearchResult>(data) : [];
    }

    public async Task<SearchResult?> DetailAsync(
        string source,
        string id,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/detail",
            [
                new KeyValuePair<string, string>("source", source),
                new KeyValuePair<string, string>("id", id)
            ],
            null,
            "获取详情失败",
            cancellationToken).ConfigureAwait(false);

        return data.ValueKind == JsonValueKind.Object
            ? data.Deserialize<SearchResult>(JsonOptions)
            : null;
    }

    public async Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/search/resources",
            [],
            null,
            "获取搜索源失败",
            cancellationToken).ConfigureAwait(false);
        return data.ValueKind == JsonValueKind.Array ? DeserializeArray<SearchResource>(data) : [];
    }

    public async Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/favorites",
            [],
            null,
            "获取收藏失败",
            cancellationToken).ConfigureAwait(false);

        return ReadKeyedMap(data, "favorites")
            .Select(pair => FavoriteItem.FromJson(pair.Key, pair.Value))
            .OrderByDescending(item => item.SaveTime)
            .ToList();
    }

    public Task AddFavoriteAsync(
        string source,
        string id,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Post,
            "/api/favorites",
            [],
            new Dictionary<string, object>
            {
                ["key"] = $"{source}+{id}",
                ["favorite"] = data
            },
            "收藏失败",
            cancellationToken);
    }

    public Task RemoveFavoriteAsync(string source, string id, CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Delete,
            "/api/favorites",
            [new KeyValuePair<string, string>("key", $"{source}+{id}")],
            null,
            "取消收藏失败",
            cancellationToken);
    }

    public async Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/playrecords",
            [],
            null,
            "获取历史记录失败",
            cancellationToken).ConfigureAwait(false);

        return ReadKeyedMap(data, "records")
            .Select(pair => DeserializePlayRecord(pair.Key, pair.Value))
            .Where(record => record is not null)
            .Cast<PlayRecord>()
            .OrderByDescending(record => record.SaveTime)
            .ToList();
    }

    public Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Post,
            "/api/playrecords",
            [],
            new Dictionary<string, object>
            {
                ["key"] = $"{record.Source}+{record.ItemId}",
                ["record"] = record.ToJson()
            },
            "保存历史记录失败",
            cancellationToken);
    }

    public Task DeletePlayRecordAsync(string source, string id, CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Delete,
            "/api/playrecords",
            [new KeyValuePair<string, string>("key", $"{source}+{id}")],
            null,
            "删除历史记录失败",
            cancellationToken);
    }

    public Task ClearPlayRecordsAsync(CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Delete,
            "/api/playrecords",
            [],
            null,
            "清空历史记录失败",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSearchHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/searchhistory",
            [],
            null,
            "获取搜索历史失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind == JsonValueKind.Array)
        {
            return data.Deserialize<List<string>>(JsonOptions) ?? [];
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "history", "keywords" })
            {
                if (data.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array)
                {
                    return property.Deserialize<List<string>>(JsonOptions) ?? [];
                }
            }
        }

        return [];
    }

    public Task AddSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Post,
            "/api/searchhistory",
            [],
            new Dictionary<string, object> { ["keyword"] = query },
            "保存搜索历史失败",
            cancellationToken);
    }

    public Task DeleteSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Delete,
            "/api/searchhistory",
            [new KeyValuePair<string, string>("keyword", query)],
            null,
            "删除搜索历史失败",
            cancellationToken);
    }

    public Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default)
    {
        return SendWithoutResultAsync(
            HttpMethod.Delete,
            "/api/searchhistory",
            [],
            null,
            "清空搜索历史失败",
            cancellationToken);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> SearchSuggestionsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/search/suggestions",
            [new KeyValuePair<string, string>("q", query)],
            null,
            "获取搜索建议失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<SearchSuggestion>(data);
        }

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("suggestions", out var suggestions) &&
            suggestions.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<SearchSuggestion>(suggestions);
        }

        return [];
    }

    public async Task<IReadOnlyList<LiveSource>> GetLiveSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/live/sources",
            [],
            null,
            "获取直播源失败",
            cancellationToken).ConfigureAwait(false);
        return ReadArrayOrWrappedArray<LiveSource>(data, "data", "sources");
    }

    public async Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/live/channels",
            [new KeyValuePair<string, string>("source", sourceKey)],
            null,
            "获取直播频道失败",
            cancellationToken).ConfigureAwait(false);
        return ReadArrayOrWrappedArray<LiveChannel>(data, "data", "channels");
    }

    public async Task<EpgData?> GetLiveEpgAsync(
        string tvgId,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/live/epg",
            [
                new KeyValuePair<string, string>("tvgId", tvgId),
                new KeyValuePair<string, string>("source", sourceKey)
            ],
            null,
            "获取 EPG 失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("data", out var wrapped) &&
            wrapped.ValueKind == JsonValueKind.Object)
        {
            return wrapped.Deserialize<EpgData>(JsonOptions);
        }

        return data.ValueKind == JsonValueKind.Object ? data.Deserialize<EpgData>(JsonOptions) : null;
    }

    public async Task<IReadOnlyList<SearchResult>> GetRecommendedShortDramasAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/shortdrama/recommend",
            [new KeyValuePair<string, string>("size", "30")],
            null,
            "获取短剧推荐失败",
            cancellationToken).ConfigureAwait(false);

        return ReadShortDramaList(data);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchShortDramasAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/shortdrama/search",
            [new KeyValuePair<string, string>("q", query)],
            null,
            "搜索短剧失败",
            cancellationToken).ConfigureAwait(false);

        return ReadShortDramaList(data);
    }

    public async Task<SearchResult?> GetShortDramaDetailAsync(
        string id,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("id", id),
            new("episode", "1")
        };
        if (!string.IsNullOrWhiteSpace(name))
        {
            query.Add(new("name", name));
        }

        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/shortdrama/detail",
            query,
            null,
            "获取短剧详情失败",
            cancellationToken).ConfigureAwait(false);

        return data.ValueKind == JsonValueKind.Object
            ? data.Deserialize<SearchResult>(JsonOptions)
            : null;
    }

    public async Task<IReadOnlyList<MediaPlatformItem>> GetBilibiliPopularAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/bilibili/popular",
            [],
            null,
            "获取 Bilibili 热门失败",
            cancellationToken).ConfigureAwait(false);

        return ReadPlatformItems(data, "bilibili");
    }

    public async Task<IReadOnlyList<MediaPlatformItem>> SearchBilibiliAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/bilibili/search",
            [new KeyValuePair<string, string>("q", query)],
            null,
            "搜索 Bilibili 失败",
            cancellationToken).ConfigureAwait(false);

        return ReadPlatformItems(data, "bilibili");
    }

    public async Task<IReadOnlyList<MediaPlatformItem>> GetYouTubePopularAsync(
        string regionCode = "US",
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/youtube/popular",
            [new KeyValuePair<string, string>("regionCode", regionCode)],
            null,
            "获取 YouTube 热门失败",
            cancellationToken).ConfigureAwait(false);

        return ReadPlatformItems(data, "youtube");
    }

    public async Task<IReadOnlyList<MediaPlatformItem>> SearchYouTubeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/youtube/search",
            [new KeyValuePair<string, string>("q", query)],
            null,
            "搜索 YouTube 失败",
            cancellationToken).ConfigureAwait(false);

        return ReadPlatformItems(data, "youtube");
    }

    public async Task<TmdbBackdrop?> GetTmdbBackdropAsync(
        string title,
        string? year = null,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<KeyValuePair<string, string>> { new("title", title) };
        if (!string.IsNullOrWhiteSpace(year)) query.Add(new("year", year));
        if (!string.IsNullOrWhiteSpace(type)) query.Add(new("stype", type));

        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/tmdb/backdrop",
            query,
            null,
            "获取 TMDB 视觉信息失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("data", out var wrapped) &&
            wrapped.ValueKind == JsonValueKind.Object)
        {
            return wrapped.Deserialize<TmdbBackdrop>(JsonOptions);
        }

        return data.ValueKind == JsonValueKind.Object
            ? data.Deserialize<TmdbBackdrop>(JsonOptions)
            : null;
    }

    public async Task<DoubanQuickInfo?> GetDoubanQuickInfoAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/douban/quick-info",
            [new KeyValuePair<string, string>("q", title)],
            null,
            "获取豆瓣快速信息失败",
            cancellationToken).ConfigureAwait(false);

        var root = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var wrapped)
            ? wrapped
            : data;
        return root.ValueKind == JsonValueKind.Object
            ? new DoubanQuickInfo
            {
                Title = ReadString(root, "title", "name"),
                Year = ReadString(root, "year"),
                Rating = ReadString(root, "rating", "rate", "score"),
                Summary = ReadString(root, "summary", "desc", "description"),
                Genres = ReadStringArray(root, "genres", "genre"),
                Directors = ReadStringArray(root, "directors", "director"),
                Cast = ReadStringArray(root, "cast", "actors")
            }
            : null;
    }

    public async Task<IReadOnlyList<DoubanComment>> GetDoubanCommentsAsync(
        string doubanId,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/douban/comments",
            [new KeyValuePair<string, string>("id", doubanId)],
            null,
            "获取豆瓣短评失败",
            cancellationToken).ConfigureAwait(false);

        var items = ReadWrappedArray(data, "comments", "data", "list");
        return items.Select(item => new DoubanComment
        {
            Author = ReadString(item, "author", "name"),
            Content = ReadString(item, "content", "comment", "text"),
            Rating = ReadString(item, "rating", "score")
        }).Where(item => !string.IsNullOrWhiteSpace(item.Content)).ToList();
    }

    public async Task<IReadOnlyList<DoubanRecommendation>> GetDoubanRecommendationsAsync(
        string doubanId,
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/douban/recommends",
            [new KeyValuePair<string, string>("id", doubanId)],
            null,
            "获取豆瓣推荐失败",
            cancellationToken).ConfigureAwait(false);

        var items = ReadWrappedArray(data, "recommends", "recommendations", "data", "list");
        return items.Select(item => new DoubanRecommendation
        {
            Id = ReadString(item, "id", "douban_id"),
            Title = ReadString(item, "title", "name"),
            Cover = ReadString(item, "cover", "poster", "pic"),
            Rating = ReadString(item, "rating", "rate", "score")
        }).Where(item => !string.IsNullOrWhiteSpace(item.Title)).ToList();
    }

    public async Task<AdminConfig?> GetAdminConfigAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await SendForJsonAsync(
            HttpMethod.Get,
            "/api/admin/config",
            [],
            null,
            "获取管理后台配置失败",
            cancellationToken).ConfigureAwait(false);

        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var config = new AdminConfig();

        // Parse Role
        if (data.TryGetProperty("Role", out var role) && role.ValueKind == JsonValueKind.String)
        {
            config.Role = role.GetString();
        }

        // The actual config may be wrapped in a "Config" property
        var configRoot = data.TryGetProperty("Config", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object
            ? wrapped : data;

        // Parse YouTubeConfig
        if (configRoot.TryGetProperty("YouTubeConfig", out var yt) && yt.ValueKind == JsonValueKind.Object)
        {
            config.YouTubeConfig = new YouTubeAdminConfig
            {
                Enabled = ReadBool(yt, "enabled"),
                ApiKey = ReadString(yt, "apiKey"),
                EnableDemo = ReadBool(yt, "enableDemo", defaultValue: true),
                MaxResults = ReadInt(yt, "maxResults", defaultValue: 25),
                EnabledRegions = ReadStringListOrEmpty(yt, "enabledRegions", YouTubeAdminConfig.DefaultRegions),
                EnabledCategories = ReadStringListOrEmpty(yt, "enabledCategories", YouTubeAdminConfig.DefaultCategories),
            };
        }

        // Parse BilibiliConfig
        if (configRoot.TryGetProperty("BilibiliConfig", out var bili) && bili.ValueKind == JsonValueKind.Object)
        {
            config.BilibiliConfig = new BilibiliAdminConfig
            {
                Enabled = ReadBool(bili, "enabled"),
                LoginStatus = ReadString(bili, "loginStatus"),
            };

            if (bili.TryGetProperty("userInfo", out var ui) && ui.ValueKind == JsonValueKind.Object)
            {
                config.BilibiliConfig.UserInfo = new BilibiliAdminUserInfo
                {
                    Mid = ReadLong(ui, "mid"),
                    Username = ReadString(ui, "username"),
                    Face = ReadString(ui, "face"),
                    IsVip = ReadBool(ui, "isVip"),
                };
            }
        }

        // Parse ShortDramaConfig
        if (configRoot.TryGetProperty("ShortDramaConfig", out var sd) && sd.ValueKind == JsonValueKind.Object)
        {
            config.ShortDramaConfig = new ShortDramaAdminConfig
            {
                PrimaryApiUrl = ReadString(sd, "primaryApiUrl"),
                AlternativeApiUrl = ReadString(sd, "alternativeApiUrl"),
                EnableAlternative = ReadBool(sd, "enableAlternative"),
            };
        }

        // Parse SiteConfig
        if (configRoot.TryGetProperty("SiteConfig", out var site) && site.ValueKind == JsonValueKind.Object)
        {
            config.SiteConfig = new AdminSiteConfig
            {
                SiteName = ReadString(site, "SiteName"),
                Announcement = ReadString(site, "Announcement"),
            };
        }

        return config;
    }

    public async Task SaveYouTubeConfigAsync(
        YouTubeAdminConfig youTubeConfig,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["enabled"] = youTubeConfig.Enabled,
            ["apiKey"] = youTubeConfig.ApiKey ?? string.Empty,
            ["enableDemo"] = youTubeConfig.EnableDemo,
            ["maxResults"] = youTubeConfig.MaxResults,
            ["enabledRegions"] = youTubeConfig.EnabledRegions ?? [],
            ["enabledCategories"] = youTubeConfig.EnabledCategories ?? [],
        };

        await SendForJsonAsync(
            HttpMethod.Post,
            "/api/admin/youtube",
            [],
            body,
            "保存 YouTube 配置失败",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveBilibiliConfigAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["enabled"] = enabled,
        };

        await SendForJsonAsync(
            HttpMethod.Post,
            "/api/admin/bilibili",
            [],
            body,
            "保存 Bilibili 配置失败",
            cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>>? query = null,
        bool includeCookie = true)
    {
        var builder = new UriBuilder(new Uri(_baseUri, path.TrimStart('/')));
        if (query is { Count: > 0 })
        {
            builder.Query = string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        var request = new HttpRequestMessage(method, builder.Uri);
        request.Headers.Accept.ParseAdd("application/json");
        if (includeCookie && !string.IsNullOrWhiteSpace(_cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookie);
        }

        return request;
    }

    private async Task<JsonElement> SendForJsonAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query,
        Dictionary<string, object>? body,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, query);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, errorMessage, cancellationToken).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    private async Task SendWithoutResultAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query,
        Dictionary<string, object>? body,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, query);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, errorMessage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string defaultMessage,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Detect feature-disabled responses from the LunaTV backend
        if (detail.Contains("功能未启用") || detail.Contains("未启用"))
        {
            throw ApiException.FeatureDisabledError(
                string.IsNullOrWhiteSpace(detail) ? defaultMessage : detail,
                (int)response.StatusCode);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new ApiException("登录已过期，请重新登录", (int)response.StatusCode);
        }

        throw new ApiException(
            string.IsNullOrWhiteSpace(detail) ? defaultMessage : $"{defaultMessage}: {detail}",
            (int)response.StatusCode);
    }

    private static string ExtractCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return string.Empty;
        }

        return string.Join("; ", values
            .Select(value => value.Split(';', 2)[0].Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static List<T> DeserializeArray<T>(JsonElement array)
    {
        return array.EnumerateArray()
            .Select(item => item.Deserialize<T>(JsonOptions))
            .Where(item => item is not null)
            .Cast<T>()
            .ToList();
    }

    private static List<T> ReadArrayOrWrappedArray<T>(JsonElement element, params string[] wrapperNames)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<T>(element);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in wrapperNames)
        {
            if (element.TryGetProperty(name, out var wrapped) && wrapped.ValueKind == JsonValueKind.Array)
            {
                return DeserializeArray<T>(wrapped);
            }
        }

        return [];
    }

    private static List<SearchResult> ReadShortDramaList(JsonElement element)
    {
        return ReadWrappedArray(element, "data", "list", "items", "results")
            .Select(item => new SearchResult
            {
                Id = ReadString(item, "id"),
                Title = ReadString(item, "name", "title"),
                Poster = ReadString(item, "cover", "poster", "pic"),
                Source = "shortdrama",
                SourceName = "短剧",
                Description = ReadString(item, "description", "desc"),
                Year = ReadString(item, "year"),
                TypeName = "短剧"
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title))
            .ToList();
    }

    private static List<MediaPlatformItem> ReadPlatformItems(JsonElement element, string source)
    {
        var items = new List<JsonElement>();
        items.AddRange(ReadWrappedArray(element, "videos", "items", "results", "data"));
        items.AddRange(ReadWrappedArray(element, "bangumi"));

        return items.Select(item =>
        {
            var id = source == "youtube"
                ? ReadYouTubeId(item)
                : ReadString(item, "bvid", "aid", "season_id", "media_id", "id");
            var snippet = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("snippet", out var s)
                ? s
                : item;
            var thumbnail = ReadThumbnail(snippet);
            return new MediaPlatformItem
            {
                Id = id,
                Title = FirstNonEmpty(ReadString(item, "title", "name"), ReadString(snippet, "title")),
                Cover = FirstNonEmpty(ReadString(item, "pic", "cover"), thumbnail),
                Author = FirstNonEmpty(ReadString(item, "author", "uname"), ReadString(snippet, "channelTitle")),
                Description = FirstNonEmpty(ReadString(item, "description", "desc"), ReadString(snippet, "description")),
                Duration = ReadString(item, "duration"),
                Source = source,
                Url = source == "youtube" && !string.IsNullOrWhiteSpace(id)
                    ? $"https://www.youtube.com/watch?v={id}"
                    : source == "bilibili" && !string.IsNullOrWhiteSpace(id)
                        ? $"https://www.bilibili.com/video/{id}"
                        : string.Empty
            };
        })
        .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title))
        .ToList();
    }

    private static List<JsonElement> ReadWrappedArray(JsonElement element, params string[] wrapperNames)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().ToList();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in wrapperNames)
        {
            if (element.TryGetProperty(name, out var wrapped))
            {
                if (wrapped.ValueKind == JsonValueKind.Array)
                {
                    return wrapped.EnumerateArray().ToList();
                }

                if (wrapped.ValueKind == JsonValueKind.Object)
                {
                    var nested = ReadWrappedArray(wrapped, wrapperNames);
                    if (nested.Count > 0)
                    {
                        return nested;
                    }
                }
            }
        }

        return [];
    }

    private static string ReadString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var property))
            {
                var value = property.ReadString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return property.EnumerateArray()
                .Select(item => item.ReadString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList();
        }

        return [];
    }

    private static string ReadYouTubeId(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (item.TryGetProperty("id", out var id))
        {
            if (id.ValueKind == JsonValueKind.Object)
            {
                return ReadString(id, "videoId", "channelId", "playlistId");
            }

            return id.ReadString();
        }

        return string.Empty;
    }

    private static string ReadThumbnail(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("thumbnails", out var thumbnails) ||
            thumbnails.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var name in new[] { "medium", "high", "default" })
        {
            if (thumbnails.TryGetProperty(name, out var thumb))
            {
                var url = ReadString(thumb, "url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> ReadKeyedMap(
        JsonElement element,
        string preferredKey)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (TryGetObject(element, preferredKey, out var nested))
        {
            return EnumerateObjectMap(nested);
        }

        if (TryGetObject(element, "data", out var data))
        {
            if (TryGetObject(data, preferredKey, out var nestedData))
            {
                return EnumerateObjectMap(nestedData);
            }

            return EnumerateObjectMap(data);
        }

        return EnumerateObjectMap(element);
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> EnumerateObjectMap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return element.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Object)
            .Select(property => new KeyValuePair<string, JsonElement>(property.Name, property.Value));
    }

    private static PlayRecord? DeserializePlayRecord(string key, JsonElement element)
    {
        try
        {
            using var document = JsonDocument.Parse(element.GetRawText());
            var mutable = document.RootElement.Deserialize<Dictionary<string, object?>>(JsonOptions) ?? [];
            mutable["id"] = key;
            mutable["source"] = ModelValueReaders.SplitKey(key).Source;
            return JsonSerializer.Deserialize<PlayRecord>(JsonSerializer.Serialize(mutable), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
        {
            return prop.GetBoolean();
        }

        return defaultValue;
    }

    private static int ReadInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }

        return defaultValue;
    }

    private static long ReadLong(JsonElement element, string propertyName, long defaultValue = 0)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt64();
        }

        return defaultValue;
    }

    private static List<string> ReadStringList(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Array)
        {
            return prop.EnumerateArray()
                .Select(item => item.ReadString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return [];
    }

    private static List<string> ReadStringListOrEmpty(JsonElement element, string propertyName, string[] defaults)
    {
        var result = ReadStringList(element, propertyName);
        return result.Count > 0 ? result : [.. defaults];
    }
}
