namespace SeleneNative.Core.Services;

/// <summary>
/// Quality ranking helpers for M3U8 / HLS episode URLs. Models the same heuristic
/// the macOS client uses in <c>M3U8Service.swift</c> so cross-source candidates
/// can be sorted by likely quality when multiple episodes are returned.
/// </summary>
public static class M3U8Service
{
    public static int ResolutionRank(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return 0;
        }

        var lower = url.ToLowerInvariant();
        if (lower.Contains("2160") || lower.Contains("4k"))
        {
            return 4;
        }
        if (lower.Contains("1080"))
        {
            return 3;
        }
        if (lower.Contains("720"))
        {
            return 2;
        }
        if (lower.Contains("480"))
        {
            return 1;
        }
        return 0;
    }

    public static IReadOnlyList<string> SortedByLikelyQuality(IEnumerable<string> urls)
    {
        return urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .OrderByDescending(ResolutionRank)
            .ToList();
    }
}
