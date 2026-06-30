using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class FavoritesViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<FavoriteItem> Favorites { get; } = [];

    public async Task LoadAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        Favorites.Clear();
        ErrorMessage = null;
        if (provider is null)
        {
            ErrorMessage = "请先登录后查看收藏";
            return;
        }

        IsLoading = true;
        try
        {
            foreach (var item in await provider.GetFavoritesAsync(cancellationToken).ConfigureAwait(false))
            {
                Favorites.Add(item);
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

    public async Task RemoveAsync(
        IContentProvider? provider,
        FavoriteItem item,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            ErrorMessage = "请先登录后管理收藏";
            return;
        }

        try
        {
            await provider.RemoveFavoriteAsync(item.Source, item.ItemId, cancellationToken).ConfigureAwait(false);
            Favorites.Remove(item);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
