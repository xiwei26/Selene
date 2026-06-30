using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IMetadataEnhancementClient
{
    Task<TmdbBackdropResult?> LoadBackdropAsync(string title, string? originalTitle, string? year, string? sourceType, CancellationToken cancellationToken = default);
    Task<TmdbActorResult?> LoadActorAsync(string actor, string type = "movie", int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanComment>> LoadDoubanCommentsAsync(string id, int start = 0, int limit = 10, string sort = "new_score", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanMovie>> LoadDoubanRecommendsAsync(string kind, int limit = 20, int start = 0, CancellationToken cancellationToken = default);
    Task<DoubanQuickInfo?> LoadDoubanQuickInfoAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanSuggestItem>> SuggestDoubanAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanCelebrityWork>> LoadCelebrityWorksAsync(string name, int limit = 20, string mode = "search", CancellationToken cancellationToken = default);
    Task<TrailerRefreshResult?> RefreshTrailerAsync(string id, bool force = false, CancellationToken cancellationToken = default);
}

public sealed class MetadataEnhancementClient(string baseUrl, string cookie = "", HttpClient? httpClient = null)
    : LunaFeatureClientBase(baseUrl, cookie, httpClient), IMetadataEnhancementClient
{
    public Task<TmdbBackdropResult?> LoadBackdropAsync(
        string title,
        string? originalTitle,
        string? year,
        string? sourceType,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<TmdbBackdropResult>(
            "/api/tmdb/backdrop",
            [
                new KeyValuePair<string, string?>("title", title),
                new KeyValuePair<string, string?>("originalTitle", originalTitle),
                new KeyValuePair<string, string?>("year", year),
                new KeyValuePair<string, string?>("sourceType", sourceType)
            ],
            cancellationToken);
    }

    public Task<TmdbActorResult?> LoadActorAsync(
        string actor,
        string type = "movie",
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<TmdbActorResult>(
            "/api/tmdb/actor",
            [
                new KeyValuePair<string, string?>("actor", actor),
                new KeyValuePair<string, string?>("type", type),
                new KeyValuePair<string, string?>("limit", limit.ToString())
            ],
            cancellationToken);
    }

    public async Task<IReadOnlyList<DoubanComment>> LoadDoubanCommentsAsync(
        string id,
        int start = 0,
        int limit = 10,
        string sort = "new_score",
        CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<DoubanCommentsResult>(
            "/api/douban/comments",
            [
                new KeyValuePair<string, string?>("id", id),
                new KeyValuePair<string, string?>("start", start.ToString()),
                new KeyValuePair<string, string?>("limit", limit.ToString()),
                new KeyValuePair<string, string?>("sort", sort)
            ],
            cancellationToken).ConfigureAwait(false);
        return result?.Comments ?? [];
    }

    public async Task<IReadOnlyList<DoubanMovie>> LoadDoubanRecommendsAsync(
        string kind,
        int limit = 20,
        int start = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<DoubanRecommendsResult>(
            "/api/douban/recommends",
            [
                new KeyValuePair<string, string?>("kind", kind),
                new KeyValuePair<string, string?>("limit", limit.ToString()),
                new KeyValuePair<string, string?>("start", start.ToString())
            ],
            cancellationToken).ConfigureAwait(false);
        return result?.Items ?? result?.List ?? [];
    }

    public Task<DoubanQuickInfo?> LoadDoubanQuickInfoAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<DoubanQuickInfo>(
            "/api/douban/quick-info",
            [new KeyValuePair<string, string?>("id", id)],
            cancellationToken);
    }

    public async Task<IReadOnlyList<DoubanSuggestItem>> SuggestDoubanAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<List<DoubanSuggestItem>>(
            "/api/douban/suggest",
            [new KeyValuePair<string, string?>("query", query)],
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<IReadOnlyList<DoubanCelebrityWork>> LoadCelebrityWorksAsync(
        string name,
        int limit = 20,
        string mode = "search",
        CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<List<DoubanCelebrityWork>>(
            "/api/douban/celebrity-works",
            [
                new KeyValuePair<string, string?>("name", name),
                new KeyValuePair<string, string?>("limit", limit.ToString()),
                new KeyValuePair<string, string?>("mode", mode)
            ],
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    public Task<TrailerRefreshResult?> RefreshTrailerAsync(
        string id,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync<TrailerRefreshResult>(
            "/api/douban/refresh-trailer",
            [
                new KeyValuePair<string, string?>("id", id),
                new KeyValuePair<string, string?>("force", force ? "true" : "false")
            ],
            cancellationToken);
    }

    private sealed class DoubanCommentsResult
    {
        public List<DoubanComment> Comments { get; set; } = [];
    }

    private sealed class DoubanRecommendsResult
    {
        public List<DoubanMovie> Items { get; set; } = [];
        public List<DoubanMovie>? List { get; set; }
    }
}
