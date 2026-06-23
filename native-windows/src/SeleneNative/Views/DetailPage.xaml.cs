using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class DetailPage : UserControl
{
    private DetailViewModel? _vm;
    private IContentProvider? _provider;

    public event Func<SearchResult, string, string, int, Task>? PlayRequested;

    public DetailPage()
    {
        InitializeComponent();
    }

    public void Build(DetailViewModel viewModel, IContentProvider? provider)
    {
        _vm = viewModel;
        _provider = provider;
        if (_vm is not null)
        {
            _vm.PlayRequested -= OnVmPlayRequested;
            _vm.PlayRequested += OnVmPlayRequested;
        }
        Render();
    }

    private void OnVmPlayRequested(string url, int index)
    {
        if (_vm?.Result is null) return;
        var title = _vm.EpisodeTitles.Count > index && !string.IsNullOrWhiteSpace(_vm.EpisodeTitles[index])
            ? _vm.EpisodeTitles[index]
            : $"第 {index + 1} 集";
        _ = PlayRequested?.Invoke(_vm.Result, title, url, index + 1);
    }

    private void Render()
    {
        if (_vm?.Result is null) return;
        var result = _vm.Result;
        ContentStack.Children.Clear();

        // Header
        var headerPanel = new StackPanel { Spacing = 8 };
        headerPanel.Children.Add(UiHelpers.PageHeader(result.Title, $"{result.SourceName}  {result.Year}".Trim()));

        // Douban rating
        if (_vm.DoubanInfo is not null)
        {
            var ratingPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            ratingPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_vm.DoubanInfo.Rate) ? "暂无评分" : $"豆瓣 {_vm.DoubanInfo.Rate}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange),
            });
            headerPanel.Children.Add(ratingPanel);
        }

        ContentStack.Children.Add(headerPanel);

        // Error
        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        // Loading
        if (_vm.IsLoading)
        {
            ContentStack.Children.Add(new ProgressRing { Width = 32, Height = 32, IsActive = true });
            return;
        }

        // Description
        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            ContentStack.Children.Add(new TextBlock
            {
                Text = result.Description,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 760,
            });
        }

        // Source switcher
        if (_vm.Sources.Count > 1)
        {
            var sourceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sourceRow.Children.Add(new TextBlock { Text = "源:", VerticalAlignment = VerticalAlignment.Center });
            foreach (var source in _vm.Sources)
            {
                var btn = new Button { Content = source.SourceName, Tag = source };
                btn.Click += async (_, _) =>
                {
                    if (btn.Tag is SearchResult sr)
                    {
                        await _vm.LoadAsync(sr, _provider, null);
                        Render();
                    }
                };
                sourceRow.Children.Add(btn);
            }
            ContentStack.Children.Add(sourceRow);
        }

        // Episodes
        if (_vm.Episodes.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无播放地址", "当前详情没有返回分集地址。"));
            return;
        }

        var episodeSection = new StackPanel { Spacing = 8 };
        episodeSection.Children.Add(new TextBlock
        {
            Text = "选集",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        for (var i = 0; i < _vm.Episodes.Count; i++)
        {
            var title = _vm.EpisodeTitles.Count > i && !string.IsNullOrWhiteSpace(_vm.EpisodeTitles[i])
                ? _vm.EpisodeTitles[i]
                : $"第 {i + 1} 集";
            var idx = i;
            var btn = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Left };
            btn.Click += (_, _) => _vm.PlayEpisodeCommand.Execute(idx);
            episodeSection.Children.Add(btn);
        }

        ContentStack.Children.Add(episodeSection);
    }
}
