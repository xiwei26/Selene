using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class ShortDramaViewModel(IShortDramaClient client) : ObservableObject
{
    private const int PageSize = 24;
    private int _currentPage = 1;
    private bool _isSearchMode;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ShortDramaCategory? _selectedCategory;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ShortDramaCategory> Categories { get; } = [];
    public ObservableCollection<ShortDramaItem> Items { get; } = [];

    public event Action<string>? PlayRequested;

    public async Task LoadInitialAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        _currentPage = 1;
        _isSearchMode = false;
        SelectedCategory = null;

        try
        {
            Categories.Clear();
            foreach (var category in await client.LoadShortDramaCategoriesAsync(cancellationToken).ConfigureAwait(false))
            {
                Categories.Add(category);
            }

            ReplaceItems(await client.LoadShortDramaRecommendAsync(size: PageSize, cancellationToken: cancellationToken)
                .ConfigureAwait(false));
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
        _currentPage = 1;
        _isSearchMode = true;

        try
        {
            ReplaceItems(await client.SearchAsync(SearchQuery.Trim(), _currentPage, PageSize, cancellationToken)
                .ConfigureAwait(false));
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

    public async Task LoadCategoryAsync(
        ShortDramaCategory category,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        SelectedCategory = category;
        _currentPage = 1;
        _isSearchMode = false;

        try
        {
            ReplaceItems(await client.LoadShortDramaListAsync(category.Id, _currentPage, PageSize, cancellationToken)
                .ConfigureAwait(false));
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
            _currentPage++;
            var result = _isSearchMode
                ? await client.SearchAsync(SearchQuery.Trim(), _currentPage, PageSize, cancellationToken).ConfigureAwait(false)
                : SelectedCategory is not null
                    ? await client.LoadShortDramaListAsync(SelectedCategory.Id, _currentPage, PageSize, cancellationToken).ConfigureAwait(false)
                    : await client.LoadShortDramaRecommendAsync(size: PageSize, cancellationToken: cancellationToken).ConfigureAwait(false);

            foreach (var item in result.Items)
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            _currentPage = Math.Max(1, _currentPage - 1);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PlayEpisodeAsync(
        ShortDramaItem item,
        int episode,
        CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;

        try
        {
            var parsed = await client.ParseAsync(item.Id, episode, item.Name, cancellationToken).ConfigureAwait(false);
            var url = FirstNonEmpty(parsed?.ParsedUrl, parsed?.ProxyUrl, parsed?.Url);
            if (string.IsNullOrWhiteSpace(url))
            {
                ErrorMessage = "Short drama playback URL is unavailable.";
                return;
            }

            PlayRequested?.Invoke(url);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReplaceItems(ShortDramaListResult result)
    {
        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
