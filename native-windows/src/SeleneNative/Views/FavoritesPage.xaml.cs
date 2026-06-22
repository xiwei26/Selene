using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class FavoritesPage : UserControl
{
    public FavoritesPage()
    {
        InitializeComponent();
    }

    public void Build(FavoritesViewModel viewModel, IContentProvider? provider)
    {
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("收藏", "服务端收藏的视频、剧集和播放源。"));
        if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", viewModel.ErrorMessage, InfoBarSeverity.Error));
        }

        if (viewModel.Favorites.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无收藏", "登录后可以同步服务端收藏。"));
            return;
        }

        ContentStack.Children.Add(UiHelpers.FavoriteList(viewModel.Favorites, async item =>
        {
            await viewModel.RemoveAsync(provider, item);
            Build(viewModel, provider);
        }));
    }
}
