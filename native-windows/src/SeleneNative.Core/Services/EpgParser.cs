using System.Globalization;
using System.Text.RegularExpressions;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Parses XMLTV-format EPG data into <see cref="EpgProgram"/> items.
/// Mirrors <c>LiveService.parseEPG</c> in the macOS client.
/// </summary>
public static class EpgParser
{
    public static EpgData? Parse(string content, string tvgId, string sourceKey, string epgUrl)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var programmes = new List<EpgProgram>();
        var programmeRegex = new Regex(
            @"<programme[^>]*channel=""([^""]*)""[^>]*start=""([^""]*)""[^>]*stop=""([^""]*)""[^>]*>(.*?)</programme>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in programmeRegex.Matches(content))
        {
            var channel = match.Groups[1].Value;
            if (!string.Equals(channel, tvgId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = ParseEpgDate(match.Groups[2].Value);
            var stop = ParseEpgDate(match.Groups[3].Value);
            var body = match.Groups[4].Value;
            var title = FirstTag(body, "title");
            var desc = FirstTag(body, "desc");

            programmes.Add(new EpgProgram
            {
                Title = title ?? string.Empty,
                Description = desc ?? string.Empty,
                StartTime = start,
                EndTime = stop,
            });
        }

        if (programmes.Count == 0)
        {
            return null;
        }

        return new EpgData
        {
            TvgId = tvgId,
            Source = sourceKey,
            EpgUrl = epgUrl,
            Programs = programmes.OrderBy(p => p.StartTime).ToList(),
        };
    }

    private static DateTimeOffset ParseEpgDate(string raw)
    {
        // "yyyyMMddHHmmss Z" or "yyyyMMddHHmmss"
        if (DateTimeOffset.TryParseExact(raw, "yyyyMMddHHmmss zzz",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        if (DateTimeOffset.TryParseExact(raw, "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
        {
            return result;
        }

        return DateTimeOffset.MinValue;
    }

    private static string? FirstTag(string body, string tagName)
    {
        var match = Regex.Match(body, $@"<{tagName}[^>]*>(.*?)</{tagName}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
