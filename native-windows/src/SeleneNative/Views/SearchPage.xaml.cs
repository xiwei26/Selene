using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class SearchPage : UserControl
{
    private SearchViewModel? _vm;
    private IContentProvider? _provider;
    private string? _serverUrl;
    private string? _cookie;

    public event Action<SearchResult, IContentProvider?>? DetailRequested;

    public SearchPage()
    {
        InitializeComponent();
    }

    public void Build(SearchViewModel viewModel, IContentProvider? provider, string? serverUrl = null, string? cookie = null)
    {
        _vm = viewModel;
        _provider = provider;
        _serverUrl = serverUrl;
        _cookie = cookie;
        Render();
    }

    public async Task SearchAndRenderAsync(string query)
    {
        if (_vm is null || _provider is null) return;
        if (string.IsNullOrWhiteSpace(query)) return;
        
        if (!string.IsNullOrWhiteSpace(_serverUrl))
        {
            await _vm.SearchWithSSEAsync(_provider, _serverUrl, _cookie ?? "", query);
        }
        else
        {
            await _vm.SearchAsync(_provider, query);
        }
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        LeftPanel.Children.Clear();
        RightPanel.Children.Clear();

        // Header
        LeftPanel.Children.Add(UiHelpers.PageHeader("搜索", "服务端聚合搜索 (SSE)"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            LeftPanel.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        // Search box
        var queryBox = new TextBox { PlaceholderText = "输入片名、剧名或关键词", Width = 420 };
        var searchButton = new Button { Content = "搜索" };
        var sseToggle = new ToggleSwitch { Header = "SSE 流式搜索", IsOn = true, Margin = new Thickness(12, 0, 0, 0) };

        searchButton.Click += async (_, _) =>
        {
            searchButton.IsEnabled = false;
            if (sseToggle.IsOn && _provider is not null && !string.IsNullOrWhiteSpace(_serverUrl))
            {
                await _vm.SearchWithSSEAsync(_provider, _serverUrl, _cookie ?? "", queryBox.Text);
            }
            else
            {
                await _vm.SearchAsync(_provider, queryBox.Text);
            }
            searchButton.IsEnabled = true;
            Render();
        };

        LeftPanel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { queryBox, searchButton, sseToggle },
        });

        // SSE progress
        if (_vm.SseProgress is not null && !_vm.SseProgress.IsComplete)
        {
            var progressBar = new ProgressBar
            {
                Value = _vm.SseProgress.ProgressPercentage * 100,
                Maximum = 100,
                Height = 6,
            };
            var progressText = new TextBlock
            {
                Text = $"正在搜索: {_vm.SseProgress.CompletedSources}/{_vm.SseProgress.TotalSources} 源",
                FontSize = 12,
                Foreground = UiHelpers.SecondaryBrush(),
            };
            LeftPanel.Children.Add(new StackPanel { Spacing = 4, Children = { progressText, progressBar } });
        }

        // Filter bar
        var filterBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var aggButton = new Button { Content = _vm.IsAggregating ? "列表" : "聚合" };
        aggButton.Click += (_, _) => { _vm.ToggleAggregate(); Render(); };
        filterBar.Children.Add(aggButton);

        if (_vm.AvailableSources.Count > 1)
        {
            var sourceBox = new ComboBox { PlaceholderText = "来源", Width = 120 };
            sourceBox.Items.Add("全部");
            foreach (var s in _vm.AvailableSources) sourceBox.Items.Add(s);
            sourceBox.SelectedIndex = 0;
            sourceBox.SelectionChanged += (_, _) =>
            {
                _vm.SourceFilter = sourceBox.SelectedIndex > 0 ? sourceBox.SelectedItem?.ToString() : null;
                Render();
            };
            filterBar.Children.Add(sourceBox);
        }

        if (_vm.AvailableYears.Count > 1)
        {
            var yearBox = new ComboBox { PlaceholderText = "年份", Width = 100 };
            yearBox.Items.Add("全部");
            foreach (var y in _vm.AvailableYears) yearBox.Items.Add(y);
            yearBox.SelectedIndex = 0;
            yearBox.SelectionChanged += (_, _) =>
            {
                _vm.YearFilter = yearBox.SelectedIndex > 0 ? yearBox.SelectedItem?.ToString() : null;
                Render();
            };
            filterBar.Children.Add(yearBox);
        }

        var clearButton = new Button { Content = "清除筛选" };
        clearButton.Click += (_, _) => { _vm.ClearFilters(); Render(); };
        filterBar.Children.Add(clearButton);
        LeftPanel.Children.Add(filterBar);

        // Blocked keywords
        var blockedBox = new TextBox { PlaceholderText = "屏蔽关键词 (逗号分隔)", Width = 300 };
        blockedBox.TextChanged += (_, _) =>
        {
            _vm.BlockedKeywordsText = blockedBox.Text;
            Render();
        };
        LeftPanel.Children.Add(blockedBox);

        // History chips
        if (_vm.History.Count > 0)
        {
            var historyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            historyRow.Children.Add(new TextBlock { Text = "历史:", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
            foreach (var h in _vm.History.Take(8))
            {
                var chip = new HyperlinkButton { Content = h, FontSize = 12 };
                chip.Click += (_, _) =>
                {
                    queryBox.Text = h;
                    searchButton.IsEnabled = false;
                    _ = _vm.SearchAsync(_provider, h).ContinueWith(_ =>
                    {
                        DispatcherQueue.TryEnqueue(() => { searchButton.IsEnabled = true; Render(); });
                    });
                };
                historyRow.Children.Add(chip);
            }
            LeftPanel.Children.Add(historyRow);
        }

        // Results list
        if (_vm.IsAggregating)
        {
            foreach (var agg in _vm.FilteredAggregatedResults)
            {
                var card = CreateAggregateCard(agg);
                LeftPanel.Children.Add(card);
            }
        }
        else
        {
            foreach (var result in _vm.FilteredResults)
            {
                var card = CreateResultCard(result);
                LeftPanel.Children.Add(card);
            }
        }

        if (_vm.Results.Count == 0 && !_vm.IsLoading)
        {
            LeftPanel.Children.Add(UiHelpers.EmptyState("暂无搜索结果", "尝试输入片名或关键词。"));
        }

        // Right panel placeholder
        RightPanel.Children.Add(UiHelpers.PageHeader("详情", "点击左侧结果查看详情"));
    }

    private UIElement CreateResultCard(SearchResult result)
    {
        var row = UiHelpers.Row(result.Title, $"{result.SourceName}  {result.Year}  {result.TypeName}".Trim());
        var detailBtn = new Button { Content = "详情" };
        detailBtn.Click += (_, _) => DetailRequested?.Invoke(result, _provider);
        row.Children.Add(detailBtn);
        return row;
    }

    private UIElement CreateAggregateCard(AggregatedSearchResult agg)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock
        {
            Text = agg.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{agg.Year}  {agg.TypeName}  ({agg.OriginalResults.Count} 源)",
            Foreground = UiHelpers.SecondaryBrush(),
            FontSize = 12,
        });
        var detailBtn = new Button { Content = "详情", HorizontalAlignment = HorizontalAlignment.Left };
        detailBtn.Click += (_, _) =>
        {
            if (agg.OriginalResults.Count > 0)
            {
                DetailRequested?.Invoke(agg.OriginalResults[0], _provider);
            }
        };
        panel.Children.Add(detailBtn);
        return panel;
    }
}
