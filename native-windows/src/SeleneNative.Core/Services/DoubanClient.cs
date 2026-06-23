using System.Net.Http.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IDoubanClient
{
    Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default);
    Task<DoubanMovie?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default);
}

public sealed class DoubanClient : IDoubanClient
{
    private readonly HttpClient _httpClient;

    public DoubanClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://m.douban.com"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotMoviesAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("movie", cancellationToken);
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotTvShowsAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("tv", cancellationToken);
    }

    public Task<IReadOnlyList<DoubanMovie>> GetHotShowsAsync(CancellationToken cancellationToken = default)
    {
        return GetRecentHotAsync("show", cancellationToken);
    }

    private async Task<IReadOnlyList<DoubanMovie>> GetRecentHotAsync(
        string kind,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/rexxar/api/v2/subject/recent_hot/{kind}?start=0&limit=20&category={Uri.EscapeDataString("热门")}");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SeleneNative/1.0");
        request.Headers.Referrer = new Uri("https://movie.douban.com/");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<DoubanResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return data?.Items ?? [];
    }

    public async Task<DoubanMovie?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doubanId))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/rexxar/api/v2/subject/{Uri.EscapeDataString(doubanId)}");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SeleneNative/1.0");
        request.Headers.Referrer = new Uri("https://movie.douban.com/");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<DoubanMovie>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
