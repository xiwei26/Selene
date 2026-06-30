namespace SeleneNative.Core.Helpers;

public static class URLNormalizer
{
    private const string DefaultServerUrl = "http://localhost:8080";

    public static string NormalizeServerUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultServerUrl;
        }

        var normalized = url.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized}";
        }

        return normalized.TrimEnd('/');
    }

    public static string NormalizeImageUrl(string? url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var normalizedBaseUrl = NormalizeServerUrl(baseUrl);
        if (!normalizedBaseUrl.EndsWith('/'))
        {
            normalizedBaseUrl += "/";
        }

        return new Uri(new Uri(normalizedBaseUrl), url.TrimStart('/')).ToString();
    }

    public static string ExtractDomain(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }

    public static bool IsValidUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string AddQueryParameter(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
