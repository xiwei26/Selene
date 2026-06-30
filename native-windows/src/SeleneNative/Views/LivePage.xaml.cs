using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class LivePage : UserControl
{
    private LiveViewModel? _vm;
    private IContentProvider? _provider;

    public LivePage()
    {
        InitializeComponent();
    }

    public void Build(LiveViewModel viewModel, IContentProvider? provider)
    {
        _vm = viewModel;
        _provider = provider;
        Render();
    }

    private void Render()
    {
        if (_vm is null) return;
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("直播", "直播源、频道和 EPG 节目表。"));
        if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", _vm.ErrorMessage, InfoBarSeverity.Error));
        }

        if (_vm.Sources.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无直播源", "登录后从服务端同步直播源。"));
            return;
        }

        // Source selector
        var sourcePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var source in _vm.Sources)
        {
            var btn = new Button { Content = source.Name };
            btn.Click += async (_, _) =>
            {
                await _vm.LoadChannelsAsync(_provider, source);
                Render();
            };
            sourcePanel.Children.Add(btn);
        }
        ContentStack.Children.Add(sourcePanel);

        // Group filter
        if (_vm.Groups.Count > 1)
        {
            var groupPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            groupPanel.Children.Add(new TextBlock { Text = "分组:", VerticalAlignment = VerticalAlignment.Center });
            var allBtn = new Button { Content = "全部" };
            allBtn.Click += (_, _) => { _vm.SelectedGroup = null; Render(); };
            groupPanel.Children.Add(allBtn);
            foreach (var group in _vm.Groups)
            {
                var gBtn = new Button { Content = group };
                gBtn.Click += (_, _) => { _vm.SelectedGroup = group; Render(); };
                groupPanel.Children.Add(gBtn);
            }
            ContentStack.Children.Add(groupPanel);
        }

        // Channel grid
        if (_vm.FilteredChannels.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("请选择直播源", "点击上方直播源加载频道。"));
            return;
        }

        var channelList = new StackPanel { Spacing = 10 };
        foreach (var channel in _vm.FilteredChannels)
        {
            var row = UiHelpers.Row(channel.Name, $"{channel.Group}  {channel.Url}");
            var playBtn = new Button { Content = "播放" };
            playBtn.Click += async (_, _) => await OpenUriAsync(channel.Url);
            row.Children.Add(playBtn);

            if (!string.IsNullOrWhiteSpace(channel.TvgId))
            {
                var epgBtn = new Button { Content = "EPG" };
                epgBtn.Click += async (_, _) =>
                {
                    await _vm.LoadEpgAsync(_provider, channel.TvgId, _vm.SelectedSource?.Key ?? "");
                    RenderEpg();
                };
                row.Children.Add(epgBtn);
            }

            channelList.Children.Add(row);
        }
        ContentStack.Children.Add(channelList);
    }

    private void RenderEpg()
    {
        if (_vm?.CurrentEpg?.Programs is null) return;

        var epgPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        epgPanel.Children.Add(new TextBlock
        {
            Text = "节目表",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var now = DateTimeOffset.UtcNow;
        foreach (var program in _vm.CurrentEpg.Programs)
        {
            var isLive = now >= program.StartTime && now < program.EndTime;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = $"{program.StartTime:HH:mm}-{program.EndTime:HH:mm}",
                FontSize = 12,
                Foreground = UiHelpers.SecondaryBrush(),
            });
            row.Children.Add(new TextBlock
            {
                Text = program.Title,
                FontWeight = isLive ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            });
            if (isLive)
            {
                row.Children.Add(new TextBlock
                {
                    Text = "直播中",
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                });
            }
            epgPanel.Children.Add(row);
        }

        ContentStack.Children.Add(epgPanel);
    }

    private static async Task OpenUriAsync(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
