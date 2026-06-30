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
    private TmdbBackdropResult? _tmdbBackdrop;

    [ObservableProperty]
    private DoubanQuickInfo? _doubanQuickInfo;

    [ObservableProperty]
    private TrailerRefreshResult? _trailerRefresh;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<SearchResult> Sources { get; } = [];
    public ObservableCollection<string> Episodes { get; } = [];
    public ObservableCollection<string> EpisodeTitles { get; } = [];
    public ObservableCollection<DoubanComment> DoubanComments { get; } = [];
    public ObservableCollection<DoubanMovie> DoubanRecommendations { get; } = [];

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
        IMetadataEnhancementClient? metadataClient = null,
        CancellationToken cancellationToken = default)
    {
        Result = seed;
        Sources.Clear();
        Episodes.Clear();
        EpisodeTitles.Clear();
        DoubanComments.Clear();
        DoubanRecommendations.Clear();
        TmdbBackdrop = null;
        DoubanQuickInfo = null;
        TrailerRefresh = null;
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            foreach (var ep in seed.Episodes) Episodes.Add(ep);
            foreach (var title in seed.EpisodeTitles) EpisodeTitles.Add(title);

            Sources.Add(seed);

            if (provider is not null && !string.IsNullOrWhiteSpace(seed.Source) && !string.IsNullOrWhiteSpace(seed.Id))
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

            if (metadataClient is not null)
            {
                await LoadMetadataEnhancementsAsync(seed, metadataClient, cancellationToken).ConfigureAwait(false);
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

    private async Task LoadMetadataEnhancementsAsync(
        SearchResult seed,
        IMetadataEnhancementClient metadataClient,
        CancellationToken cancellationToken)
    {
        var detail = Result ?? seed;

        try
        {
            TmdbBackdrop = await metadataClient.LoadBackdropAsync(
                detail.Title,
                originalTitle: null,
                string.IsNullOrWhiteSpace(detail.Year) ? null : detail.Year,
                detail.TypeName ?? detail.ClassName ?? detail.Source,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TmdbBackdrop = null;
        }

        if (seed.DoubanId is not int doubanId || doubanId <= 0)
        {
            return;
        }

        var id = doubanId.ToString();

        try
        {
            DoubanQuickInfo = await metadataClient.LoadDoubanQuickInfoAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DoubanQuickInfo = null;
        }

        try
        {
            foreach (var comment in await metadataClient.LoadDoubanCommentsAsync(id, cancellationToken: cancellationToken)
                         .ConfigureAwait(false))
            {
                DoubanComments.Add(comment);
            }
        }
        catch
        {
            DoubanComments.Clear();
        }

        try
        {
            var kind = detail.TypeName ?? detail.ClassName ?? "movie";
            foreach (var movie in await metadataClient.LoadDoubanRecommendsAsync(kind, cancellationToken: cancellationToken)
                         .ConfigureAwait(false))
            {
                DoubanRecommendations.Add(movie);
            }
        }
        catch
        {
            DoubanRecommendations.Clear();
        }

        try
        {
            TrailerRefresh = await metadataClient.RefreshTrailerAsync(id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            TrailerRefresh = null;
        }
    }
}
