using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Tests.Player;

/// <summary>Content provider double whose search, detail and record-save calls are scripted per test.</summary>
internal sealed class StubContentProvider : IContentProvider
{
    public Func<string, Task<IReadOnlyList<SearchResult>>> OnSearch { get; set; } =
        _ => Task.FromResult<IReadOnlyList<SearchResult>>([]);

    public Func<string, string, Task<SearchResult?>> OnDetail { get; set; } =
        (_, _) => Task.FromResult<SearchResult?>(null);

    public Func<PlayRecord, Task> OnSavePlayRecord { get; set; } = _ => Task.CompletedTask;

    public List<string> SearchQueries { get; } = [];
    public List<(string Source, string Id)> DetailRequests { get; } = [];
    public List<PlayRecord> SavedRecords { get; } = [];

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        SearchQueries.Add(query);
        return OnSearch(query);
    }

    public Task<SearchResult?> DetailAsync(string source, string id, CancellationToken cancellationToken = default)
    {
        DetailRequests.Add((source, id));
        return OnDetail(source, id);
    }

    public async Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        await OnSavePlayRecord(record);
        SavedRecords.Add(record);
    }

    public Task<LoginSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SearchResource>>([]);

    public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FavoriteItem>>([]);

    public Task AddFavoriteAsync(string source, string id, Dictionary<string, object> data, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveFavoriteAsync(string source, string id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PlayRecord>>([]);

    public Task DeletePlayRecordAsync(string source, string id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ClearPlayRecordsAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<string>> GetSearchHistoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task AddSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteSearchHistoryAsync(string query, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<SearchSuggestion>> SearchSuggestionsAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);

    public Task<IReadOnlyList<LiveSource>> GetLiveSourcesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LiveSource>>([]);

    public Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(string sourceKey, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LiveChannel>>([]);

    public Task<EpgData?> GetLiveEpgAsync(string tvgId, string sourceKey, CancellationToken cancellationToken = default)
        => Task.FromResult<EpgData?>(null);
}

internal static class PlayerTestData
{
    /// <summary>A result with numbered episode URLs and, optionally, titles "第1集"…</summary>
    public static SearchResult Result(
        string title = "测试剧",
        string source = "src-a",
        string id = "1",
        int episodes = 5,
        bool titled = false,
        string year = "",
        string? typeName = null,
        int? doubanId = null)
    {
        return new SearchResult
        {
            Id = id,
            Title = title,
            Source = source,
            SourceName = source,
            Year = year,
            TypeName = typeName,
            DoubanId = doubanId,
            Episodes = Enumerable.Range(1, episodes).Select(n => $"https://example.com/{source}/{id}/ep{n}.m3u8").ToList(),
            EpisodeTitles = titled ? Enumerable.Range(1, episodes).Select(n => $"第{n}集").ToList() : [],
        };
    }
}
