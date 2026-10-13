using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class AdminPage : UserControl
{
    private AdminViewModel? _vm;
    private IContentProvider? _provider;

    public AdminPage()
    {
        InitializeComponent();
    }

    public async Task BuildAsync(AdminViewModel viewModel, IContentProvider? provider)
    {
        _vm = viewModel;
        _provider = provider;
        await viewModel.LoadCommand.ExecuteAsync(provider);
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        ContentStack.Children.Clear();

        ContentStack.Children.Add(UiHelpers.PageHeader("管理后台", "LunaTV 服务端配置管理。"));

        if (_provider is null)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("请先登录", "登录后才能管理服务端配置。"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        if (!string.IsNullOrWhiteSpace(_vm.SuccessMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("成功", _vm.SuccessMessage, InfoBarSeverity.Success));
        }

        if (!_vm.IsAdminOrOwner && _vm.CurrentRole is not null)
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("权限不足", "仅管理员或站长可修改配置。当前角色：" + _vm.CurrentRole, InfoBarSeverity.Warning));
        }

        if (_vm.IsLoading)
        {
            ContentStack.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32 });
            return;
        }

        // YouTube config section
        ContentStack.Children.Add(BuildYouTubeSection());

        // Bilibili config section
        ContentStack.Children.Add(BuildBilibiliSection());
    }

    private UIElement BuildYouTubeSection()
    {
        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };

        // Header
        panel.Children.Add(new TextBlock
        {
            Text = "YouTube 配置",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        panel.Children.Add(new TextBlock
        {
            Text = "支持 YouTube 官方 API 或演示模式，让用户搜索和观看 YouTube 视频",
            Tag = "SecondaryText",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });

        // Enabled toggle
        var enabledToggle = new ToggleSwitch
        {
            Header = "启用 YouTube 搜索功能",
            IsOn = _vm!.YouTubeEnabled,
        };
        enabledToggle.Toggled += (_, _) =>
        {
            _vm.YouTubeEnabled = enabledToggle.IsOn;
            Render();
        };
        panel.Children.Add(enabledToggle);

        if (!_vm.YouTubeEnabled)
        {
            return ApplyTheme(panel);
        }

        // Demo mode toggle
        var demoToggle = new ToggleSwitch
        {
            Header = "启用演示模式",
            IsOn = _vm.YouTubeEnableDemo,
        };
        demoToggle.Toggled += (_, _) =>
        {
            _vm.YouTubeEnableDemo = demoToggle.IsOn;
            Render();
        };
        panel.Children.Add(demoToggle);

        panel.Children.Add(new TextBlock
        {
            Text = "演示模式使用预设视频数据，无需 API 密钥。关闭后将使用真实的 YouTube API",
            Tag = "SecondaryText",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        // API Key (only show when demo is off)
        if (!_vm.YouTubeEnableDemo)
        {
            var apiKeyBox = new PasswordBox
            {
                Header = "YouTube API 密钥",
                Password = _vm.YouTubeApiKey,
                MaxWidth = 640,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            apiKeyBox.PasswordChanged += (_, _) => _vm.YouTubeApiKey = apiKeyBox.Password;
            AddField(panel, apiKeyBox);

            var hint = new StackPanel { Spacing = 4 };
            hint.Children.Add(new TextBlock
            {
                Text = "获取 API 密钥步骤：",
                FontSize = 12,
                Tag = "SecondaryText",
            });
            hint.Children.Add(new HyperlinkButton
            {
                Content = "1. 访问 Google Cloud Console",
                NavigateUri = new Uri("https://console.cloud.google.com/"),
                FontSize = 12,
            });
            hint.Children.Add(new TextBlock { Text = "2. 创建新项目 → 启用 YouTube Data API v3 → 创建 API 密钥", FontSize = 12, Tag = "SecondaryText" });
            panel.Children.Add(hint);
        }

        // Max results
        var maxResultsBox = new NumberBox
        {
            Header = "每页最大结果数",
            Value = _vm.YouTubeMaxResults,
            Minimum = 1,
            Maximum = 50,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MaxWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        maxResultsBox.ValueChanged += (_, _) => _vm.YouTubeMaxResults = (int)maxResultsBox.Value;
        AddField(panel, maxResultsBox);

        // Regions
        panel.Children.Add(new TextBlock
        {
            Text = $"启用的地区 ({_vm.YouTubeEnabledRegions.Count}个)",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        });

        var regionWrap = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 140,
            ItemHeight = 36,
        };
        foreach (var region in AdminViewModel.AvailableRegions)
        {
            var cb = new CheckBox
            {
                Content = $"{region.Name} ({region.Code})",
                IsChecked = _vm.YouTubeEnabledRegions.Contains(region.Code),
                Tag = region.Code,
            };
            cb.Click += (_, _) => _vm.ToggleYouTubeRegion(region.Code);
            regionWrap.Children.Add(cb);
        }
        panel.Children.Add(regionWrap);

        // Categories
        panel.Children.Add(new TextBlock
        {
            Text = $"启用的分类 ({_vm.YouTubeEnabledCategories.Count}个)",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        });

        var categoryWrap = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 220,
            ItemHeight = 36,
        };
        foreach (var category in AdminViewModel.AvailableCategories)
        {
            var cb = new CheckBox
            {
                Content = category,
                IsChecked = _vm.YouTubeEnabledCategories.Contains(category),
                Tag = category,
            };
            cb.Click += (_, _) => _vm.ToggleYouTubeCategory(category);
            categoryWrap.Children.Add(cb);
        }
        panel.Children.Add(categoryWrap);

        // Save button
        var saveBtn = new Button
        {
            Content = "保存 YouTube 配置",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        saveBtn.Click += async (_, _) =>
        {
            await _vm.SaveYouTubeCommand.ExecuteAsync(null);
            Render();
        };
        panel.Children.Add(saveBtn);

        return ApplyTheme(panel);
    }

    private UIElement BuildBilibiliSection()
    {
        var panel = new StackPanel
        {
            Spacing = 12,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Bilibili 配置",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        panel.Children.Add(new TextBlock
        {
            Text = "搜索 B站视频和番剧，使用 iframe 播放器，自动处理 Wbi 签名",
            Tag = "SecondaryText",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });

        // Login status
        if (_vm!.BilibiliUserInfo is not null)
        {
            var loginInfo = new StackPanel { Spacing = 4 };
            loginInfo.Children.Add(new TextBlock
            {
                Text = $"已登录：{_vm.BilibiliUserInfo.Username} (UID: {_vm.BilibiliUserInfo.Mid})",
                FontSize = 13,
                Tag = "SecondaryText",
            });
            if (_vm.BilibiliUserInfo.IsVip)
            {
                loginInfo.Children.Add(new TextBlock
                {
                    Text = "大会员",
                    FontSize = 12,
                    Tag = "SecondaryText",
                });
            }
            panel.Children.Add(loginInfo);
        }
        else if (_vm.BilibiliLoginStatus != "logged_in")
        {
            panel.Children.Add(new TextBlock
            {
                Text = "未登录 B站账号（可选，登录后搜索结果更完整）",
                FontSize = 13,
                Tag = "SecondaryText",
            });
        }

        // Enabled toggle
        var enabledToggle = new ToggleSwitch
        {
            Header = "启用 B站搜索功能",
            IsOn = _vm.BilibiliEnabled,
        };
        enabledToggle.Toggled += (_, _) => _vm.BilibiliEnabled = enabledToggle.IsOn;
        panel.Children.Add(enabledToggle);

        // Save button
        var saveBtn = new Button
        {
            Content = "保存 Bilibili 配置",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        saveBtn.Click += async (_, _) =>
        {
            await _vm.SaveBilibiliCommand.ExecuteAsync(null);
            Render();
        };
        panel.Children.Add(saveBtn);

        return ApplyTheme(panel);
    }

    private static void AddField(Panel panel, FrameworkElement field)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MaxWidth = field.MaxWidth,
        });
        row.Children.Add(field);
        panel.Children.Add(row);
    }

    private static StackPanel ApplyTheme(StackPanel panel)
    {
        UiHelpers.SetThemeResource(panel, StackPanel.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        UiHelpers.SetThemeResource(panel, StackPanel.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        ApplyTextTheme(panel);
        return panel;
    }

    private static void ApplyTextTheme(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is TextBlock text && text.Tag as string == "SecondaryText")
            {
                UiHelpers.SetThemeResource(text, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            }
            else if (child is Panel nested)
            {
                ApplyTextTheme(nested);
            }
        }
    }
}
