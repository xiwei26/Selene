using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

[JsonConverter(typeof(PlayRecordJsonConverter))]
public sealed class PlayRecord
{
    public string Title { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string ItemId => ModelValueReaders.SplitKey(Id).ItemId;
    public string Cover { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public int EpisodeNumber { get; init; }
    public int TotalEpisodes { get; init; }
    public double PlayTime { get; init; }
    public double TotalTime { get; init; }
    public DateTimeOffset SaveTime { get; init; }
    public string SearchTitle { get; init; } = string.Empty;

    public double ProgressPercentage => TotalTime <= 0 ? 0 : Math.Clamp(PlayTime / TotalTime, 0, 1);

    public Dictionary<string, object> ToJson()
    {
        return new Dictionary<string, object>
        {
            ["title"] = Title,
            ["source_name"] = SourceName,
            ["year"] = Year,
            ["cover"] = Cover,
            ["index"] = EpisodeNumber,
            ["total_episodes"] = TotalEpisodes,
            ["play_time"] = PlayTime,
            ["total_time"] = TotalTime,
            ["save_time"] = SaveTime.ToUnixTimeMilliseconds(),
            ["search_title"] = SearchTitle
        };
    }
}

internal sealed class PlayRecordJsonConverter : JsonConverter<PlayRecord>
{
    public override PlayRecord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new PlayRecord
        {
            Title = ReadString(root, "title"),
            Source = ReadString(root, "source"),
            SourceName = ReadString(root, "source_name", "sourceName"),
            Id = ReadString(root, "id"),
            Cover = ReadString(root, "cover", "poster"),
            Year = ReadString(root, "year"),
            EpisodeNumber = ReadInt(root, "index"),
            TotalEpisodes = ReadInt(root, "total_episodes", "totalEpisodes"),
            PlayTime = ReadDouble(root, "play_time", "playTime"),
            TotalTime = ReadDouble(root, "total_time", "totalTime"),
            SaveTime = ReadDateTime(root, "save_time", "saveTime"),
            SearchTitle = ReadString(root, "search_title", "searchTitle")
        };
    }

    public override void Write(Utf8JsonWriter writer, PlayRecord value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("title", value.Title);
        writer.WriteString("source", value.Source);
        writer.WriteString("source_name", value.SourceName);
        writer.WriteString("id", value.Id);
        writer.WriteString("cover", value.Cover);
        writer.WriteString("year", value.Year);
        writer.WriteNumber("index", value.EpisodeNumber);
        writer.WriteNumber("total_episodes", value.TotalEpisodes);
        writer.WriteNumber("play_time", value.PlayTime);
        writer.WriteNumber("total_time", value.TotalTime);
        writer.WriteNumber("save_time", value.SaveTime.ToUnixTimeMilliseconds());
        writer.WriteString("search_title", value.SearchTitle);
        writer.WriteEndObject();
    }

    private static string ReadString(JsonElement element, params string[] propertyNames)
    {
        return ModelValueReaders.ReadString(element, propertyNames);
    }

    private static int ReadInt(JsonElement element, params string[] propertyNames)
    {
        return ModelValueReaders.ReadInt(element, propertyNames);
    }

    private static double ReadDouble(JsonElement element, params string[] propertyNames)
    {
        return ModelValueReaders.ReadDouble(element, propertyNames);
    }

    private static DateTimeOffset ReadDateTime(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var milliseconds))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var raw = property.GetString();
                if (DateTimeOffset.TryParse(raw, out var parsed))
                {
                    return parsed;
                }

                if (long.TryParse(raw, out var rawMilliseconds))
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(rawMilliseconds);
                }
            }
        }

        return DateTimeOffset.MinValue;
    }
}
