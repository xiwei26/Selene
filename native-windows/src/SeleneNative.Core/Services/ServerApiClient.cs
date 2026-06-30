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
}
