using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IShortDramaClient
{
    Task<IReadOnlyList<ShortDramaCategory>> LoadShortDramaCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ShortDramaListResult> LoadShortDramaRecommendAsync(string? category = null, int size = 24, CancellationToken cancellationToken = default);
    Task<ShortDramaListResult> LoadShortDramaListAsync(string categoryId, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default);
    Task<ShortDramaListResult> SearchAsync(string query, int page = 1, int pageSize = 24, CancellationToken cancellationToken = default);
    Task<ShortDramaDetail?> LoadDetailAsync(string id, string? name = null, CancellationToken cancellationToken = default);
    Task<ShortDramaParseResult?> ParseAsync(string id, int episode, string? name = null, CancellationToken cancellationToken = default);
}

public sealed class ShortDramaClient(string baseUrl, string cookie = "", HttpClient? httpClient = null)
    : LunaFeatureClientBase(baseUrl, cookie, httpClient), IShortDramaClient
{
    public async Task<IReadOnlyList<ShortDramaCategory>> LoadShortDramaCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<List<ShortDramaCategory>>(
            "/api/shortdrama/categories",
            [],
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<ShortDramaListResult> LoadShortDramaRecommendAsync(
        string? category = null,
        int size = 24,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<ShortDramaListResult>(
            "/api/shortdrama/recommend",
            [
                new KeyValuePair<string, string?>("category", category),
                new KeyValuePair<string, string?>("size", size.ToString())
            ],
            cancellationToken).ConfigureAwait(false) ?? new ShortDramaListResult();
    }

    public async Task<ShortDramaListResult> LoadShortDramaListAsync(
        string categoryId,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<ShortDramaListResult>(
            "/api/shortdrama/list",
            [
                new KeyValuePair<string, string?>("categoryId", categoryId),
                new KeyValuePair<string, string?>("page", page.ToString()),
                new KeyValuePair<string, string?>("size", pageSize.ToString())
            ],
            cancellationToken).ConfigureAwait(false) ?? new ShortDramaListResult();
    }

    public async Task<ShortDramaListResult> SearchAsync(
        string query,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<ShortDramaListResult>(
            "/api/shortdrama/search",
            [
                new KeyValuePair<string, string?>("query", query),
                new KeyValuePair<string, string?>("page", page.ToString()),
                new KeyValuePair<string, string?>("size", pageSize.ToString())
            ],
            cancellationToken).ConfigureAwait(false) ?? new ShortDramaListResult();
    }

    public Task<ShortDramaDetail?> LoadDetailAsync(
        string id,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<ShortDramaDetail>(
            "/api/shortdrama/detail",
            [
                new KeyValuePair<string, string?>("id", id),
                new KeyValuePair<string, string?>("name", name)
            ],
            cancellationToken);
    }

    public Task<ShortDramaParseResult?> ParseAsync(
        string id,
        int episode,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<ShortDramaParseResult>(
            "/api/shortdrama/parse",
            [
                new KeyValuePair<string, string?>("id", id),
                new KeyValuePair<string, string?>("episode", episode.ToString()),
                new KeyValuePair<string, string?>("name", name)
            ],
            cancellationToken);
    }
}
