using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SeleneNative.Core.Models;

namespace SeleneNative;

/// <summary>Shared WinUI controls used by the native pages.</summary>
internal static class UiHelpers
{
    /// <summary>Applies a live ThemeResource binding while preserving the control's style.</summary>
    public static void SetThemeResource(FrameworkElement element, DependencyProperty property, string key)
        => ThemeResources.Set(element, property, key);

    public static void InitializeThemeResources(Grid root) => ThemeResources.Initialize(root);

    public static void ApplyAccentStyle(Button button)
    {
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var resource) && resource is Style style)
        {
            button.Style = style;
        }
    }

    public static UIElement PageHeader(string title, string subtitle, bool isLoading = false)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 6 };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        titleStack.Children.Add(SecondaryText(subtitle));
        grid.Children.Add(titleStack);

        if (isLoading)
        {
            var progressRing = new ProgressRing { Width = 28, Height = 28, IsActive = true };
            AutomationProperties.SetName(progressRing, "正在加载");
            Grid.SetColumn(progressRing, 1);
            grid.Children.Add(progressRing);
        }

        return grid;
    }

    public static UIElement CreateRecommendationPanel(DoubanMovie movie, Action onClick)
    {
        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(CreateImageHost(movie.Poster, 132, 185));

        var details = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(SecondaryText("今日推荐"));
        details.Children.Add(new TextBlock
        {
            Text = movie.Title,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var metadata = new[]
        {
            movie.Year,
            string.IsNullOrWhiteSpace(movie.Rate) || movie.Rate == "0" ? string.Empty : $"评分 {movie.Rate}",
        };
        details.Children.Add(SecondaryText(string.Join(" · ", metadata.Where(value => !string.IsNullOrWhiteSpace(value)))));
        details.Children.Add(SecondaryText("查看影片详情并选择播放源。"));

        var viewButton = new Button
        {
            Content = "查看影片",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        ApplyAccentStyle(viewButton);
        AutomationProperties.SetName(viewButton, $"查看 {movie.Title}");
        viewButton.Click += (_, _) => onClick();
        details.Children.Add(viewButton);
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var panel = new Border
        {
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
        SetThemeResource(panel, Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        SetThemeResource(panel, Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return panel;
    }

    public static UIElement PlayRecordSection(string title, IEnumerable<PlayRecord> records)
        => Section(title, records, CreatePlayRecordCard);

    public static UIElement PlayRecordSection(string title, IEnumerable<PlayRecord> records, Action<PlayRecord> onClick)
        => Section(title, records, record => MakeClickable(CreatePlayRecordCard(record), () => onClick(record), $"继续观看 {record.Title}，第 {record.EpisodeNumber} 集"));

    public static UIElement DoubanSection(string title, IEnumerable<DoubanMovie> movies)
        => Section(title, movies, movie => CreatePosterCard(movie.Title, movie.Poster, movie.Year,
            string.IsNullOrWhiteSpace(movie.Rate) ? "暂无评分" : $"评分 {movie.Rate}"));

    public static UIElement DoubanSection(string title, IEnumerable<DoubanMovie> movies, Action<DoubanMovie> onClick)
        => Section(title, movies, movie => MakeClickable(CreatePosterCard(movie.Title, movie.Poster, movie.Year,
            string.IsNullOrWhiteSpace(movie.Rate) ? "暂无评分" : $"评分 {movie.Rate}"), () => onClick(movie), $"查看 {movie.Title}"));

    public static UIElement BangumiSection(string title, IEnumerable<BangumiItem> items)
        => Section(title, items, item => CreatePosterCard(item.DisplayTitle, item.Images.BestImageUrl, item.AirDate,
            item.Rating.Score > 0 ? $"评分 {item.Rating.Score:0.0}" : "暂无评分"));

    public static UIElement BangumiSection(string title, IEnumerable<BangumiItem> items, Action<BangumiItem> onClick)
        => Section(title, items, item => MakeClickable(CreatePosterCard(item.DisplayTitle, item.Images.BestImageUrl, item.AirDate,
            item.Rating.Score > 0 ? $"评分 {item.Rating.Score:0.0}" : "暂无评分"), () => onClick(item), $"查看 {item.DisplayTitle}"));

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
            section.Children.Add(SecondaryText($"暂无{title}"));
            return section;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var item in materialized)
        {
            row.Children.Add(createCard(item));
        }

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = new Border { Padding = new Thickness(2, 2, 2, 18), Child = row },
        };
        AutomationProperties.SetName(scroll, title);
        section.Children.Add(scroll);
        return section;
    }

    public static UIElement SearchResults(IEnumerable<SearchResult> results)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "搜索结果", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var materialized = results.ToList();
        if (materialized.Count == 0)
        {
            panel.Children.Add(SecondaryText("暂无搜索结果"));
            return panel;
        }

        foreach (var result in materialized)
        {
            panel.Children.Add(Row(result.Title, $"{result.SourceName}  {result.Year}  {result.TypeName}".Trim()));
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
            AutomationProperties.SetName(remove, $"取消收藏 {item.Title}");
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
            Children = { CardTitle(title), SecondaryText(subtitle) },
        });
        return row;
    }

    public static UIElement StringList(string title, IEnumerable<string> items)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(SecondaryText(string.Join(" / ", items.Take(12))));
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
                new TextBlock { Text = title, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                SecondaryText(message),
            },
        };
    }

    public static InfoBar InfoBar(string title, string message, InfoBarSeverity severity)
        => new() { IsOpen = true, Severity = severity, Title = title, Message = message };

    public static UIElement CreatePlayRecordCard(PlayRecord record)
    {
        var card = new StackPanel { Width = 180, Spacing = 8 };
        var imageHost = CreateImageHost(record.Cover, 180, 252);
        card.Children.Add(imageHost);
        card.Children.Add(CardTitle(record.Title));
        card.Children.Add(SecondaryText(string.IsNullOrWhiteSpace(record.Year)
            ? $"第 {record.EpisodeNumber} 集"
            : $"{record.Year} / 第 {record.EpisodeNumber} 集", 12));
        card.Children.Add(SecondaryText(record.SourceName, 12));
        var progress = new ProgressBar { Value = record.ProgressPercentage, Maximum = 1, Height = 3 };
        AutomationProperties.SetName(progress, $"{record.Title} 观看进度");
        card.Children.Add(progress);
        return card;
    }

    public static UIElement CreatePosterCard(string title, string imageUrl, string metadata, string subtitle)
    {
        var card = new StackPanel { Width = 180, Spacing = 8 };
        card.Children.Add(CreateImageHost(imageUrl, 180, 252));
        card.Children.Add(CardTitle(title));
        card.Children.Add(SecondaryText(string.Join(" · ", new[] { metadata, subtitle }.Where(value => !string.IsNullOrWhiteSpace(value))), 12));
        return card;
    }

    public static Border CreateImageHost(string imageUrl, double width, double height)
    {
        var grid = new Grid();
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            grid.Children.Add(new Image { Source = new BitmapImage(uri), Stretch = Stretch.Uniform });
        }
        else
        {
            var placeholder = SecondaryText("Selene");
            placeholder.HorizontalAlignment = HorizontalAlignment.Center;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(placeholder);
        }

        var host = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
        SetThemeResource(host, Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
        return host;
    }

    public static TextBlock CardTitle(string title)
        => new() { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };

    public static TextBlock SecondaryText(string text, double fontSize = 14)
    {
        var block = new TextBlock { Text = text, FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        SetThemeResource(block, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return block;
    }

    public static UIElement MakeClickable(UIElement element, Action onClick, string? automationName = null)
    {
        var button = new Button
        {
            Content = element,
            Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetName(button, automationName ?? "查看内容");
        button.Click += (_, _) => onClick();
        return button;
    }
}
