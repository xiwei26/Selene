using System.Net;

namespace SeleneNative.Core.Helpers;

/// <summary>
/// Builds a configured <see cref="HttpClient"/> for the Selene server API. The
/// returned client shares a single <see cref="HttpClientHandler"/> with cookie
/// support so the session cookie persists across requests for the lifetime of
/// the process.
/// </summary>
public static class NetworkHelper
{
    public static HttpClient CreateServerClient(string baseUrl, string cookie = "", TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = NormalizeBaseUri(baseUrl),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { Accept = { new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json") } },
        };
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        return client;
    }

    public static CookieContainer CreateCookieContainer()
    {
        return new CookieContainer();
    }

    private static Uri NormalizeBaseUri(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Server URL is required.", nameof(baseUrl));
        }

        return new Uri(trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/");
    }
}
