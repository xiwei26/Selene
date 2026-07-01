using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Windows.System;
using Windows.UI;
using MH = Microsoft.UI;

namespace SeleneNative.Views;

public sealed partial class LunaFeaturePage : UserControl
{
    private IContentProvider? _provider;
    private string _kind = string.Empty;
    private TextBox? _searchBox;

    public event Action<SearchResult, IContentProvider?>? ShortDramaDetailRequested;

    public LunaFeaturePage()
    {
        InitializeComponent();
    }

    public async Task BuildAsync(string kind, IContentProvider? provider)
    {
        _kind = kind;
        _provider = provider;
        ContentStack.Children.Clear();

        if (_provider is null)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("请先登录", "登录后才能使用 LunaTV 后端的短剧、Bilibili 和 YouTube 功能。"));
            return;
        }

        ContentStack.Children.Add(UiHelpers.PageHeader(PageTitle(kind), PageSubtitle(kind), isLoading: true));
        AddSearchRow();

        try
        {
            if (kind == "shortdrama")
            {
                var items = await _provider.GetRecommendedShortDramasAsync();
                RenderShortDramas("推荐短剧", items);
            }
            else if (kind == "youtube")
            {
                var items = await _provider.GetYouTubePopularAsync();
                RenderPlatformItems("热门 YouTube", items);
            }
            else
            {
                var items = await _provider.GetBilibiliPopularAsync();
                RenderPlatformItems("热门 Bilibili", items);
            }
        }
        catch (ApiException ex) when (ex.FeatureDisabled)
        {
            ContentStack.Children.Clear();
            ContentStack.Children.Add(UiHelpers.PageHeader(PageTitle(kind), PageSubtitle(kind)));

            var disabledPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 24, 0, 0) };
            disabledPanel.Children.Add(new TextBlock
            {
                Text = "功能未启用",
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            disabledPanel.Children.Add(new TextBlock
            {
                Text = $"请在 LunaTV 管理后台开启 {PageTitle(kind)} 功能。{ex.Message}",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 187, 203, 186)),
                TextWrapping = TextWrapping.Wrap,
            });

            // Extract server URL from the provider (ServerApiClient)
            var serverUrl = _provider is ServerApiClient sapi ? sapi.BaseUrl : (string?)null;
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                var adminBtn = new Button
                {
                    Content = "打开管理后台",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromArgb(255, 18, 200, 102)),
                    Foreground = new SolidColorBrush(MH.Colors.Black),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Padding = new Thickness(16, 8, 16, 8),
                    CornerRadius = new CornerRadius(6),
                };
                adminBtn.Click += async (_, _) =>
                {
                    var adminUri = new Uri($"{serverUrl.TrimEnd('/')}/admin");
                    await Launcher.LaunchUriAsync(adminUri);
                };
                disabledPanel.Children.Add(adminBtn);
            }

            ContentStack.Children.Add(disabledPanel);
        }
        catch (Exception ex)
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("加载失败", ex.Message, InfoBarSeverity.Error));
        }
    }

    private void AddSearchRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _searchBox = new TextBox
        {
            PlaceholderText = _kind == "shortdrama" ? "搜索短剧" : $"搜索 {PageTitle(_kind)}",
            MinWidth = 360,
        };
        var button = new Button { Content = "搜索" };
        button.Click += async (_, _) => await SearchAsync();
        row.Children.Add(_searchBox);
        row.Children.Add(button);
        ContentStack.Children.Add(row);
    }

    private async Task SearchAsync()
    {
        if (_provider is null || _searchBox is null || string.IsNullOrWhiteSpace(_searchBox.Text))
        {
            return;
        }

        try
        {
            if (_kind == "shortdrama")
            {
                var items = await _provider.SearchShortDramasAsync(_searchBox.Text.Trim());
                RenderShortDramas("短剧搜索结果", items, keepHeader: true);
            }
            else if (_kind == "youtube")
            {
                var items = await _provider.SearchYouTubeAsync(_searchBox.Text.Trim());
                RenderPlatformItems("YouTube 搜索结果", items, keepHeader: true);
            }
            else
            {
                var items = await _provider.SearchBilibiliAsync(_searchBox.Text.Trim());
                RenderPlatformItems("Bilibili 搜索结果", items, keepHeader: true);
            }
        }
        catch (ApiException ex) when (ex.FeatureDisabled)
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("功能未启用", $"请在 LunaTV 管理后台开启 {PageTitle(_kind)} 功能。", InfoBarSeverity.Warning));

            var serverUrl = _provider is ServerApiClient sapi ? sapi.BaseUrl : (string?)null;
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                var adminBtn = new Button
                {
                    Content = "打开管理后台",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromArgb(255, 18, 200, 102)),
                    CornerRadius = new CornerRadius(6),
                };
                adminBtn.Click += async (_, _) =>
                {
                    var adminUri = new Uri($"{serverUrl.TrimEnd('/')}/admin");
                    await Launcher.LaunchUriAsync(adminUri);
                };
                ContentStack.Children.Add(adminBtn);
            }
        }
        catch (Exception ex)
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("搜索失败", ex.Message, InfoBarSeverity.Error));
        }
    }

    private void RenderShortDramas(string title, IReadOnlyList<SearchResult> items, bool keepHeader = false)
    {
        ResetResults(keepHeader);
        if (items.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无短剧", "后端没有返回短剧内容。"));
            return;
        }

        ContentStack.Children.Add(CreateSectionTitle(title));
        var wrap = CreateWrap();
        foreach (var item in items)
        {
            var card = CreateMediaCard(item.Title, item.Poster, "短剧", item.Description);
            card.Click += async (_, _) =>
            {
                if (_provider is null) return;
                var detail = await _provider.GetShortDramaDetailAsync(item.Id, item.Title);
                if (detail is not null)
                {
                    ShortDramaDetailRequested?.Invoke(detail, _provider);
                }
            };
            wrap.Children.Add(card);
        }

        ContentStack.Children.Add(wrap);
    }

    private void RenderPlatformItems(string title, IReadOnlyList<MediaPlatformItem> items, bool keepHeader = false)
    {
        ResetResults(keepHeader);
        if (items.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无内容", "后端没有返回平台视频。"));
            return;
        }

        ContentStack.Children.Add(CreateSectionTitle(title));
        var wrap = CreateWrap();
        foreach (var item in items)
        {
            var card = CreateMediaCard(item.Title, item.Cover, item.Author, item.Description);
            card.Click += async (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(item.Url) && Uri.TryCreate(item.Url, UriKind.Absolute, out var uri))
                {
                    await Launcher.LaunchUriAsync(uri);
                }
            };
            wrap.Children.Add(card);
        }

        ContentStack.Children.Add(wrap);
    }

    private void ResetResults(bool keepHeader)
    {
        if (!keepHeader)
        {
            return;
        }

        while (ContentStack.Children.Count > 2)
        {
            ContentStack.Children.RemoveAt(2);
        }
    }

    private static VariableSizedWrapGrid CreateWrap()
    {
        return new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 220, ItemHeight = 238 };
    }

    private static TextBlock CreateSectionTitle(string title)
    {
        return new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0),
        };
    }

    private static Button CreateMediaCard(string title, string imageUrl, string meta, string? description)
    {
        var stack = new StackPanel { Spacing = 6, Width = 204 };
        var imageHost = new Border
        {
            Width = 204,
            Height = 116,
            Background = new SolidColorBrush(Color.FromArgb(255, 24, 24, 24)),
            CornerRadius = new CornerRadius(8),
            Child = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
                ? new Image { Source = new BitmapImage(uri), Stretch = Stretch.UniformToFill }
                : new FontIcon { Glyph = "\uE714", FontSize = 28, Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)) }
        };
        stack.Children.Add(imageHost);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
        });
        stack.Children.Add(new TextBlock
        {
            Text = meta,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
            });
        }

        return new Button
        {
            Content = stack,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 12, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    private static string PageTitle(string kind) => kind switch
    {
        "shortdrama" => "短剧",
        "bilibili" => "Bilibili",
        "youtube" => "YouTube",
        _ => "平台视频",
    };

    private static string PageSubtitle(string kind) => kind switch
    {
        "shortdrama" => "推荐、搜索、解析播放 LunaTV 短剧内容",
        "bilibili" => "浏览和搜索 Bilibili 视频",
        "youtube" => "浏览和搜索 YouTube 视频",
        _ => "LunaTV 平台内容",
    };
}
