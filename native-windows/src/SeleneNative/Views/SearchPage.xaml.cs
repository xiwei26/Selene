using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Windows.System;

namespace SeleneNative.Views;

public sealed partial class SearchPage : UserControl
{
    private SearchViewModel? _vm;
    private IContentProvider? _provider;
    private string? _serverUrl;
    private string? _cookie;
    private bool _updatingControls;
    private bool _searching;

    public event Action<SearchResult, IContentProvider?>? DetailRequested;

    public SearchPage()
    {
        InitializeComponent();
        SearchButton.Click += async (_, _) => await SearchAndRenderAsync(QueryBox.Text);
        QueryBox.KeyDown += async (_, e) =>
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            await SearchAndRenderAsync(QueryBox.Text);
        };
        AggregateButton.Click += (_, _) =>
        {
            _vm?.ToggleAggregate();
            RenderResults();
        };
        SourceBox.SelectionChanged += (_, _) =>
        {
            if (_updatingControls || _vm is null) return;
            _vm.SourceFilter = (SourceBox.SelectedItem as ComboBoxItem)?.Tag as string;
            RenderResults();
        };
        YearBox.SelectionChanged += (_, _) =>
        {
            if (_updatingControls || _vm is null) return;
            _vm.YearFilter = YearBox.SelectedIndex > 0 ? YearBox.SelectedItem?.ToString() : null;
            RenderResults();
        };
        BlockedBox.TextChanged += (_, _) =>
        {
            if (_updatingControls || _vm is null) return;
            _vm.BlockedKeywordsText = BlockedBox.Text;
            RenderResults();
        };
        ClearFiltersButton.Click += (_, _) =>
        {
            if (_vm is null) return;
            _vm.ClearFilters();
            RefreshFilters();
            RenderResults();
        };
    }

    public void Build(SearchViewModel viewModel, IContentProvider? provider, string? serverUrl = null, string? cookie = null)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = viewModel;
        _provider = provider;
        _serverUrl = serverUrl;
        _cookie = cookie;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        RefreshFilters();
        RenderHistory();
        RenderStatus();
        RenderResults();
    }

    public async Task SearchAndRenderAsync(string query)
    {
        if (_vm is null || _searching || string.IsNullOrWhiteSpace(query)) return;
        QueryBox.Text = query.Trim();
        _searching = true;
        SearchButton.IsEnabled = false;
        ResultsStack.Children.Clear();
        try
        {
            if (StreamingToggle.IsOn && _provider is not null && !string.IsNullOrWhiteSpace(_serverUrl))
            {
                await _vm.SearchWithSSEAsync(_provider, _serverUrl, _cookie ?? "", QueryBox.Text);
            }
            else
            {
                await _vm.SearchAsync(_provider, QueryBox.Text);
            }
        }
        finally
        {
            _searching = false;
            SearchButton.IsEnabled = true;
            RefreshFilters();
            RenderHistory();
            RenderStatus();
            RenderResults();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SearchViewModel.IsLoading) or nameof(SearchViewModel.SseProgress) or nameof(SearchViewModel.ErrorMessage))
        {
            DispatcherQueue.TryEnqueue(RenderStatus);
        }
    }

    private void RenderStatus()
    {
        if (_vm is null) return;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(_vm.ErrorMessage);
        ErrorBar.Message = _vm.ErrorMessage ?? string.Empty;
        ProgressPanel.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        SearchProgress.IsIndeterminate = _vm.SseProgress is null || _vm.SseProgress.TotalSources <= 0;
        SearchProgress.Value = (_vm.SseProgress?.ProgressPercentage ?? 0) * 100;
        ProgressText.Text = _vm.SseProgress is { TotalSources: > 0 } progress
            ? $"正在搜索：{progress.CompletedSources}/{progress.TotalSources} 个来源"
            : "正在搜索…";
    }

    private void RefreshFilters()
    {
        if (_vm is null) return;
        _updatingControls = true;
        try
        {
            SourceBox.Items.Clear();
            var allSources = new ComboBoxItem { Content = "全部来源" };
            SourceBox.Items.Add(allSources);
            SourceBox.SelectedItem = allSources;
            foreach (var source in _vm.Results.GroupBy(result => result.Source).OrderBy(group => group.First().SourceName))
            {
                var item = new ComboBoxItem { Content = source.First().SourceName, Tag = source.Key };
                SourceBox.Items.Add(item);
                if (source.Key == _vm.SourceFilter) SourceBox.SelectedItem = item;
            }
            if (SourceBox.SelectedIndex == 0) _vm.SourceFilter = null;
            YearBox.Items.Clear();
            YearBox.Items.Add("全部年份");
            foreach (var year in _vm.AvailableYears) YearBox.Items.Add(year);
            YearBox.SelectedItem = _vm.YearFilter is { } filter && YearBox.Items.Contains(filter) ? filter : "全部年份";
            if (YearBox.SelectedIndex == 0) _vm.YearFilter = null;
            BlockedBox.Text = _vm.BlockedKeywordsText;
        }
        finally
        {
            _updatingControls = false;
        }
    }

    private void RenderHistory()
    {
        HistoryRow.Children.Clear();
        if (_vm is null || _vm.History.Count == 0) return;
        HistoryRow.Children.Add(new TextBlock { Text = "最近搜索", VerticalAlignment = VerticalAlignment.Center });
        foreach (var query in _vm.History.Take(8))
        {
            var chip = new HyperlinkButton { Content = query };
            chip.Click += async (_, _) => await SearchAndRenderAsync(query);
            HistoryRow.Children.Add(chip);
        }
    }

    private void RenderResults()
    {
        if (_vm is null || _searching) return;
        ResultsStack.Children.Clear();
        AggregateButton.Content = _vm.IsAggregating ? "显示列表" : "合并同名内容";
        var results = _vm.FilteredResults;
        if (_vm.IsAggregating)
        {
            foreach (var aggregate in AggregatedSearchResult.RebuildAggregates(results))
            {
                ResultsStack.Children.Add(CreateAggregateCard(aggregate));
            }
        }
        else
        {
            foreach (var result in results) ResultsStack.Children.Add(CreateResultCard(result));
        }
        if (results.Count == 0 && !_vm.IsLoading)
        {
            ResultsStack.Children.Add(_vm.Results.Count == 0
                ? UiHelpers.EmptyState("暂无搜索结果", "输入片名或关键词开始搜索。")
                : UiHelpers.EmptyState("没有符合筛选的结果", "调整来源、年份或屏蔽关键词。"));
        }
    }

    private UIElement CreateResultCard(SearchResult result)
    {
        var row = UiHelpers.Row(result.Title, $"{result.SourceName}  {result.Year}  {result.TypeName}".Trim());
        var detailButton = new Button { Content = "查看详情" };
        detailButton.Click += (_, _) => DetailRequested?.Invoke(result, _provider);
        row.Children.Add(detailButton);
        return row;
    }

    private UIElement CreateAggregateCard(AggregatedSearchResult aggregate)
    {
        var row = UiHelpers.Row(aggregate.Title, $"{aggregate.Year}  {aggregate.TypeName}  {aggregate.OriginalResults.Count} 个来源".Trim());
        var detailButton = new Button { Content = "查看详情" };
        detailButton.Click += (_, _) => DetailRequested?.Invoke(aggregate.OriginalResults[0], _provider);
        row.Children.Add(detailButton);
        return row;
    }
}
