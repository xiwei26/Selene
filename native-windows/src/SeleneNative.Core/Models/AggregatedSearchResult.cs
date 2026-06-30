namespace SeleneNative.Core.Models;

/// <summary>
/// Aggregated search result that groups multiple <see cref="SearchResult"/> items
/// from different sources that represent the same content (matched by title, year,
/// and type). Mirrors <c>AggregatedSearchResult.swift</c>.
/// </summary>
public sealed class AggregatedSearchResult
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string? Poster { get; init; }
    public List<SearchResult> OriginalResults { get; init; } = [];
    public DateTimeOffset AddedTimestamp { get; init; } = DateTimeOffset.UtcNow;

    public static string BuildKey(SearchResult result)
    {
        return $"{result.Title ?? ""}|{result.Year ?? ""}|{result.TypeName ?? ""}";
    }

    public static IReadOnlyList<AggregatedSearchResult> RebuildAggregates(
        IEnumerable<SearchResult> results)
    {
        var groups = new Dictionary<string, AggregatedSearchResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var key = BuildKey(result);
            if (groups.TryGetValue(key, out var existing))
            {
                existing.OriginalResults.Add(result);
            }
            else
            {
                groups[key] = new AggregatedSearchResult
                {
                    Key = key,
                    Title = result.Title ?? string.Empty,
                    Year = result.Year ?? string.Empty,
                    TypeName = result.TypeName ?? string.Empty,
                    Poster = result.Poster,
                    OriginalResults = [result],
                    AddedTimestamp = DateTimeOffset.UtcNow,
                };
            }
        }

        return groups.Values
            .OrderBy(a => a.AddedTimestamp)
            .ToList();
    }
}
