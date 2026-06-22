using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class SearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("poster")]
    public string Poster { get; set; } = string.Empty;

    [JsonPropertyName("episodes")]
    public List<string> Episodes { get; set; } = [];

    [JsonPropertyName("episodes_titles")]
    public List<string> EpisodeTitles { get; set; } = [];

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("source_name")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    [JsonPropertyName("year")]
    public string Year { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Description { get; set; }

    [JsonPropertyName("type_name")]
    public string? TypeName { get; set; }

    [JsonPropertyName("douban_id")]
    public int? DoubanId { get; set; }
}
