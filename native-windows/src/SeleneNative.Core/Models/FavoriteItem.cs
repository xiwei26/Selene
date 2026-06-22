using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class FavoriteItem
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("source_name")]
    public string SourceName { get; set; } = string.Empty;

    public string Year { get; set; } = string.Empty;

    public string Cover { get; set; } = string.Empty;

    [JsonPropertyName("total_episodes")]
    public int TotalEpisodes { get; set; }

    [JsonPropertyName("save_time")]
    public long SaveTime { get; set; }

    public static FavoriteItem FromJson(string key, JsonElement element)
    {
        var parts = ModelValueReaders.SplitKey(key);
        return new FavoriteItem
        {
            Id = key,
            Source = parts.Source,
            ItemId = parts.ItemId,
            Title = ModelValueReaders.ReadString(element, "title"),
            SourceName = ModelValueReaders.ReadString(element, "source_name", "sourceName"),
            Year = ModelValueReaders.ReadString(element, "year"),
            Cover = ModelValueReaders.ReadString(element, "cover", "poster"),
            TotalEpisodes = ModelValueReaders.ReadInt(element, "total_episodes", "totalEpisodes"),
            SaveTime = ModelValueReaders.ReadLong(element, "save_time", "saveTime")
                ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public Dictionary<string, object> ToJson()
    {
        return new Dictionary<string, object>
        {
            ["title"] = Title,
            ["source_name"] = SourceName,
            ["year"] = Year,
            ["cover"] = Cover,
            ["total_episodes"] = TotalEpisodes,
            ["save_time"] = SaveTime
        };
    }
}
