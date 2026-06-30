using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class CategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _categoryKind = "movie";

    [ObservableProperty]
    private int _pageIndex;

    [ObservableProperty]
    private int _weekday = 1; // Monday = 1 for Bangumi

    public int PageSize => 24;

    public ObservableCollection<DoubanMovie> MovieItems { get; } = [];
    public ObservableCollection<BangumiItem> AnimeItems { get; } = [];
    public ObservableCollection<int> AvailableWeekdays { get; } = [1, 2, 3, 4, 5, 6, 7];

    public bool HasMore => MovieItems.Count < 100; // conservative cap for Douban hot

    public async Task LoadMoviesAsync(IDoubanClient doubanClient, string kind, bool reset = false)
    {
        CategoryKind = kind;
        AnimeItems.Clear();

        if (reset)
        {
            MovieItems.Clear();
            PageIndex = 0;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var results = kind switch
            {
                "tv" => await doubanClient.GetHotTvShowsAsync().ConfigureAwait(false),
                "shows" => await doubanClient.GetHotShowsAsync().ConfigureAwait(false),
                _ => await doubanClient.GetHotMoviesAsync().ConfigureAwait(false),
            };

            foreach (var item in results.Skip(PageIndex * PageSize).Take(PageSize))
            {
                MovieItems.Add(item);
            }
            PageIndex++;
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

    public async Task LoadAnimeAsync(IBangumiClient bangumiClient, bool reset = false)
    {
        CategoryKind = "anime";
        MovieItems.Clear();

        if (reset)
        {
            AnimeItems.Clear();
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await bangumiClient.GetCalendarByWeekdayAsync(Weekday).ConfigureAwait(false);
            AnimeItems.Clear();
            foreach (var item in items)
            {
                AnimeItems.Add(item);
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
}
