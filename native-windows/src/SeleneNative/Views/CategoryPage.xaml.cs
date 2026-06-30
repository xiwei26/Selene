using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class CategoryPage : UserControl
{
    public event Action<DoubanMovie, string>? DoubanMovieClicked;
    public event Action<BangumiItem>? BangumiItemClicked;

    private CategoryViewModel? _vm;
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
        _bangumi = bangumiClient;
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        ContentStack.Children.Clear();

        if (_vm.CategoryKind == "anime")
        {
            AddWeekdaySelector();
        }

        ContentStack.Children.Add(UiHelpers.PageHeader(
            _vm.CategoryKind switch
            {
                "anime" => "\u52a8\u6f2b",
                "tv" => "\u7535\u89c6\u5267",
                "shows" => "\u7efc\u827a",
                _ => "\u7535\u5f71",
            },
            _vm.CategoryKind == "anime" ? "\u756a\u7ec4\u65e5\u5386" : "\u70ed\u95e8\u5185\u5bb9"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("\u9519\u8bef", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        if (_vm.CategoryKind == "anime")
        {
            ContentStack.Children.Add(UiHelpers.BangumiSection("\u756a\u7ec4", _vm.AnimeItems,
                item => BangumiItemClicked?.Invoke(item)));
        }
        else
        {
            ContentStack.Children.Add(UiHelpers.DoubanSection(
                _vm.CategoryKind switch { "tv" => "\u5267\u96c6", "shows" => "\u7efc\u827a", _ => "\u7535\u5f71" },
                _vm.MovieItems,
                movie => DoubanMovieClicked?.Invoke(movie, _vm.CategoryKind)));
        }
    }

    private void AddWeekdaySelector()
    {
        if (_vm is null || _bangumi is null) return;

        var dayNames = new[]
        {
            "\u5468\u4e00",
            "\u5468\u4e8c",
            "\u5468\u4e09",
            "\u5468\u56db",
            "\u5468\u4e94",
            "\u5468\u516d",
            "\u5468\u65e5"
        };
        var dayRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var i = 0; i < 7; i++)
        {
            var weekday = i + 1;
            var button = new Button { Content = dayNames[i], Tag = weekday };
            button.Click += async (_, _) =>
            {
                _vm.Weekday = weekday;
                await _vm.LoadAnimeAsync(_bangumi, reset: true);
                Render();
            };
            dayRow.Children.Add(button);
        }

        ContentStack.Children.Add(dayRow);
    }
}
