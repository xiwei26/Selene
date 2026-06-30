using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IDoubanClient _doubanClient;
    private readonly IBangumiClient _bangumiClient;
    private readonly IPlayRecordStore _playRecordStore;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public HomeViewModel()
        : this(new DoubanClient(), new BangumiClient(), new PlayRecordStore())
    {
    }

    public HomeViewModel(
        IDoubanClient doubanClient,
        IBangumiClient bangumiClient,
        IPlayRecordStore playRecordStore)
    {
        _doubanClient = doubanClient;
        _bangumiClient = bangumiClient;
        _playRecordStore = playRecordStore;
    }

    public ObservableCollection<PlayRecord> PlayRecords { get; } = [];
    public ObservableCollection<DoubanMovie> HotMovies { get; } = [];
    public ObservableCollection<DoubanMovie> HotTvShows { get; } = [];
    public ObservableCollection<BangumiItem> TodayBangumi { get; } = [];
    public ObservableCollection<DoubanMovie> HotShows { get; } = [];

    public async Task LoadAsync(
        IContentProvider? provider = null,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var recordsTask = TryLoadAsync(() => provider is null
                ? _playRecordStore.LoadAsync(cancellationToken)
                : provider.GetPlayRecordsAsync(cancellationToken));
            var moviesTask = TryLoadAsync(() => _doubanClient.GetHotMoviesAsync(cancellationToken));
            var tvTask = TryLoadAsync(() => _doubanClient.GetHotTvShowsAsync(cancellationToken));
            var bangumiTask = TryLoadAsync(() => _bangumiClient.GetTodayCalendarAsync(cancellationToken));
            var showsTask = TryLoadAsync(() => _doubanClient.GetHotShowsAsync(cancellationToken));

            ReplaceItems(PlayRecords, await recordsTask);
            ReplaceItems(HotMovies, await moviesTask);
            ReplaceItems(HotTvShows, await tvTask);
            ReplaceItems(TodayBangumi, await bangumiTask);
            ReplaceItems(HotShows, await showsTask);

            if (PlayRecords.Count == 0 &&
                HotMovies.Count == 0 &&
                HotTvShows.Count == 0 &&
                TodayBangumi.Count == 0 &&
                HotShows.Count == 0)
            {
                ErrorMessage = "首页暂时没有加载到内容";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<IReadOnlyList<T>> TryLoadAsync<T>(
        Func<Task<IReadOnlyList<T>>> load)
    {
        try
        {
            return await load();
        }
        catch
        {
            return [];
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
