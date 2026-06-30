using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Models;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class ShortDramaPage : UserControl
{
    private ShortDramaViewModel? _vm;
    private ShortDramaItem? _pendingPlayItem;
    private int _pendingPlayEpisode;

    public event Func<SearchResult, string, string, int, Task>? PlayRequested;

    public ShortDramaPage()
    {
        InitializeComponent();
    }

    public void Build(ShortDramaViewModel viewModel)
    {
        if (_vm is not null)
        {
            _vm.PlayRequested -= OnPlayRequested;
        }

        _vm = viewModel;
        _vm.PlayRequested += OnPlayRequested;
        Render();
    }

    private void OnPlayRequested(string url)
    {
        if (_pendingPlayItem is null) return;

        var result = new SearchResult
        {
            Id = _pendingPlayItem.Id,
            Title = _pendingPlayItem.Name,
            Poster = _pendingPlayItem.Cover ?? string.Empty,
            Source = "shortdrama",
            SourceName = "Short Drama",
            Year = _pendingPlayItem.Year ?? string.Empty,
            Episodes = [url],
            EpisodeTitles = [$"Episode {_pendingPlayEpisode}"]
        };

        _ = PlayRequested?.Invoke(result, $"Episode {_pendingPlayEpisode}", url, _pendingPlayEpisode);
    }

    private void Render()
    {
        if (_vm is null) return;

        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("Short Drama", "Browse and play LunaTV short drama episodes"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("Error", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        var searchBox = new TextBox
        {
            PlaceholderText = "Search short dramas",
            Text = _vm.SearchQuery,
            Width = 320
        };
        var searchButton = new Button { Content = "Search" };
        searchButton.Click += async (_, _) =>
        {
            _vm.SearchQuery = searchBox.Text;
            await _vm.SearchAsync();
            Render();
        };

        var refreshButton = new Button { Content = "Refresh" };
        refreshButton.Click += async (_, _) =>
        {
            await _vm.LoadInitialAsync();
            Render();
        };

        ContentStack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { searchBox, searchButton, refreshButton }
        });

        if (_vm.Categories.Count > 0)
        {
            var categoryRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var category in _vm.Categories)
            {
                var button = new Button { Content = category.Name, Tag = category };
                button.Click += async (_, _) =>
                {
                    if (button.Tag is ShortDramaCategory selected)
                    {
                        await _vm.LoadCategoryAsync(selected);
                        Render();
                    }
                };
                categoryRow.Children.Add(button);
            }
            ContentStack.Children.Add(categoryRow);
        }

        if (_vm.IsLoading)
        {
            ContentStack.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32 });
            return;
        }

        if (_vm.Items.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("No short dramas", "Sign in to a LunaTV server or try another query."));
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

    private UIElement CreateItemRow(ShortDramaItem item)
    {
        var subtitle = string.Join("  ", new[]
        {
            item.Year,
            item.EpisodeCount is int count && count > 0 ? $"{count} episodes" : null,
            item.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var row = UiHelpers.Row(item.Name, subtitle);
        var playButton = new Button { Content = "Play" };
        playButton.Click += async (_, _) =>
        {
            if (_vm is null) return;

            _pendingPlayItem = item;
            _pendingPlayEpisode = 1;
            await _vm.PlayEpisodeAsync(item, 1);
            Render();
        };
        row.Children.Add(playButton);
        return row;
    }
}
