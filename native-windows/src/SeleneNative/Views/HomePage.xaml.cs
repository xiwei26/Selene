using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
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

        var loginVm = App.Services.GetService(typeof(LoginViewModel)) as LoginViewModel;
        var username = loginVm?.Session?.Username ?? "xiwei26";
        ContentStack.Children.Add(UiHelpers.CreateGreetingBanner(username));

        if (viewModel.HotMovies != null && viewModel.HotMovies.Any())
        {
            var featured = viewModel.HotMovies.First();
            ContentStack.Children.Add(UiHelpers.CreateHeroSection(featured,
                () => DoubanMovieClicked?.Invoke(featured, "movie"),
                () => DoubanMovieClicked?.Invoke(featured, "movie")
            ));
        }

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
