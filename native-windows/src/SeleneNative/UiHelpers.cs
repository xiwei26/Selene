using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using SeleneNative.Core.Models;

namespace SeleneNative;

/// <summary>
/// Shared, imperative UI building blocks for the Pages. Each helper returns a
/// <see cref="UIElement"/>; they are not bound to XAML. Phase 0 keeps the
/// existing imperative layout so the MainWindow split is a pure relocation; a
/// later phase can replace these with XAML data templates.
/// </summary>
internal static class UiHelpers
{
    public static UIElement PageHeader(string title, string subtitle, bool isLoading = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 6 };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 32,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = SecondaryBrush(),
        });
        Grid.SetColumn(titleStack, 0);
        grid.Children.Add(titleStack);

        if (isLoading)
        {
            var progressRing = new ProgressRing { Width = 32, Height = 32, IsActive = true };
            Grid.SetColumn(progressRing, 1);
            grid.Children.Add(progressRing);
        }

        return grid;
    }

    public static UIElement CreateGreetingBanner(string username)
    {
        var grid = new Grid
        {
            MinHeight = 140,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 8, 0, 16),
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(255, 99, 102, 241), Offset = 0.0 }, // #6366f1
                    new GradientStop { Color = Color.FromArgb(255, 168, 85, 247), Offset = 0.5 },  // #a855f7
                    new GradientStop { Color = Color.FromArgb(255, 236, 72, 153), Offset = 1.0 }   // #ec4899
                }
            }
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 4
        };

        stack.Children.Add(new TextBlock
        {
            Text = $"晚上好，{username} 👋",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(Colors.White)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "发现更多精彩影视内容 ✨",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255))
        });

        grid.Children.Add(stack);
        return grid;
    }

    public static UIElement CreateHeroSection(DoubanMovie movie, Action onPlayClick, Action onInfoClick)
    {
        var grid = new Grid
        {
            Height = 380,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 0, 16),
            Background = new SolidColorBrush(Color.FromArgb(255, 17, 17, 17))
        };

        if (Uri.TryCreate(movie.Poster, UriKind.Absolute, out var uri))
        {
            grid.Children.Add(new Image
            {
                Source = new BitmapImage(uri),
                Stretch = Stretch.UniformToFill,
                Opacity = 0.5
            });
        }

        var overlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Colors.Transparent, Offset = 0.0 },
                    new GradientStop { Color = Color.FromArgb(180, 0, 0, 0), Offset = 0.6 },
                    new GradientStop { Color = Colors.Black, Offset = 1.0 }
                }
            }
        };
        grid.Children.Add(overlay);

        var contentGrid = new Grid
        {
            Padding = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var stack = new StackPanel { Spacing = 10 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(movie.Rate) && movie.Rate != "0")
        {
            var ratingBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE735", FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)) },
                        new TextBlock { Text = movie.Rate, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)) }
                    }
                }
            };
            badgeRow.Children.Add(ratingBadge);
        }

        if (!string.IsNullOrWhiteSpace(movie.Year))
        {
            badgeRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock { Text = movie.Year, FontSize = 11, Foreground = new SolidColorBrush(Colors.White) }
            });
        }

        badgeRow.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(51, 18, 200, 102)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new TextBlock { Text = "电影", FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 18, 200, 102)) }
        });

        stack.Children.Add(badgeRow);

        stack.Children.Add(new TextBlock
        {
            Text = movie.Title,
            FontSize = 36,
            FontWeight = Microsoft.UI.Text.FontWeights.Black,
            Foreground = new SolidColorBrush(Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        stack.Children.Add(new TextBlock
        {
            Text = "今日推荐影片。Selene 为你精选优质好片，畅享视听盛宴。",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 550
        });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 4, 0, 0) };
        var playBtn = new Button
        {
            Content = "立即播放",
            Background = new SolidColorBrush(Color.FromArgb(255, 18, 200, 102)),
            Foreground = new SolidColorBrush(Colors.Black),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Padding = new Thickness(20, 6, 20, 6),
            CornerRadius = new CornerRadius(6)
        };
        playBtn.Click += (s, e) => onPlayClick();

        var infoBtn = new Button
        {
            Content = "详情信息",
            Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
            Padding = new Thickness(20, 6, 20, 6),
            CornerRadius = new CornerRadius(6)
        };
        infoBtn.Click += (s, e) => onInfoClick();

        btnRow.Children.Add(playBtn);
        btnRow.Children.Add(infoBtn);
        stack.Children.Add(btnRow);

        contentGrid.Children.Add(stack);
        grid.Children.Add(contentGrid);

        return grid;
    }

    public static UIElement PlayRecordSection(string title, IEnumerable<PlayRecord> records)
    {
        return Section(title, records, CreatePlayRecordCard);
    }

    public static UIElement PlayRecordSection(string title, IEnumerable<PlayRecord> records, Action<PlayRecord> onClick)
    {
        return Section(title, records, r => MakeClickable(CreatePlayRecordCard(r), () => onClick(r)));
    }

    public static UIElement DoubanSection(string title, IEnumerable<DoubanMovie> movies)
    {
        return Section(title, movies, movie => CreatePosterCard(
            movie.Title,
            movie.Poster,
            movie.Year,
            string.IsNullOrWhiteSpace(movie.Rate) ? "暂无评分" : $"评分 {movie.Rate}"));
    }

    public static UIElement DoubanSection(string title, IEnumerable<DoubanMovie> movies, Action<DoubanMovie> onClick)
    {
        return Section(title, movies, movie => MakeClickable(CreatePosterCard(
            movie.Title,
            movie.Poster,
            movie.Year,
            string.IsNullOrWhiteSpace(movie.Rate) ? "暂无评分" : $"评分 {movie.Rate}"), () => onClick(movie)));
    }

    public static UIElement BangumiSection(string title, IEnumerable<BangumiItem> items)
    {
        return Section(title, items, item => CreatePosterCard(
            item.DisplayTitle,
            item.Images.BestImageUrl,
            item.AirDate,
            item.Rating.Score > 0 ? $"评分 {item.Rating.Score:0.0}" : "暂无评分"));
    }

    public static UIElement BangumiSection(string title, IEnumerable<BangumiItem> items, Action<BangumiItem> onClick)
    {
        return Section(title, items, item => MakeClickable(CreatePosterCard(
            item.DisplayTitle,
            item.Images.BestImageUrl,
            item.AirDate,
            item.Rating.Score > 0 ? $"评分 {item.Rating.Score:0.0}" : "暂无评分"), () => onClick(item)));
    }

    public static UIElement Section<T>(string title, IEnumerable<T> items, Func<T, UIElement> createCard)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var materialized = items.Take(24).ToList();
        if (materialized.Count == 0)
        {
            section.Children.Add(new TextBlock { Text = $"暂无{title}", Foreground = SecondaryBrush() });
            return section;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        foreach (var item in materialized)
        {
            row.Children.Add(createCard(item));
        }

        var scrollContent = new Border
        {
            Padding = new Thickness(0, 0, 0, 18),
            Child = row,
        };

        section.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = scrollContent,
        });
        return section;
    }

    public static UIElement SearchResults(IEnumerable<SearchResult> results)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "搜索结果",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var materialized = results.ToList();
        if (materialized.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "暂无搜索结果", Foreground = SecondaryBrush() });
            return panel;
        }

        foreach (var result in materialized)
        {
            var row = Row(result.Title, $"{result.SourceName}  {result.Year}  {result.TypeName}".Trim());
            panel.Children.Add(row);
        }

        return panel;
    }

    public static UIElement FavoriteList(IEnumerable<FavoriteItem> items, Func<FavoriteItem, Task> onRemove)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var item in items)
        {
            var row = Row(item.Title, $"{item.SourceName}  {item.Year}  共 {item.TotalEpisodes} 集".Trim());
            var remove = new Button { Content = "取消收藏" };
            remove.Click += async (_, _) => await onRemove(item);
            row.Children.Add(remove);
            panel.Children.Add(row);
        }

        return panel;
    }

    public static StackPanel Row(string title, string subtitle)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        row.Children.Add(new StackPanel
        {
            Width = 520,
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = subtitle,
                    Foreground = SecondaryBrush(),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        });
        return row;
    }

    public static UIElement StringList(string title, IEnumerable<string> items)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = string.Join(" / ", items.Take(12)),
            Foreground = SecondaryBrush(),
            TextWrapping = TextWrapping.Wrap,
        });
        return panel;
    }

    public static UIElement EmptyState(string title, string message)
    {
        return new StackPanel
        {
            Spacing = 8,
            Padding = new Thickness(0, 18, 0, 0),
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                },
                new TextBlock
                {
                    Text = message,
                    Foreground = SecondaryBrush(),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
    }

    public static InfoBar InfoBar(string title, string message, InfoBarSeverity severity)
    {
        return new InfoBar
        {
            IsOpen = true,
            Severity = severity,
            Title = title,
            Message = message,
        };
    }

    public static UIElement CreatePlayRecordCard(PlayRecord record)
    {
        var card = new StackPanel { Width = 180, Spacing = 8 };
        var imageHost = CreateImageHost(record.Cover, 180, 252);

        var overlay = new Border
        {
            Background = new SolidColorBrush(Colors.Black) { Opacity = 0.62 },
            Padding = new Thickness(10, 8, 10, 8),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = record.SourceName, Foreground = new SolidColorBrush(Colors.White), FontSize = 12 },
                    new ProgressBar { Value = record.ProgressPercentage, Maximum = 1, Height = 3 },
                },
            },
        };

        if (imageHost.Child is Grid grid)
        {
            grid.Children.Add(overlay);
        }

        card.Children.Add(imageHost);
        card.Children.Add(CardTitle(record.Title));
        card.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(record.Year)
                ? $"第 {record.EpisodeNumber} 集"
                : $"{record.Year} / 第 {record.EpisodeNumber} 集",
            Foreground = SecondaryBrush(),
            FontSize = 12,
        });
        return card;
    }

    public static UIElement CreatePosterCard(string title, string imageUrl, string metadata, string subtitle)
    {
        var card = new StackPanel { Width = 180, Spacing = 8 };
        card.Children.Add(CreateImageHost(imageUrl, 180, 252));
        card.Children.Add(CardTitle(title));

        var metadataRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        metadataRow.Children.Add(new TextBlock { Text = metadata, Foreground = SecondaryBrush(), FontSize = 12 });
        
        var ratingBrush = SecondaryBrush();
        if (subtitle != null && (subtitle.StartsWith("评分") || subtitle.Contains("评分")))
        {
            ratingBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0));
        }
        metadataRow.Children.Add(new TextBlock { Text = subtitle, Foreground = ratingBrush, FontSize = 12 });
        card.Children.Add(metadataRow);
        return card;
    }

    public static Border CreateImageHost(string imageUrl, double width, double height)
    {
        var grid = new Grid();
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            grid.Children.Add(new Image
            {
                Source = new BitmapImage(uri),
                Stretch = Stretch.Uniform,
            });
        }
        else
        {
            grid.Children.Add(new TextBlock
            {
                Text = "Selene",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SecondaryBrush(),
            });
        }

        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(8),
            Background = CardBrush(),
            Child = grid,
        };
    }

    public static TextBlock CardTitle(string title)
    {
        return new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    public static UIElement MakeClickable(UIElement element, Action onClick)
    {
        return new ClickableElement(element, onClick);
    }

    private class ClickableElement : Grid
    {
        public ClickableElement(UIElement content, Action onClick)
        {
            Children.Add(content);
            Tapped += (s, e) => onClick();
            
            content.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            var scale = new ScaleTransform();
            content.RenderTransform = scale;

            PointerEntered += (s, e) =>
            {
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                scale.ScaleX = 1.03;
                scale.ScaleY = 1.03;
            };

            PointerExited += (s, e) =>
            {
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            };
        }
    }

    public static Brush CardBrush() => new SolidColorBrush(Color.FromArgb(255, 22, 29, 22));

    public static Brush SecondaryBrush() => new SolidColorBrush(Color.FromArgb(255, 187, 203, 186));

    public static Brush PrimaryBrush() => new SolidColorBrush(Color.FromArgb(255, 18, 200, 102));
}
