using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

        section.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = row,
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
        var card = new StackPanel { Width = 260, Spacing = 8 };
        var imageHost = CreateImageHost(record.Cover, 260, 146);

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
        metadataRow.Children.Add(new TextBlock { Text = subtitle, Foreground = SecondaryBrush(), FontSize = 12 });
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
                Stretch = Stretch.UniformToFill,
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
            PointerEntered += (s, e) =>
            {
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
            };
        }
    }

    public static Brush CardBrush() => new SolidColorBrush(Colors.Gray) { Opacity = 0.18 };

    public static Brush SecondaryBrush() => new SolidColorBrush(Colors.Gray);
}
