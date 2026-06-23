using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Keyword-based content filter. Splits a comma-separated blocklist and
/// excludes any <see cref="SearchResult"/> whose title, description, source
/// name, or type name contains a blocked keyword (case-insensitive).
/// Mirrors <c>ContentFilterService.swift</c>.
/// </summary>
public sealed class ContentFilterService
{
    public IReadOnlyList<SearchResult> Filter(
        IEnumerable<SearchResult> results,
        string blockedKeywordsText)
    {
        var keywords = (blockedKeywordsText ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .Where(k => k.Length > 0)
            .ToList();

        if (keywords.Count == 0)
        {
            return results.ToList();
        }

        return results
            .Where(r => !IsBlocked(r, keywords))
            .ToList();
    }

    private static bool IsBlocked(SearchResult result, IReadOnlyList<string> keywords)
    {
        var haystack = string.Join(" ",
            result.Title ?? "",
            result.Description ?? "",
            result.SourceName ?? "",
            result.TypeName ?? "").ToLowerInvariant();

        return keywords.Any(k => haystack.Contains(k));
    }
}
