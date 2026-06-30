using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class ShortDramaCategory
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class ShortDramaItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title
    {
        get => string.IsNullOrWhiteSpace(Name) ? null : Name;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Name = value;
            }
        }
    }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("episode_count")]
    public int? EpisodeCount { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }
}

public sealed class ShortDramaListResult
{
    [JsonPropertyName("items")]
    public List<ShortDramaItem> Items { get; set; } = [];

    [JsonPropertyName("list")]
    public List<ShortDramaItem>? List
    {
        get => Items;
        set => Items = value ?? [];
    }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("has_more")]
    public bool? HasMore { get; set; }
}

public sealed class ShortDramaEpisode
{
    [JsonPropertyName("episode")]
    public int Episode { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class ShortDramaDetail
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("episodes")]
    public List<ShortDramaEpisode> Episodes { get; set; } = [];
}

public sealed class ShortDramaParseResult
{
    [JsonPropertyName("parsedUrl")]
    public string? ParsedUrl { get; set; }

    [JsonPropertyName("parsed_url")]
    public string? ParsedUrlSnake
    {
        get => ParsedUrl;
        set => ParsedUrl = value;
    }

    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; set; }

    [JsonPropertyName("proxy_url")]
    public string? ProxyUrlSnake
    {
        get => ProxyUrl;
        set => ProxyUrl = value;
    }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class VideoPlatformPage
{
    [JsonPropertyName("items")]
    public List<VideoPlatformItem> Items { get; set; } = [];

    [JsonPropertyName("list")]
    public List<VideoPlatformItem>? List
    {
        get => Items;
        set => Items = value ?? [];
    }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }

    [JsonPropertyName("next_page_token")]
    public string? NextPageTokenSnake
    {
        get => NextPageToken;
        set => NextPageToken = value;
    }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

public sealed class VideoPlatformItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("playableUrl")]
    public string? PlayableUrl { get; set; }

    [JsonPropertyName("playable_url")]
    public string? PlayableUrlSnake
    {
        get => PlayableUrl;
        set => PlayableUrl = value;
    }

    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; set; }

    [JsonPropertyName("proxy_url")]
    public string? ProxyUrlSnake
    {
        get => ProxyUrl;
        set => ProxyUrl = value;
    }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("views")]
    public string? Views { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public sealed class YouTubeRegion
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class TmdbBackdropResult
{
    [JsonPropertyName("backdropUrl")]
    public string? BackdropUrl { get; set; }

    [JsonPropertyName("backdrop_url")]
    public string? BackdropUrlSnake
    {
        get => BackdropUrl;
        set => BackdropUrl = value;
    }

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrlSnake
    {
        get => LogoUrl;
        set => LogoUrl = value;
    }
}

public sealed class TmdbActorResult
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("profileUrl")]
    public string? ProfileUrl { get; set; }

    [JsonPropertyName("profile_url")]
    public string? ProfileUrlSnake
    {
        get => ProfileUrl;
        set => ProfileUrl = value;
    }
}

public sealed class DoubanComment
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

public sealed class DoubanQuickInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("rating")]
    public string? Rating { get; set; }
}

public sealed class DoubanSuggestItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public sealed class DoubanCelebrityWork
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public string? Year { get; set; }
}

public sealed class TrailerRefreshResult
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("trailerUrl")]
    public string? TrailerUrl { get; set; }

    [JsonPropertyName("trailer_url")]
    public string? TrailerUrlSnake
    {
        get => TrailerUrl;
        set => TrailerUrl = value;
    }
}
