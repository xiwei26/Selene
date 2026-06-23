using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Models;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class HomePage : UserControl
{
    public event Action<PlayRecord>? PlayRecordClicked;
    public event Action<DoubanMovie, string>? DoubanMovieClicked;
    public event Action<BangumiItem>? BangumiItemClicked;

    public HomePage()
    {
        InitializeComponent();
    }

    public void Build(HomeViewModel viewModel)
    {
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("Selene", "继续观看、热门内容与今日番组"));

        if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("首页数据", viewModel.ErrorMessage, InfoBarSeverity.Informational));
        }

        ContentStack.Children.Add(UiHelpers.PlayRecordSection("继续观看", viewModel.PlayRecords,
            record => PlayRecordClicked?.Invoke(record)));

        ContentStack.Children.Add(UiHelpers.DoubanSection("热门电影", viewModel.HotMovies,
            movie => DoubanMovieClicked?.Invoke(movie, "movie")));

        ContentStack.Children.Add(UiHelpers.DoubanSection("热门剧集", viewModel.HotTvShows,
            movie => DoubanMovieClicked?.Invoke(movie, "tv")));

        ContentStack.Children.Add(UiHelpers.BangumiSection("今日番组", viewModel.TodayBangumi,
            item => BangumiItemClicked?.Invoke(item)));

        ContentStack.Children.Add(UiHelpers.DoubanSection("热门综艺", viewModel.HotShows,
            movie => DoubanMovieClicked?.Invoke(movie, "show")));
    }
}
