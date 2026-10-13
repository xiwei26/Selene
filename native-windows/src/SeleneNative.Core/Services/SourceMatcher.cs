using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Decides which search results are alternative sources of the same work, and
/// which episode of another source corresponds to the one being played.
/// A shared keyword is never enough: remakes, namesakes and other seasons are
/// excluded rather than guessed.
/// </summary>
public static partial class SourceMatcher
{
    private enum MediaKind
    {
        Unknown,
        Movie,
        Series,
    }

    public static string SourceKey(SearchResult result) => $"{result.Source}+{result.Id}";

    public static bool IsSameSource(SearchResult left, SearchResult right)
    {
        return string.Equals(left.Source, right.Source, StringComparison.Ordinal) &&
            string.Equals(left.Id, right.Id, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <paramref name="current"/> followed by every other source in
    /// <paramref name="pool"/> that is the same work, de-duplicated by source + id.
    /// </summary>
    public static IReadOnlyList<SearchResult> BuildCandidates(SearchResult current, IEnumerable<SearchResult>? pool)
    {
        var candidates = new List<SearchResult> { current };
        if (pool is null)
        {
            return candidates;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { SourceKey(current) };
        foreach (var item in pool)
        {
            if (item is null ||
                string.IsNullOrWhiteSpace(item.Source) ||
                string.IsNullOrWhiteSpace(item.Id) ||
                !seen.Add(SourceKey(item)))
            {
                continue;
            }

            if (IsSameWork(current, item))
            {
                candidates.Add(item);
            }
        }

        return candidates;
    }

    public static bool IsSameWork(SearchResult reference, SearchResult candidate)
    {
        // Two valid Douban ids settle the question either way.
        if (reference.DoubanId is > 0 && candidate.DoubanId is > 0)
        {
            return reference.DoubanId == candidate.DoubanId;
        }

        // A known conflict excludes the candidate; the title is not consulted afterwards.
        if (TryParseYear(reference.Year, out var referenceYear) &&
            TryParseYear(candidate.Year, out var candidateYear) &&
            referenceYear != candidateYear)
        {
            return false;
        }

        var referenceKind = InferKind(reference);
        var candidateKind = InferKind(candidate);
        if (referenceKind != MediaKind.Unknown &&
            candidateKind != MediaKind.Unknown &&
            referenceKind != candidateKind)
        {
            return false;
        }

        var referenceTitle = NormalizeTitle(reference.Title);
        return referenceTitle.Length > 0 &&
            string.Equals(referenceTitle, NormalizeTitle(candidate.Title), StringComparison.Ordinal);
    }

    /// <summary>
    /// Folds case, width, spacing and punctuation, and rewrites season/part markers
    /// to one spelling so that "第二季" equals "第2季" but never equals "第三季" or no marker.
    /// </summary>
    public static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var text = title.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var markers = new List<string>();

        text = ChineseOrdinalRegex().Replace(text, match =>
        {
            if (!TryParseNumber(match.Groups["n"].Value, out var number))
            {
                return match.Value;
            }

            markers.Add($"{match.Groups["unit"].Value}{number}");
            return " ";
        });
        text = SeasonRegex().Replace(text, match =>
        {
            markers.Add($"季{int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture)}");
            return " ";
        });
        text = PartRegex().Replace(text, match =>
        {
            markers.Add($"部{int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture)}");
            return " ";
        });

        var builder = new StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        foreach (var marker in markers)
        {
            builder.Append('|').Append(marker);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Finds the episode in <paramref name="to"/> that corresponds to episode
    /// <paramref name="fromIndex"/> of <paramref name="from"/>. Episode names win;
    /// the bare index is used only when it is a plausible identity. Returns null
    /// when the target has no matching episode.
    /// </summary>
    public static int? MatchEpisodeIndex(SearchResult from, int fromIndex, SearchResult to)
    {
        if (fromIndex < 0 || fromIndex >= from.Episodes.Count || to.Episodes.Count == 0)
        {
            return null;
        }

        var fromLabel = NormalizeEpisodeLabel(ExplicitEpisodeTitle(from, fromIndex));
        if (fromLabel.Length > 0)
        {
            var sameName = MatchingIndices(to, index =>
                string.Equals(NormalizeEpisodeLabel(ExplicitEpisodeTitle(to, index)), fromLabel, StringComparison.Ordinal));
            if (PickPlayable(to, sameName, fromIndex) is int byName)
            {
                return byName;
            }
        }

        if (EpisodeNumber(from, fromIndex) is int number)
        {
            var sameNumber = MatchingIndices(to, index => EpisodeNumber(to, index) == number);
            if (PickPlayable(to, sameNumber, fromIndex) is int byNumber)
            {
                return byNumber;
            }

            // The episode is numbered and the target numbers its episodes too, yet
            // none carries this number: the target is missing the episode.
            if (HasNumberedEpisodes(to))
            {
                return null;
            }
        }

        // Names say nothing usable (for example "HD" against "正片"). Position is a
        // plausible identity only when both sources list the same number of episodes.
        return from.Episodes.Count == to.Episodes.Count && IsPlayable(to, fromIndex)
            ? fromIndex
            : null;
    }

    public static string EpisodeTitle(SearchResult result, int index)
    {
        var title = ExplicitEpisodeTitle(result, index);
        return string.IsNullOrWhiteSpace(title) ? $"第{index + 1}集" : title;
    }

    private static string? ExplicitEpisodeTitle(SearchResult result, int index)
    {
        return index >= 0 && index < result.EpisodeTitles.Count && !string.IsNullOrWhiteSpace(result.EpisodeTitles[index])
            ? result.EpisodeTitles[index]
            : null;
    }

    /// <summary>
    /// The episode number named by the title, or the 1-based position when the
    /// source gives the episode no title at all. Null for a non-numeric title.
    /// </summary>
    private static int? EpisodeNumber(SearchResult result, int index)
    {
        var title = ExplicitEpisodeTitle(result, index);
        if (title is null)
        {
            return index + 1;
        }

        var text = title.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var ordinal = EpisodeOrdinalRegex().Match(text);
        if (ordinal.Success && TryParseNumber(ordinal.Groups["n"].Value, out var ordinalNumber))
        {
            return ordinalNumber;
        }

        var coded = EpisodeCodeRegex().Match(text);
        if (coded.Success && int.TryParse(coded.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var codedNumber))
        {
            return codedNumber;
        }

        var leading = LeadingNumberRegex().Match(text);
        return leading.Success && int.TryParse(leading.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var leadingNumber)
            ? leadingNumber
            : null;
    }

    private static bool HasNumberedEpisodes(SearchResult result)
    {
        for (var index = 0; index < result.Episodes.Count; index++)
        {
            if (EpisodeNumber(result, index) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static List<int> MatchingIndices(SearchResult result, Func<int, bool> predicate)
    {
        var matches = new List<int>();
        for (var index = 0; index < result.Episodes.Count; index++)
        {
            if (predicate(index))
            {
                matches.Add(index);
            }
        }

        return matches;
    }

    private static int? PickPlayable(SearchResult result, List<int> matches, int preferredIndex)
    {
        var playable = matches.Where(index => IsPlayable(result, index)).ToList();
        return playable.Count switch
        {
            0 => null,
            1 => playable[0],
            _ => playable.Contains(preferredIndex) ? preferredIndex : null,
        };
    }

    private static bool IsPlayable(SearchResult result, int index)
    {
        return index >= 0 && index < result.Episodes.Count && !string.IsNullOrWhiteSpace(result.Episodes[index]);
    }

    private static string NormalizeEpisodeLabel(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        foreach (var ch in title.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool TryParseYear(string? value, out int year)
    {
        year = 0;
        var text = value?.Trim();
        return text is { Length: 4 } &&
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out year) &&
            year is >= 1900 and <= 2100;
    }

    private static MediaKind InferKind(SearchResult result)
    {
        var kind = InferKind(result.TypeName);
        return kind != MediaKind.Unknown ? kind : InferKind(result.ClassName);
    }

    private static MediaKind InferKind(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return MediaKind.Unknown;
        }

        var text = label.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();

        // Animation and documentaries are released both as films and as series.
        if (text.Contains("动画") || text.Contains("动漫") || text.Contains("纪录") || text.Contains("记录"))
        {
            return MediaKind.Unknown;
        }

        if (text.Contains("电影") || text.EndsWith('片') || text.Contains("movie") || text.Contains("film"))
        {
            return MediaKind.Movie;
        }

        // A bare genre such as "喜剧" also labels films, so only unambiguous series labels count.
        if (SeriesLabelRegex().IsMatch(text) || text.Contains("series") || text.Contains("drama") || text == "tv")
        {
            return MediaKind.Series;
        }

        return MediaKind.Unknown;
    }

    private static bool TryParseNumber(string text, out int number)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        // Chinese numerals up to 999, which covers seasons, parts and episodes.
        number = 0;
        var section = 0;
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '百':
                    number += (section == 0 ? 1 : section) * 100;
                    section = 0;
                    break;
                case '十':
                    number += (section == 0 ? 1 : section) * 10;
                    section = 0;
                    break;
                case '零' or '〇':
                    break;
                default:
                    var digit = "一二三四五六七八九".IndexOf(ch);
                    if (ch == '两')
                    {
                        digit = 1;
                    }

                    if (digit < 0)
                    {
                        number = 0;
                        return false;
                    }

                    section = digit + 1;
                    break;
            }
        }

        number += section;
        return number > 0;
    }

    [GeneratedRegex(@"第\s*(?<n>[0-9]+|[一二两三四五六七八九十百零〇]+)\s*(?<unit>季|部|期|辑|章|篇)")]
    private static partial Regex ChineseOrdinalRegex();

    [GeneratedRegex(@"(?:season\s*|(?<![a-z0-9])s)(?<n>[0-9]{1,3})(?![a-z0-9])")]
    private static partial Regex SeasonRegex();

    [GeneratedRegex(@"part\s*(?<n>[0-9]{1,3})(?![a-z0-9])")]
    private static partial Regex PartRegex();

    [GeneratedRegex(@"电视剧|连续剧|剧集|短剧|综艺|(?:国产|大陆|内地|香港|港台|台湾|日本|韩国|日韩|欧美|美国|英国|泰国|海外|港|台|日|韩|美|英|泰)剧")]
    private static partial Regex SeriesLabelRegex();

    [GeneratedRegex(@"第\s*(?<n>[0-9]+|[一二两三四五六七八九十百零〇]+)\s*[集话話期回章节節]")]
    private static partial Regex EpisodeOrdinalRegex();

    [GeneratedRegex(@"(?<![a-z])(?:ep|e)\s*(?<n>[0-9]{1,4})(?![0-9])")]
    private static partial Regex EpisodeCodeRegex();

    [GeneratedRegex(@"^\s*(?<n>[0-9]{1,8})(?:\s*[集话話期回])?(?![0-9a-z])")]
    private static partial Regex LeadingNumberRegex();
}
