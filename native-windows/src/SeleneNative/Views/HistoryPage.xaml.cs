using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class HistoryPage : UserControl
{
    public HistoryPage()
    {
        InitializeComponent();
    }

    public void Build(HistoryViewModel viewModel, IContentProvider? provider)
    {
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader(
            "历史",
            provider is null ? "本地播放记录。" : "服务端播放记录。"));
        if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", viewModel.ErrorMessage, InfoBarSeverity.Error));
        }

        if (viewModel.PlayRecords.Count == 0)
        {
            ContentStack.Children.Add(UiHelpers.EmptyState("暂无播放历史", "还没有播放记录。"));
            return;
        }

        ContentStack.Children.Add(UiHelpers.PlayRecordSection("播放历史", viewModel.PlayRecords));
    }
}
