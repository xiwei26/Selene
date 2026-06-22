using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class SearchViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private SearchProgress? _sseProgress;

    [ObservableProperty]
    private bool _isAggregating;

    [ObservableProperty]
    private string _blockedKeywordsText = string.Empty;

    [ObservableProperty]
    private string? _sourceFilter;

    [ObservableProperty]
    private string? _yearFilter;

    [ObservableProperty]
    private bool _sortNewestFirst = true;

    private readonly ContentFilterService _contentFilter = new();
    private SSESearchClient? _sseClient;
    private CancellationTokenSource? _sseCancellationTokenSource;
    private string _lastQuery = string.Empty;

    public ObservableCollection<SearchResult> Results { get; } = [];
    public ObservableCollection<string> History { get; } = [];
    public ObservableCollection<AggregatedSearchResult> AggregatedResults { get; } = [];
    public ObservableCollection<SearchSuggestion> Suggestions { get; } = [];

    public IReadOnlyList<SearchResult> FilteredResults
    {
        get
        {
            var list = Results.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SourceFilter))
            {
                list = list.Where(r => r.Source == SourceFilter);
            }
            if (!string.IsNullOrWhiteSpace(YearFilter))
            {
                list = list.Where(r => r.Year == YearFilter);
            }
            list = _contentFilter.Filter(list, BlockedKeywordsText);
            if (SortNewestFirst)
            {
                list = list.OrderByDescending(r => r.Year);
            }
            return list.ToList();
        }
    }

    public IReadOnlyList<AggregatedSearchResult> FilteredAggregatedResults
    {
        get
        {
            var list = AggregatedResults.AsEnumerable();
            var filtered = _contentFilter.Filter(
                list.SelectMany(a => a.OriginalResults),
                BlockedKeywordsText);
            var allowedKeys = new HashSet<string>(filtered.Select(AggregatedSearchResult.BuildKey), StringComparer.OrdinalIgnoreCase);
            list = list.Where(a => allowedKeys.Contains(a.Key));
            return list.ToList();
        }
    }

    public IReadOnlyList<string> AvailableSources =>
        Results.Select(r => r.SourceName).Distinct().OrderBy(s => s).ToList();

    public IReadOnlyList<string> AvailableYears =>
        Results.Select(r => r.Year).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderByDescending(y => y).ToList();

    public async Task LoadHistoryAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        History.Clear();
        if (provider is null) return;
        try
        {
            foreach (var item in await provider.GetSearchHistoryAsync(cancellationToken).ConfigureAwait(false))
            {
                History.Add(item);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task LoadSuggestionsAsync(IContentProvider? provider, string query, CancellationToken cancellationToken = default)
    {
        Suggestions.Clear();
        if (provider is null || string.IsNullOrWhiteSpace(query)) return;
        try
        {
            foreach (var item in await provider.SearchSuggestionsAsync(query, cancellationToken).ConfigureAwait(false))
            {
                Suggestions.Add(item);
            }
        }
        catch
        {
            // silently drop
        }
    }

    public async Task SearchAsync(IContentProvider? provider, string query, CancellationToken cancellationToken = default)
    {
        Results.Clear();
        AggregatedResults.Clear();
        ErrorMessage = null;
        if (provider is null) { ErrorMessage = "请先登录服务端再搜索"; return; }
        if (string.IsNullOrWhiteSpace(query)) return;
        _lastQuery = query.Trim();
        IsLoading = true;
        try
        {
            var results = await provider.SearchAsync(_lastQuery, cancellationToken).ConfigureAwait(false);
            foreach (var r in results) Results.Add(r);
            RebuildAggregates();
            await provider.AddSearchHistoryAsync(_lastQuery, cancellationToken).ConfigureAwait(false);
            await LoadHistoryAsync(provider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task SearchWithSSEAsync(IContentProvider? provider, string serverUrl, string cookie, string query, CancellationToken cancellationToken = default)
    {
        StopStreamingSearch();
        Results.Clear();
        AggregatedResults.Clear();
        SseProgress = null;
        ErrorMessage = null;
        if (provider is null) { ErrorMessage = "请先登录服务端再搜索"; return; }
        if (string.IsNullOrWhiteSpace(query)) return;
        _lastQuery = query.Trim();
        IsLoading = true;

        _sseCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sseClient = new SSESearchClient();
        var completedSources = 0;
        var totalSources = 0;
        var seenIds = new HashSet<string>();

        _sseClient.IncrementalResults += batch =>
        {
            foreach (var result in batch)
            {
                var id = $"{result.Source}|{result.Id}";
                if (seenIds.Add(id)) Results.Add(result);
            }
            RebuildAggregates();
        };
        _sseClient.Progress += progress =>
        {
            if (progress.IsComplete || progress.TotalSources > 0)
            {
                completedSources += progress.CompletedSources;
                if (progress.TotalSources > 0) totalSources = progress.TotalSources;
                SseProgress = new SearchProgress
                {
                    TotalSources = totalSources,
                    CompletedSources = Math.Min(completedSources, Math.Max(totalSources, completedSources)),
                    CurrentSource = progress.CurrentSource,
                    IsComplete = progress.IsComplete,
                };
            }
            if (progress.IsComplete) IsLoading = false;
        };
        _sseClient.Errors += msg => ErrorMessage = msg;

        await _sseClient.StartSearchAsync(_lastQuery, serverUrl, cookie, _sseCancellationTokenSource.Token).ConfigureAwait(false);
        if (SseProgress is null || !SseProgress.IsComplete)
        {
            // SSE failed or gave no results — fall back to plain search
            await SearchAsync(provider, _lastQuery, CancellationToken.None).ConfigureAwait(false);
        }
        IsLoading = false;
    }

    public void StopStreamingSearch()
    {
        _sseClient?.Stop();
        _sseCancellationTokenSource?.Cancel();
        _sseCancellationTokenSource?.Dispose();
        _sseCancellationTokenSource = null;
        _sseClient = null;
    }

    public void ClearFilters()
    {
        SourceFilter = null;
        YearFilter = null;
        BlockedKeywordsText = string.Empty;
    }

    public void ToggleAggregate()
    {
        IsAggregating = !IsAggregating;
    }

    private void RebuildAggregates()
    {
        AggregatedResults.Clear();
        foreach (var item in AggregatedSearchResult.RebuildAggregates(Results))
        {
            AggregatedResults.Add(item);
        }
    }
}
