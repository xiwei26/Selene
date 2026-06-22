using System.Text.RegularExpressions;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Parses M3U playlist text into <see cref="LiveChannel"/> items.
/// Mirrors <c>LiveService.parseM3U</c> in the macOS client.
/// </summary>
public static class M3UParser
{
    public static IReadOnlyList<LiveChannel> Parse(string content, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var channels = new List<LiveChannel>();
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? pendingInfo = null;

        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    pendingInfo = line;
                }
                continue;
            }

            var url = line.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var name = "未知频道";
            var tvgId = string.Empty;
            var tvgLogo = string.Empty;
            var group = "未分组";

            if (pendingInfo is not null)
            {
                name = ExtractName(pendingInfo);
                tvgId = ExtractAttribute(pendingInfo, "tvg-id");
                tvgLogo = ExtractAttribute(pendingInfo, "tvg-logo");
                group = ExtractAttribute(pendingInfo, "group-title") ?? "未分组";
            }

            channels.Add(new LiveChannel
            {
                Id = $"{(string.IsNullOrWhiteSpace(tvgId) ? name : tvgId)}-{url}",
                Name = name,
                Url = url,
                TvgId = tvgId,
                Logo = tvgLogo,
                Group = group,
            });

            pendingInfo = null;
        }

        return channels;
    }

    private static string ExtractName(string extinf)
    {
        var parts = extinf.Split(',', 2);
        return parts.Length >= 2 ? parts[1].Trim() : "未知频道";
    }

    private static string? ExtractAttribute(string line, string attributeName)
    {
        var match = Regex.Match(line, $@"{Regex.Escape(attributeName)}=""([^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
