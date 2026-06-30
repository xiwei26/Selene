using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
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

        // Douban rating badge
        if (_vm.DoubanInfo is not null)
        {
            var rateStr = _vm.DoubanInfo.Rate;
            var hasRate = !string.IsNullOrWhiteSpace(rateStr) && rateStr != "0";
            var ratingBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(hasRate ? Color.FromArgb(255, 255, 215, 0) : Color.FromArgb(128, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 4),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE735", FontSize = 10, Foreground = new SolidColorBrush(hasRate ? Color.FromArgb(255, 255, 215, 0) : Color.FromArgb(128, 128, 128, 128)) },
                        new TextBlock 
                        { 
                            Text = hasRate ? $"豆瓣 {rateStr}" : "暂无评分", 
                            FontSize = 11, 
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                            Foreground = new SolidColorBrush(hasRate ? Color.FromArgb(255, 255, 215, 0) : Color.FromArgb(128, 128, 128, 128)) 
                        }
                    }
                }
            };
            headerPanel.Children.Add(ratingBadge);
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

        if (!string.IsNullOrWhiteSpace(_vm.TmdbBackdrop?.BackdropUrl) &&
            Uri.TryCreate(_vm.TmdbBackdrop.BackdropUrl, UriKind.Absolute, out var backdropUri))
        {
            ContentStack.Children.Add(new Image
            {
                Source = new BitmapImage(backdropUri),
                Stretch = Stretch.UniformToFill,
                MaxHeight = 260,
            });
        }

        if (_vm.DoubanQuickInfo is not null)
        {
            var quickInfoText = string.Join(Environment.NewLine, new[]
            {
                _vm.DoubanQuickInfo.Title,
                _vm.DoubanQuickInfo.Rating is { Length: > 0 } rating ? $"Douban {rating}" : null,
                _vm.DoubanQuickInfo.Summary
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (!string.IsNullOrWhiteSpace(quickInfoText))
            {
                ContentStack.Children.Add(new TextBlock
                {
                    Text = quickInfoText,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 760,
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(_vm.TrailerRefresh?.TrailerUrl) ||
            !string.IsNullOrWhiteSpace(_vm.TrailerRefresh?.Url))
        {
            var trailerUrl = _vm.TrailerRefresh.TrailerUrl ?? _vm.TrailerRefresh.Url;
            ContentStack.Children.Add(UiHelpers.InfoBar("Trailer", trailerUrl!, InfoBarSeverity.Informational));
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
        }
        else
        {
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

        if (_vm.DoubanComments.Count > 0)
        {
            var comments = new StackPanel { Spacing = 8 };
            comments.Children.Add(new TextBlock
            {
                Text = "Douban Comments",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            foreach (var comment in _vm.DoubanComments.Take(5))
            {
                comments.Children.Add(UiHelpers.Row(comment.Username, comment.Content));
            }
            ContentStack.Children.Add(comments);
        }

        if (_vm.DoubanRecommendations.Count > 0)
        {
            var recommendations = new StackPanel { Spacing = 8 };
            recommendations.Children.Add(new TextBlock
            {
                Text = "Related",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            foreach (var movie in _vm.DoubanRecommendations.Take(8))
            {
                recommendations.Children.Add(UiHelpers.Row(movie.Title, $"{movie.Year}  {movie.Rate}".Trim()));
            }
            ContentStack.Children.Add(recommendations);
        }
    }
}
