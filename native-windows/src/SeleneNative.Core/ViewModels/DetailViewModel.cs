using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class DetailViewModel : ObservableObject
{
    [ObservableProperty]
    private SearchResult? _result;

    [ObservableProperty]
    private DoubanMovie? _doubanInfo;

    [ObservableProperty]
    private TmdbBackdrop? _tmdbBackdrop;

    [ObservableProperty]
    private DoubanQuickInfo? _quickInfo;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<SearchResult> Sources { get; } = [];
    public ObservableCollection<string> Episodes { get; } = [];
    public ObservableCollection<string> EpisodeTitles { get; } = [];
    public ObservableCollection<DoubanComment> Comments { get; } = [];
    public ObservableCollection<DoubanRecommendation> Recommendations { get; } = [];

    public event Action<string, int>? PlayRequested;

    [RelayCommand]
    private void PlayEpisode(int index)
    {
        if (Result is null || index < 0 || index >= Episodes.Count) return;
        PlayRequested?.Invoke(Episodes[index], index);
    }

    public async Task LoadAsync(
        SearchResult seed,
        IContentProvider? provider,
        IDoubanClient? doubanClient,
        CancellationToken cancellationToken = default)
    {
        Result = seed;
        Sources.Clear();
        Episodes.Clear();
        EpisodeTitles.Clear();
        Comments.Clear();
        Recommendations.Clear();
        TmdbBackdrop = null;
        QuickInfo = null;
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            foreach (var ep in seed.Episodes) Episodes.Add(ep);
            foreach (var title in seed.EpisodeTitles) EpisodeTitles.Add(title);

            Sources.Add(seed);

            if (provider is not null &&
                !string.Equals(seed.Source, "shortdrama", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(seed.Source) &&
                !string.IsNullOrWhiteSpace(seed.Id))
            {
                var detail = await provider.DetailAsync(seed.Source, seed.Id, cancellationToken).ConfigureAwait(false);
                if (detail is not null && detail.Episodes.Count > 0)
                {
                    Result = detail;
                    Episodes.Clear();
                    EpisodeTitles.Clear();
                    foreach (var ep in detail.Episodes) Episodes.Add(ep);
                    foreach (var title in detail.EpisodeTitles) EpisodeTitles.Add(title);

                    if (detail.Source != seed.Source)
                    {
                        Sources.Add(detail);
                    }
                }
            }

            DoubanInfo = null;
            if (doubanClient is not null && seed.DoubanId is int doubanId && doubanId > 0)
            {
                try
                {
                    DoubanInfo = await doubanClient.GetDetailAsync(doubanId.ToString(), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Douban failure does not block the detail page
                }
            }

            if (provider is not null)
            {
                try
                {
                    var type = string.Equals(seed.Source, "movie", StringComparison.OrdinalIgnoreCase) ? "movie" : "tv";
                    TmdbBackdrop = await provider.GetTmdbBackdropAsync(Result?.Title ?? seed.Title, Result?.Year ?? seed.Year, type, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // TMDB enrichment is optional.
                }

                try
                {
                    QuickInfo = await provider.GetDoubanQuickInfoAsync(Result?.Title ?? seed.Title, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Douban quick-info is optional.
                }

                if (seed.DoubanId is int detailDoubanId && detailDoubanId > 0)
                {
                    try
                    {
                        foreach (var comment in await provider.GetDoubanCommentsAsync(detailDoubanId.ToString(), cancellationToken)
                                     .ConfigureAwait(false))
                        {
                            Comments.Add(comment);
                        }
                    }
                    catch
                    {
                        // Douban comments are optional.
                    }

                    try
                    {
                        foreach (var item in await provider.GetDoubanRecommendationsAsync(detailDoubanId.ToString(), cancellationToken)
                                     .ConfigureAwait(false))
                        {
                            Recommendations.Add(item);
                        }
                    }
                    catch
                    {
                        // Douban recommendations are optional.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
