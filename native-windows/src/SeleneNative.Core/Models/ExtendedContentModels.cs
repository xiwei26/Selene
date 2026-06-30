using System.Text.Json;
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

    [JsonPropertyName("videos")]
    public List<VideoPlatformItem>? Videos
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

[JsonConverter(typeof(VideoPlatformItemJsonConverter))]
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

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("published_at")]
    public string? PublishedAtSnake
    {
        get => PublishedAt;
        set => PublishedAt = value;
    }

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

internal sealed class VideoPlatformItemJsonConverter : JsonConverter<VideoPlatformItem>
{
    public override VideoPlatformItem Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var snippet = GetObject(root, "snippet");

        var thumbnail = FirstNonBlank(
            GetString(root, "thumbnail"),
            GetString(root, "cover"),
            GetString(root, "pic"),
            GetString(root, "image"),
            snippet is { } snippetElement ? GetYouTubeThumbnail(snippetElement) : null);
        var cover = FirstNonBlank(
            GetString(root, "cover"),
            GetString(root, "thumbnail"),
            GetString(root, "pic"),
            GetString(root, "image"),
            thumbnail);

        return new VideoPlatformItem
        {
            Id = FirstNonBlank(
                GetString(root, "bvid"),
                GetYouTubeId(root),
                GetString(root, "id"),
                GetString(root, "aid")) ?? string.Empty,
            Title = FirstNonBlank(
                GetString(root, "title"),
                snippet is { } snippetElementForTitle ? GetString(snippetElementForTitle, "title") : null) ?? string.Empty,
            Thumbnail = thumbnail,
            Cover = cover,
            Description = FirstNonBlank(
                GetString(root, "description"),
                GetString(root, "desc"),
                snippet is { } snippetElementForDescription ? GetString(snippetElementForDescription, "description") : null),
            Author = FirstNonBlank(
                GetString(root, "author"),
                GetNestedString(root, "owner", "name"),
                GetNestedString(root, "channel", "title"),
                snippet is { } snippetElementForAuthor ? GetString(snippetElementForAuthor, "channelTitle") : null),
            PublishedAt = FirstNonBlank(
                GetString(root, "publishedAt"),
                GetString(root, "published_at"),
                GetString(root, "pubdate"),
                snippet is { } snippetElementForDate ? GetString(snippetElementForDate, "publishedAt") : null),
            PlayableUrl = FirstNonBlank(GetString(root, "playableUrl"), GetString(root, "playable_url")),
            ProxyUrl = FirstNonBlank(GetString(root, "proxyUrl"), GetString(root, "proxy_url")),
            Url = GetString(root, "url"),
            Views = FirstNonBlank(GetString(root, "views"), GetString(root, "play")),
            Duration = GetString(root, "duration")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        VideoPlatformItem value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("title", value.Title);
        WriteStringIfPresent(writer, "thumbnail", value.Thumbnail);
        WriteStringIfPresent(writer, "cover", value.Cover);
        WriteStringIfPresent(writer, "description", value.Description);
        WriteStringIfPresent(writer, "author", value.Author);
        WriteStringIfPresent(writer, "publishedAt", value.PublishedAt);
        WriteStringIfPresent(writer, "playableUrl", value.PlayableUrl);
        WriteStringIfPresent(writer, "proxyUrl", value.ProxyUrl);
        WriteStringIfPresent(writer, "url", value.Url);
        WriteStringIfPresent(writer, "views", value.Views);
        WriteStringIfPresent(writer, "duration", value.Duration);
        writer.WriteEndObject();
    }

    private static JsonElement? GetObject(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string? GetNestedString(JsonElement element, string objectName, string propertyName)
    {
        return GetObject(element, objectName) is { } nested
            ? GetString(nested, propertyName)
            : null;
    }

    private static string? GetYouTubeId(JsonElement element)
    {
        if (!TryGetProperty(element, "id", out var id))
        {
            return null;
        }

        if (id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        if (id.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return FirstNonBlank(
            GetString(id, "videoId"),
            GetString(id, "channelId"),
            GetString(id, "playlistId"),
            GetString(id, "kind"));
    }

    private static string? GetYouTubeThumbnail(JsonElement snippet)
    {
        if (!TryGetProperty(snippet, "thumbnails", out var thumbnails) ||
            thumbnails.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var size in new[] { "maxres", "standard", "high", "medium", "default" })
        {
            if (GetNestedString(thumbnails, size, "url") is { Length: > 0 } url)
            {
                return url;
            }
        }

        foreach (var thumbnail in thumbnails.EnumerateObject())
        {
            if (thumbnail.Value.ValueKind == JsonValueKind.Object &&
                GetString(thumbnail.Value, "url") is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void WriteStringIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }
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
