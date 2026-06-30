using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class LiveViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private LiveSource? _selectedSource;

    [ObservableProperty]
    private string? _selectedGroup;

    [ObservableProperty]
    private EpgData? _currentEpg;

    public ObservableCollection<LiveSource> Sources { get; } = [];
    public ObservableCollection<LiveChannel> Channels { get; } = [];
    public ObservableCollection<string> Groups { get; } = [];

    public IReadOnlyList<LiveChannel> FilteredChannels =>
        string.IsNullOrWhiteSpace(SelectedGroup)
            ? Channels
            : Channels.Where(c => c.Group == SelectedGroup).ToList();

    public async Task LoadSourcesAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        Sources.Clear();
        Channels.Clear();
        Groups.Clear();
        ErrorMessage = null;
        if (provider is null)
        {
            ErrorMessage = "请先登录后查看直播";
            return;
        }

        IsLoading = true;
        try
        {
            foreach (var source in await provider.GetLiveSourcesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!source.Disabled)
                {
                    Sources.Add(source);
                }
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

    public async Task LoadChannelsAsync(
        IContentProvider? provider,
        LiveSource source,
        CancellationToken cancellationToken = default)
    {
        Channels.Clear();
        Groups.Clear();
        SelectedSource = source;
        SelectedGroup = null;
        if (provider is null)
        {
            ErrorMessage = "请先登录后查看直播";
            return;
        }

        IsLoading = true;
        try
        {
            foreach (var channel in await provider.GetLiveChannelsAsync(source.Key, cancellationToken)
                         .ConfigureAwait(false))
            {
                Channels.Add(channel);
            }
            RebuildGroups();
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

    public async Task LoadEpgAsync(
        IContentProvider? provider,
        string tvgId,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        CurrentEpg = null;
        if (provider is null || string.IsNullOrWhiteSpace(tvgId)) return;

        try
        {
            CurrentEpg = await provider.GetLiveEpgAsync(tvgId, sourceKey, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // EPG is optional
        }
    }

    private void RebuildGroups()
    {
        Groups.Clear();
        foreach (var group in Channels.Select(c => c.Group).Distinct().OrderBy(g => g))
        {
            Groups.Add(group);
        }
    }
}
