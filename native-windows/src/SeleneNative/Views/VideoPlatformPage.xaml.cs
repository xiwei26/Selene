using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Models;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class VideoPlatformPage : UserControl
{
    private VideoPlatformViewModel? _vm;

    public event Func<SearchResult, string, string, int, Task>? PlayRequested;

    public VideoPlatformPage()
    {
        InitializeComponent();
    }

    public void Build(VideoPlatformViewModel viewModel)
    {
        _vm = viewModel;
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;

        ContentStack.Children.Clear();
        var title = _vm.Kind == VideoPlatformKind.YouTube ? "YouTube" : "Bilibili";
        ContentStack.Children.Add(UiHelpers.PageHeader(title, "Browse LunaTV server-backed video platform results"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("Error", _vm.ErrorMessage, InfoBarSeverity.Warning));
        }

        var queryBox = new TextBox
        {
            PlaceholderText = $"Search {title}",
            Text = _vm.SearchQuery,
            Width = 320
        };
        var searchButton = new Button { Content = "Search" };
        searchButton.Click += async (_, _) =>
        {
            _vm.SearchQuery = queryBox.Text;
            await _vm.SearchAsync();
            Render();
        };

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { queryBox, searchButton }
        };

        if (_vm.Kind == VideoPlatformKind.YouTube && _vm.Regions.Count > 0)
        {
            var regionBox = new ComboBox { Width = 180 };
            foreach (var region in _vm.Regions)
            {
                regionBox.Items.Add(region);
            }
            regionBox.DisplayMemberPath = nameof(YouTubeRegion.Name);
            regionBox.SelectedItem = _vm.SelectedRegion;
            regionBox.SelectionChanged += async (_, _) =>
            {
                if (regionBox.SelectedItem is YouTubeRegion region)
                {
                    _vm.SelectedRegion = region;
                    await _vm.LoadInitialAsync();
                    Render();
                }
            };
            topRow.Children.Add(regionBox);
        }

        ContentStack.Children.Add(topRow);

        if (_vm.IsLoading)
        {
            ContentStack.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32 });
            return;
        }

        if (_vm.Items.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState($"No {title} items", "Try a different query or region."));
            return;
        }

        foreach (var item in _vm.Items)
        {
            ContentStack.Children.Add(CreateItemRow(item));
        }

        var loadMoreButton = new Button { Content = "Load more", HorizontalAlignment = HorizontalAlignment.Left };
        loadMoreButton.Click += async (_, _) =>
        {
            await _vm.LoadMoreAsync();
            Render();
        };
        ContentStack.Children.Add(loadMoreButton);
    }

    private UIElement CreateItemRow(VideoPlatformItem item)
    {
        var subtitle = string.Join("  ", new[]
        {
            item.Duration,
            item.Views,
            item.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var row = UiHelpers.Row(item.Title, subtitle);
        var playButton = new Button { Content = "Play" };
        playButton.Click += (_, _) =>
        {
            if (_vm is null) return;

            var url = _vm.TryGetPlayableUrl(item);
            if (url is null)
            {
                Render();
                return;
            }

            var sourceName = _vm.Kind == VideoPlatformKind.YouTube ? "YouTube" : "Bilibili";
            var result = new SearchResult
            {
                Id = item.Id,
                Title = item.Title,
                Poster = item.Thumbnail ?? item.Cover ?? string.Empty,
                Source = sourceName.ToLowerInvariant(),
                SourceName = sourceName,
                Episodes = [url],
                EpisodeTitles = [item.Title]
            };
            _ = PlayRequested?.Invoke(result, item.Title, url, 1);
        };
        row.Children.Add(playButton);
        return row;
    }
}
