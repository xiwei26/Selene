using System.Net.Http.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IBangumiClient
{
    Task<IReadOnlyList<BangumiItem>> GetTodayCalendarAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BangumiItem>> GetCalendarByWeekdayAsync(int weekday, CancellationToken cancellationToken = default);
}

public sealed class BangumiClient : IBangumiClient
{
    private readonly HttpClient _httpClient;

    public BangumiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://api.bgm.tv"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public Task<IReadOnlyList<BangumiItem>> GetTodayCalendarAsync(CancellationToken cancellationToken = default)
    {
        var weekday = DateTimeOffset.Now.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTimeOffset.Now.DayOfWeek;
        return GetCalendarByWeekdayAsync(weekday, cancellationToken);
    }

    public async Task<IReadOnlyList<BangumiItem>> GetCalendarByWeekdayAsync(
        int weekday,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/calendar");
        request.Headers.TryAddWithoutValidation("User-Agent", "senshinya/selene/1.0.0");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<List<BangumiCalendarResponse>>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return data?.FirstOrDefault(day => day.Weekday.Id == weekday)?.Items ?? [];
    }
}
