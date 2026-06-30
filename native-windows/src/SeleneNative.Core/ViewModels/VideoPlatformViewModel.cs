using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public enum VideoPlatformKind
{
    Bilibili,
    YouTube
}

public sealed partial class VideoPlatformViewModel(
    IVideoPlatformClient client,
    VideoPlatformKind kind) : ObservableObject
{
    private const int BilibiliPageSize = 20;
    private int _page = 1;
    private bool _isSearchMode;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private YouTubeRegion? _selectedRegion;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _nextPageToken;

    public VideoPlatformKind Kind { get; } = kind;
    public ObservableCollection<VideoPlatformItem> Items { get; } = [];
    public ObservableCollection<YouTubeRegion> Regions { get; } = [];

    public async Task LoadInitialAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        _page = 1;
        _isSearchMode = false;

        try
        {
            if (Kind == VideoPlatformKind.YouTube)
            {
                var preferredRegionCode = SelectedRegion?.Code;
                if (Regions.Count == 0)
                {
                    foreach (var region in await client.LoadYouTubeRegionsAsync(cancellationToken).ConfigureAwait(false))
                    {
                        Regions.Add(region);
                    }
                }

                SelectedRegion = Regions.FirstOrDefault(region => region.Code == preferredRegionCode)
                    ?? Regions.FirstOrDefault(region => region.Code == "US")
                    ?? Regions.FirstOrDefault()
                    ?? SelectedRegion;
                ReplaceItems(await client.LoadYouTubePopularAsync(SelectedRegion?.Code ?? "US", null, cancellationToken)
                    .ConfigureAwait(false));
            }
            else
            {
                ReplaceItems(await client.LoadBilibiliPopularAsync(_page, BilibiliPageSize, cancellationToken)
                    .ConfigureAwait(false));
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

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadInitialAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        _page = 1;
        _isSearchMode = true;

        try
        {
            var query = SearchQuery.Trim();
            var result = Kind == VideoPlatformKind.YouTube
                ? await client.SearchYouTubeAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await client.SearchBilibiliAsync(query, cancellationToken).ConfigureAwait(false);
            ReplaceItems(result);
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

    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            VideoPlatformPage result;
            if (Kind == VideoPlatformKind.YouTube)
            {
                result = _isSearchMode
                    ? await client.SearchYouTubeAsync(SearchQuery.Trim(), cancellationToken: cancellationToken).ConfigureAwait(false)
                    : await client.LoadYouTubePopularAsync(SelectedRegion?.Code ?? "US", NextPageToken, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _page++;
                result = _isSearchMode
                    ? await client.SearchBilibiliAsync(SearchQuery.Trim(), cancellationToken).ConfigureAwait(false)
                    : await client.LoadBilibiliPopularAsync(_page, BilibiliPageSize, cancellationToken).ConfigureAwait(false);
            }

            NextPageToken = result.NextPageToken;
            foreach (var item in result.Items)
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            if (Kind == VideoPlatformKind.Bilibili)
            {
                _page = Math.Max(1, _page - 1);
            }
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public string? TryGetPlayableUrl(VideoPlatformItem item)
    {
        ErrorMessage = null;
        foreach (var candidate in new[] { item.PlayableUrl, item.ProxyUrl })
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.ToString();
            }
        }

        ErrorMessage = "当前条目暂无可播放地址";
        return null;
    }

    private void ReplaceItems(VideoPlatformPage page)
    {
        Items.Clear();
        foreach (var item in page.Items)
        {
            Items.Add(item);
        }
        NextPageToken = page.NextPageToken;
    }
}
