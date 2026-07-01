using Microsoft.UI;
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

        if (_vm.TmdbBackdrop is not null)
        {
            ContentStack.Children.Add(CreateTmdbBackdropHero(result, _vm.TmdbBackdrop));
        }

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

        if (_vm.QuickInfo is not null)
        {
            ContentStack.Children.Add(CreateQuickInfoPanel(_vm.QuickInfo));
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

        if (_vm.Comments.Count > 0)
        {
            ContentStack.Children.Add(CreateCommentsPanel(_vm.Comments));
        }

        if (_vm.Recommendations.Count > 0)
        {
            ContentStack.Children.Add(CreateRecommendationsPanel(_vm.Recommendations));
        }
    }

    private static UIElement CreateTmdbBackdropHero(SearchResult result, TmdbBackdrop TmdbBackdrop)
    {
        var hero = new Grid
        {
            Height = 360,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(255, 12, 12, 12)),
        };

        var imageUrl = TmdbBackdrop.Backdrop ?? TmdbBackdrop.Poster;
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            hero.Children.Add(new Image
            {
                Source = new BitmapImage(uri),
                Stretch = Stretch.UniformToFill,
                Opacity = 0.55,
            });
        }

        hero.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(30, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(230, 0, 0, 0), Offset = 1 }
                }
            }
        });

        var stack = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Bottom,
            MaxWidth = 760,
        };

        if (Uri.TryCreate(TmdbBackdrop.Logo, UriKind.Absolute, out var logoUri))
        {
            stack.Children.Add(new Image
            {
                Source = new BitmapImage(logoUri),
                Stretch = Stretch.Uniform,
                MaxWidth = 260,
                MaxHeight = 96,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = TmdbBackdrop.Title ?? result.Title,
                FontSize = 34,
                FontWeight = Microsoft.UI.Text.FontWeights.Black,
                Foreground = new SolidColorBrush(Colors.White),
            });
        }

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (TmdbBackdrop.Rating is double rating)
        {
            chips.Children.Add(CreateChip($"TMDB {rating:0.0}", Color.FromArgb(255, 245, 197, 24)));
        }

        if (!string.IsNullOrWhiteSpace(TmdbBackdrop.Year))
        {
            chips.Children.Add(CreateChip(TmdbBackdrop.Year!, Color.FromArgb(210, 255, 255, 255)));
        }

        if (TmdbBackdrop.NumberOfSeasons is int seasons and > 0)
        {
            chips.Children.Add(CreateChip($"共 {seasons} 季", Color.FromArgb(255, 18, 200, 102)));
        }

        stack.Children.Add(chips);

        var overview = TmdbBackdrop.Overview ?? result.Description;
        if (!string.IsNullOrWhiteSpace(overview))
        {
            stack.Children.Add(new TextBlock
            {
                Text = overview,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 4,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            });
        }

        hero.Children.Add(stack);
        return hero;
    }

    private static UIElement CreateQuickInfoPanel(DoubanQuickInfo QuickInfo)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣详情",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var lines = new[]
        {
            JoinLine("类型", QuickInfo.Genres),
            JoinLine("导演", QuickInfo.Directors),
            JoinLine("主演", QuickInfo.Cast),
            !string.IsNullOrWhiteSpace(QuickInfo.Rating) ? $"豆瓣评分：{QuickInfo.Rating}" : string.Empty,
            !string.IsNullOrWhiteSpace(QuickInfo.Summary) ? QuickInfo.Summary : string.Empty,
        }.Where(line => !string.IsNullOrWhiteSpace(line));

        foreach (var line in lines)
        {
            stack.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, MaxWidth = 980 });
        }

        return stack;
    }

    private static UIElement CreateCommentsPanel(IEnumerable<DoubanComment> Comments)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣短评",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        foreach (var comment in Comments.Take(5))
        {
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(comment.Author)
                    ? comment.Content
                    : $"{comment.Author}: {comment.Content}",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 980,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            });
        }

        return stack;
    }

    private static UIElement CreateRecommendationsPanel(IEnumerable<DoubanRecommendation> Recommendations)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "相关推荐",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var item in Recommendations.Take(8))
        {
            row.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Rating) ? item.Title : $"{item.Title}  {item.Rating}",
                MaxWidth = 160,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        stack.Children.Add(row);
        return stack;
    }

    private static Border CreateChip(string text, Color foreground)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(foreground),
            }
        };
    }

    private static string JoinLine(string label, IReadOnlyList<string> values)
    {
        return values.Count > 0 ? $"{label}：{string.Join("、", values)}" : string.Empty;
    }
}
