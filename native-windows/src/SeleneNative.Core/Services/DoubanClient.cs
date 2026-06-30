using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IDoubanClient
{
    Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default);
    Task<DoubanMovie?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default);
}

public sealed class DoubanClient : IDoubanClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string HotCategory = "\u70ed\u95e8";
    private const string AllType = "\u5168\u90e8";

    private readonly HttpClient _httpClient;
    private Uri? _backendBaseUri;
    private string _backendCookie = string.Empty;

    public DoubanClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://m.douban.com"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public void ConfigureBackend(string? baseUrl, string cookie = "")
    {
        _backendBaseUri = string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : new Uri(NormalizeBaseUrl(baseUrl));
        _backendCookie = cookie;
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("movie", HotCategory, AllType, cancellationToken);
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("tv", "tv", "tv", cancellationToken);
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("tv", "show", "show", cancellationToken);
    }

    private async Task<IReadOnlyList<DoubanMovie>> GetRecentHotAsync(
        string kind,
        string category,
        string type,
        CancellationToken cancellationToken)
    {
        if (_backendBaseUri is not null)
        {
            return await GetBackendCategoriesAsync(kind, category, type, cancellationToken)
                .ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/rexxar/api/v2/subject/recent_hot/{kind}?start=0&limit=20&category={Uri.EscapeDataString(category)}&type={Uri.EscapeDataString(type)}");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SeleneNative/1.0");
        request.Headers.Referrer = new Uri("https://movie.douban.com/");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<DoubanResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return data?.Items ?? [];
    }

    private async Task<IReadOnlyList<DoubanMovie>> GetBackendCategoriesAsync(
        string kind,
        string category,
        string type,
        CancellationToken cancellationToken)
    {
        using var request = CreateBackendRequest(
            "/api/douban/categories",
            [
                new KeyValuePair<string, string>("kind", kind),
                new KeyValuePair<string, string>("category", category),
                new KeyValuePair<string, string>("type", type),
                new KeyValuePair<string, string>("limit", "20"),
                new KeyValuePair<string, string>("start", "0")
            ]);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<DoubanCategoryResult>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return data?.List ?? [];
    }

    public async Task<DoubanMovie?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doubanId))
        {
            return null;
        }

        if (_backendBaseUri is not null)
        {
            return await GetBackendDetailAsync(doubanId, cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/rexxar/api/v2/subject/{Uri.EscapeDataString(doubanId)}");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SeleneNative/1.0");
        request.Headers.Referrer = new Uri("https://movie.douban.com/");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<DoubanMovie>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<DoubanMovie?> GetBackendDetailAsync(
        string doubanId,
        CancellationToken cancellationToken)
    {
        using var request = CreateBackendRequest(
            "/api/douban/details",
            [new KeyValuePair<string, string>("id", doubanId)]);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<DoubanDetailResult>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return data?.Data;
    }

    private HttpRequestMessage CreateBackendRequest(
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query)
    {
        var builder = new UriBuilder(new Uri(_backendBaseUri!, path.TrimStart('/')));
        builder.Query = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(_backendCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _backendCookie);
        }

        return request;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private sealed class DoubanCategoryResult
    {
        [JsonPropertyName("list")]
        public List<DoubanMovie> List { get; set; } = [];
    }

    private sealed class DoubanDetailResult
    {
        [JsonPropertyName("data")]
        public DoubanMovie? Data { get; set; }
    }
}
