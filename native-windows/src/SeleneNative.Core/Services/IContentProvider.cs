using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IContentProvider
{
    Task<LoginSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<SearchResult?> DetailAsync(string source, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default);
    Task AddFavoriteAsync(string source, string id, Dictionary<string, object> data, CancellationToken cancellationToken = default);
    Task RemoveFavoriteAsync(string source, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(CancellationToken cancellationToken = default);
    Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default);
    Task DeletePlayRecordAsync(string source, string id, CancellationToken cancellationToken = default);
    Task ClearPlayRecordsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetSearchHistoryAsync(CancellationToken cancellationToken = default);
    Task AddSearchHistoryAsync(string query, CancellationToken cancellationToken = default);
    Task DeleteSearchHistoryAsync(string query, CancellationToken cancellationToken = default);
    Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchSuggestion>> SearchSuggestionsAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveSource>> GetLiveSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task<EpgData?> GetLiveEpgAsync(string tvgId, string sourceKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> GetRecommendedShortDramasAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    Task<IReadOnlyList<SearchResult>> SearchShortDramasAsync(string query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    Task<SearchResult?> GetShortDramaDetailAsync(string id, string? name = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SearchResult?>(null);
    }

    Task<IReadOnlyList<MediaPlatformItem>> GetBilibiliPopularAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MediaPlatformItem>>([]);
    }

    Task<IReadOnlyList<MediaPlatformItem>> SearchBilibiliAsync(string query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MediaPlatformItem>>([]);
    }

    Task<IReadOnlyList<MediaPlatformItem>> GetYouTubePopularAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MediaPlatformItem>>([]);
    }

    Task<IReadOnlyList<MediaPlatformItem>> SearchYouTubeAsync(string query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MediaPlatformItem>>([]);
    }

    Task<TmdbBackdrop?> GetTmdbBackdropAsync(string title, string? year = null, string? type = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TmdbBackdrop?>(null);
    }

    Task<DoubanQuickInfo?> GetDoubanQuickInfoAsync(string title, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DoubanQuickInfo?>(null);
    }

    Task<IReadOnlyList<DoubanComment>> GetDoubanCommentsAsync(string doubanId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DoubanComment>>([]);
    }

    Task<IReadOnlyList<DoubanRecommendation>> GetDoubanRecommendationsAsync(string doubanId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DoubanRecommendation>>([]);
    }
}
