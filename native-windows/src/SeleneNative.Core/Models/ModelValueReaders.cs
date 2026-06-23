using System.Text.Json;

namespace SeleneNative.Core.Models;

internal static class ModelValueReaders
{
    public static (string Source, string ItemId) SplitKey(string key)
    {
        var separator = key.IndexOf('+', StringComparison.Ordinal);
        return separator < 0
            ? (string.Empty, key)
            : (key[..separator], key[(separator + 1)..]);
    }

    public static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    public static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (int.TryParse(ReadString(element, name), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    public static long? ReadLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (long.TryParse(ReadString(element, name), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    public static double ReadDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var doubleValue))
            {
                return doubleValue;
            }

            if (double.TryParse(ReadString(element, name), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
