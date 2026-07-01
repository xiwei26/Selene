using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using CommunityToolkit.Mvvm.Input;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class SettingsPage : UserControl
{
    private SettingsViewModel? _vm;
    private string? _serverUrl;

    public event Action<string>? ThemeChanged;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void Build(SettingsViewModel viewModel, string? serverUrl = null)
    {
        _vm = viewModel;
        _serverUrl = serverUrl;
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("设置", "Windows native 客户端设置。"));

        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        if (!string.IsNullOrWhiteSpace(_vm.UpdateMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("更新", _vm.UpdateMessage, InfoBarSeverity.Informational));
        }

        // Theme
        var themeBox = new ComboBox
        {
            Header = "主题",
            Width = 240,
            Items = { "跟随系统", "浅色", "深色" },
        };
        themeBox.SelectedItem = _vm.Theme;
        themeBox.SelectionChanged += (_, _) =>
        {
            _vm.Theme = themeBox.SelectedItem?.ToString() ?? "跟随系统";
            ThemeChanged?.Invoke(_vm.Theme);
        };
        ContentStack.Children.Add(themeBox);

        // Open home on launch
        var openHome = new ToggleSwitch
        {
            Header = "启动时打开首页",
            IsOn = _vm.OpenHomeOnLaunch,
        };
        openHome.Toggled += (_, _) => _vm.OpenHomeOnLaunch = openHome.IsOn;
        ContentStack.Children.Add(openHome);

        // M3U8 proxy
        var proxyBox = new TextBox
        {
            Header = "M3U8 代理地址",
            PlaceholderText = "可选",
            Text = _vm.M3u8ProxyUrl,
            Width = 460,
        };
        proxyBox.TextChanged += (_, _) => _vm.M3u8ProxyUrl = proxyBox.Text;
        ContentStack.Children.Add(proxyBox);

        // Save
        var saveBtn = new Button { Content = "保存设置", HorizontalAlignment = HorizontalAlignment.Left };
        saveBtn.Click += async (_, _) =>
        {
            await _vm.SaveCommand.ExecuteAsync(null);
            Render();
        };
        ContentStack.Children.Add(saveBtn);

        // Check update
        var updateBtn = new Button { Content = "检查更新", HorizontalAlignment = HorizontalAlignment.Left };
        updateBtn.Click += async (_, _) =>
        {
            await _vm.CheckUpdateCommand.ExecuteAsync(null);
            Render();
        };
        ContentStack.Children.Add(updateBtn);

        // Clear cache
        var cacheBtn = new Button { Content = "清理缓存", HorizontalAlignment = HorizontalAlignment.Left };
        cacheBtn.Click += async (_, _) =>
        {
            await _vm.ClearCacheCommand.ExecuteAsync(null);
            Render();
        };
        ContentStack.Children.Add(cacheBtn);

        // Platform features section
        ContentStack.Children.Add(BuildPlatformSection());

        // Logout
        var logoutBtn = new Button { Content = "退出登录", HorizontalAlignment = HorizontalAlignment.Left };
        logoutBtn.Click += async (_, _) =>
        {
            await _vm.LogoutCommand.ExecuteAsync(null);
            Render();
        };
        ContentStack.Children.Add(logoutBtn);
    }

    private UIElement BuildPlatformSection()
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 16, 0, 0),
        };

        panel.Children.Add(new TextBlock
        {
            Text = "LunaTV 平台功能",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        if (string.IsNullOrWhiteSpace(_serverUrl))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "请先登录以查看和管理平台功能状态。",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 187, 203, 186)),
                TextWrapping = TextWrapping.Wrap,
            });
            return panel;
        }

        panel.Children.Add(new TextBlock
        {
            Text = $"服务端：{_serverUrl}",
            FontSize = 13,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 187, 203, 186)),
        });

        // Open admin panel in browser
        var adminLinkBtn = new HyperlinkButton
        {
            Content = "在浏览器中打开管理后台",
            NavigateUri = new Uri($"{_serverUrl.TrimEnd('/')}/admin"),
        };
        panel.Children.Add(adminLinkBtn);

        return panel;
    }
}
