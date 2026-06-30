using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IPlayRecordStore _localStore;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public HistoryViewModel(IPlayRecordStore? localStore = null)
    {
        _localStore = localStore ?? new PlayRecordStore();
    }

    public ObservableCollection<PlayRecord> PlayRecords { get; } = [];

    public async Task LoadAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        PlayRecords.Clear();
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var records = provider is null
                ? await _localStore.LoadAsync(cancellationToken).ConfigureAwait(false)
                : await provider.GetPlayRecordsAsync(cancellationToken).ConfigureAwait(false);

            foreach (var record in records)
            {
                PlayRecords.Add(record);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteAsync(
        IContentProvider? provider,
        PlayRecord record,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            ErrorMessage = "本地历史暂不支持逐条删除";
            return;
        }

        try
        {
            await provider.DeletePlayRecordAsync(record.Source, record.ItemId, cancellationToken).ConfigureAwait(false);
            PlayRecords.Remove(record);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
