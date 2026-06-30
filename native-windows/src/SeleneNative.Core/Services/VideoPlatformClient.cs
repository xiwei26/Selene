using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IVideoPlatformClient
{
    Task<VideoPlatformPage> LoadBilibiliPopularAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<VideoPlatformPage> SearchBilibiliAsync(string query, CancellationToken cancellationToken = default);
    Task<VideoPlatformPage> LoadYouTubePopularAsync(string regionCode = "US", string? pageToken = null, CancellationToken cancellationToken = default);
    Task<VideoPlatformPage> SearchYouTubeAsync(string query, string contentType = "all", string order = "relevance", int maxResults = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YouTubeRegion>> LoadYouTubeRegionsAsync(CancellationToken cancellationToken = default);
}

public sealed class VideoPlatformClient(string baseUrl, string cookie = "", HttpClient? httpClient = null)
    : LunaFeatureClientBase(baseUrl, cookie, httpClient), IVideoPlatformClient
{
    public async Task<VideoPlatformPage> LoadBilibiliPopularAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<VideoPlatformPage>(
            "/api/bilibili/popular",
            [
                new KeyValuePair<string, string?>("page", page.ToString()),
                new KeyValuePair<string, string?>("size", pageSize.ToString())
            ],
            cancellationToken).ConfigureAwait(false) ?? new VideoPlatformPage();
    }

    public async Task<VideoPlatformPage> SearchBilibiliAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<VideoPlatformPage>(
            "/api/bilibili/search",
            [new KeyValuePair<string, string?>("query", query)],
            cancellationToken).ConfigureAwait(false) ?? new VideoPlatformPage();
    }

    public async Task<VideoPlatformPage> LoadYouTubePopularAsync(
        string regionCode = "US",
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<VideoPlatformPage>(
            "/api/youtube/popular",
            [
                new KeyValuePair<string, string?>("regionCode", regionCode),
                new KeyValuePair<string, string?>("pageToken", pageToken)
            ],
            cancellationToken).ConfigureAwait(false) ?? new VideoPlatformPage();
    }

    public async Task<VideoPlatformPage> SearchYouTubeAsync(
        string query,
        string contentType = "all",
        string order = "relevance",
        int maxResults = 25,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<VideoPlatformPage>(
            "/api/youtube/search",
            [
                new KeyValuePair<string, string?>("query", query),
                new KeyValuePair<string, string?>("contentType", contentType),
                new KeyValuePair<string, string?>("order", order),
                new KeyValuePair<string, string?>("maxResults", maxResults.ToString())
            ],
            cancellationToken).ConfigureAwait(false) ?? new VideoPlatformPage();
    }

    public async Task<IReadOnlyList<YouTubeRegion>> LoadYouTubeRegionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<List<YouTubeRegion>>(
            "/api/youtube/regions",
            [],
            cancellationToken).ConfigureAwait(false) ?? [];
    }
}
