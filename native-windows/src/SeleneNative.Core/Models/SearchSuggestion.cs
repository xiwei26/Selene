using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class SearchSuggestion
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }
}
