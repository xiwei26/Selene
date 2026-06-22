using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class CategoryPage : UserControl
{
    public event Action<DoubanMovie, string>? DoubanMovieClicked;
    public event Action<BangumiItem>? BangumiItemClicked;

    private CategoryViewModel? _vm;
    private IDoubanClient? _douban;
    private IBangumiClient? _bangumi;

    public CategoryPage()
    {
        InitializeComponent();
    }

    public void Build(
        CategoryViewModel viewModel,
        IDoubanClient doubanClient,
        IBangumiClient bangumiClient)
    {
        _vm = viewModel;
        _douban = doubanClient;
        _bangumi = bangumiClient;
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        ContentStack.Children.Clear();

        // Category selector
        var kinds = new[] { ("movie", "电影"), ("tv", "电视剧"), ("shows", "综艺"), ("anime", "动漫") };
        var selector = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (kind, label) in kinds)
        {
            var btn = new Button { Content = label, Tag = kind };
            btn.Click += async (_, _) =>
            {
                _vm.CategoryKind = kind;
                if (kind == "anime")
                {
                    await _vm.LoadAnimeAsync(_bangumi!);
                }
                else
                {
                    await _vm.LoadMoviesAsync(_douban!, kind, reset: true);
                }
                Render();
            };
            selector.Children.Add(btn);
        }

        ContentStack.Children.Add(selector);

        // Anime weekday selector
        if (_vm.CategoryKind == "anime")
        {
            var dayNames = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            var dayRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            for (var i = 0; i < 7; i++)
            {
                var wd = i + 1;
                var dBtn = new Button { Content = dayNames[i], Tag = wd };
                dBtn.Click += async (_, _) =>
                {
                    _vm.Weekday = wd;
                    await _vm.LoadAnimeAsync(_bangumi!, reset: true);
                    Render();
                };
                dayRow.Children.Add(dBtn);
            }
            ContentStack.Children.Add(dayRow);
        }

        ContentStack.Children.Add(UiHelpers.PageHeader(
            _vm.CategoryKind switch
            {
                "anime" => "动漫",
                "tv" => "电视剧",
                "shows" => "综艺",
                _ => "电影",
            },
            _vm.CategoryKind == "anime" ? "番组日历" : "热门内容"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        if (_vm.CategoryKind == "anime")
        {
            ContentStack.Children.Add(UiHelpers.BangumiSection("番组", _vm.AnimeItems,
                item => BangumiItemClicked?.Invoke(item)));
        }
        else
        {
            ContentStack.Children.Add(UiHelpers.DoubanSection(
                _vm.CategoryKind switch { "tv" => "剧集", "shows" => "综艺", _ => "电影" },
                _vm.MovieItems,
                movie => DoubanMovieClicked?.Invoke(movie, _vm.CategoryKind)));
        }
    }
}
