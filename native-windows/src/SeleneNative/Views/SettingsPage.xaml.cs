using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        ContentStack.Children.Add(UiHelpers.PageHeader("设置", "调整外观、播放和账户设置。"));
        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }
        if (!string.IsNullOrWhiteSpace(_vm.UpdateMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("更新", _vm.UpdateMessage, InfoBarSeverity.Informational));
        }

        var appearance = CreateSection("外观与启动");
        var themeBox = new ComboBox
        {
            Header = "应用主题",
            MaxWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = { "跟随系统", "浅色", "深色" },
            SelectedItem = _vm.Theme,
        };
        themeBox.SelectionChanged += (_, _) =>
        {
            _vm.Theme = themeBox.SelectedItem?.ToString() ?? "跟随系统";
            ThemeChanged?.Invoke(_vm.Theme);
        };
        AddField(appearance, themeBox);
        var openHome = new ToggleSwitch { Header = "启动时打开首页", IsOn = _vm.OpenHomeOnLaunch };
        openHome.Toggled += (_, _) => _vm.OpenHomeOnLaunch = openHome.IsOn;
        appearance.Children.Add(openHome);
        ContentStack.Children.Add(appearance);

        var playback = CreateSection("播放");
        var proxyBox = new TextBox
        {
            Header = "M3U8 代理地址",
            PlaceholderText = "可选",
            Text = _vm.M3u8ProxyUrl,
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        proxyBox.TextChanged += (_, _) => _vm.M3u8ProxyUrl = proxyBox.Text;
        AddField(playback, proxyBox);
        var saveButton = new Button
        {
            Content = "保存设置",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        saveButton.Click += async (_, _) =>
        {
            await _vm.SaveCommand.ExecuteAsync(null);
            Render();
        };
        playback.Children.Add(saveButton);
        ContentStack.Children.Add(playback);

        var maintenance = CreateSection("更新与存储");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var updateButton = new Button { Content = "检查更新" };
        updateButton.Click += async (_, _) =>
        {
            await _vm.CheckUpdateCommand.ExecuteAsync(null);
            Render();
        };
        actions.Children.Add(updateButton);
        var cacheButton = new Button { Content = "清理缓存" };
        cacheButton.Click += async (_, _) =>
        {
            await _vm.ClearCacheCommand.ExecuteAsync(null);
            Render();
        };
        actions.Children.Add(cacheButton);
        maintenance.Children.Add(actions);
        ContentStack.Children.Add(maintenance);

        var account = CreateSection("账户与服务端");
        if (string.IsNullOrWhiteSpace(_serverUrl))
        {
            account.Children.Add(SecondaryText("登录后可查看和管理 LunaTV 平台功能。"));
        }
        else
        {
            account.Children.Add(SecondaryText($"服务端：{_serverUrl}"));
            account.Children.Add(new HyperlinkButton
            {
                Content = "在浏览器中打开管理后台",
                NavigateUri = new Uri($"{_serverUrl.TrimEnd('/')}/admin"),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        var logoutButton = new Button { Content = "退出登录", HorizontalAlignment = HorizontalAlignment.Left };
        logoutButton.Click += async (_, _) =>
        {
            await _vm.LogoutCommand.ExecuteAsync(null);
            Render();
        };
        account.Children.Add(logoutButton);
        ContentStack.Children.Add(account);
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

    private static StackPanel CreateSection(string title)
    {
        var panel = new StackPanel
        {
            Spacing = 14,
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
        };
        UiHelpers.SetThemeResource(panel, StackPanel.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        UiHelpers.SetThemeResource(panel, StackPanel.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        return panel;
    }

    private static TextBlock SecondaryText(string text)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        UiHelpers.SetThemeResource(label, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return label;
    }
}
