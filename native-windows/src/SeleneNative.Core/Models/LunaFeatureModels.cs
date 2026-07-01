using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class MediaPlatformItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Cover { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class TmdbBackdrop
{
    [JsonPropertyName("backdrop")]
    public string? Backdrop { get; init; }

    [JsonPropertyName("poster")]
    public string? Poster { get; init; }

    [JsonPropertyName("logo")]
    public string? Logo { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("rating")]
    public double? Rating { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("numberOfSeasons")]
    public int? NumberOfSeasons { get; init; }
}

public sealed class DoubanQuickInfo
{
    public string Title { get; init; } = string.Empty;
    public string? Year { get; init; }
    public string? Rating { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public IReadOnlyList<string> Directors { get; init; } = [];
    public IReadOnlyList<string> Cast { get; init; } = [];
}

public sealed class DoubanComment
{
    public string Author { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Rating { get; init; } = string.Empty;
}

public sealed class DoubanRecommendation
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Cover { get; init; } = string.Empty;
    public string Rating { get; init; } = string.Empty;
}
