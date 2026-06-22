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
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<SearchResult> Sources { get; } = [];
    public ObservableCollection<string> Episodes { get; } = [];
    public ObservableCollection<string> EpisodeTitles { get; } = [];

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
