using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class LiveSource
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("ua")]
    public string UserAgent { get; set; } = string.Empty;

    [JsonPropertyName("epg")]
    public string Epg { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}

public sealed class LiveChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tvg_id")]
    public string TvgId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string Logo { get; set; } = string.Empty;

    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }
}

public sealed class EpgProgram
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("start_time")]
    public DateTimeOffset StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public DateTimeOffset EndTime { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class EpgData
{
    [JsonPropertyName("tvg_id")]
    public string TvgId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("epg_url")]
    public string EpgUrl { get; set; } = string.Empty;

    [JsonPropertyName("programs")]
    public List<EpgProgram> Programs { get; set; } = [];
}
