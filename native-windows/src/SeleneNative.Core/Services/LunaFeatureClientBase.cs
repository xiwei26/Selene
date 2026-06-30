using System.Text.Json;

namespace SeleneNative.Core.Services;

public abstract class LunaFeatureClientBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string _cookie;

    protected LunaFeatureClientBase(string baseUrl, string cookie = "", HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Server URL is required.", nameof(baseUrl));
        }

        _baseUri = new Uri(NormalizeBaseUrl(baseUrl));
        _cookie = cookie;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    protected HttpRequestMessage CreateGetRequest(
        string path,
        IReadOnlyList<KeyValuePair<string, string?>> query)
    {
        var builder = new UriBuilder(new Uri(_baseUri, path.TrimStart('/')));
        var queryText = string.Join("&", query
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        builder.Query = queryText;

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(_cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookie);
        }

        return request;
    }

    protected async Task<T?> GetJsonAsync<T>(
        string path,
        IReadOnlyList<KeyValuePair<string, string?>> query,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateGetRequest(path, query);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var payload = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
            ? data
            : root;

        return payload.Deserialize<T>(JsonOptions);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }
}
