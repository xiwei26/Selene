using System.Text.Json;

namespace SeleneNative.Core.Models;

internal static class JsonModelHelpers
{
    public static string ReadString(this JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var value) ? value.ToString() : element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    public static double ReadDouble(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    public static DateTimeOffset ReadDateTimeOffset(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(element.GetString(), out var dateTime))
        {
            return dateTime;
        }

        return DateTimeOffset.MinValue;
    }

    public static string DecodeHtmlEntities(string value)
    {
        return value
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal);
    }
}
