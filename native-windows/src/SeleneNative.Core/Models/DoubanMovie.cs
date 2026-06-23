using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SeleneNative.Core.Models;

[JsonConverter(typeof(DoubanMovieJsonConverter))]
public sealed class DoubanMovie
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Poster { get; init; } = string.Empty;
    public string? Rate { get; init; }
    public string Year { get; init; } = string.Empty;

    public static string ExtractYear(string value)
    {
        var match = Regex.Match(value, "\\d{4}");
        return match.Success ? match.Value : string.Empty;
    }
}

public sealed class DoubanResponse
{
    [JsonPropertyName("items")]
    public List<DoubanMovie> Items { get; set; } = [];
}

internal sealed class DoubanMovieJsonConverter : JsonConverter<DoubanMovie>
{
    public override DoubanMovie Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var poster = ReadDirectString(root, "poster");
        if (string.IsNullOrWhiteSpace(poster) && root.TryGetProperty("pic", out var pic))
        {
            poster = ReadDirectString(pic, "normal");
            if (string.IsNullOrWhiteSpace(poster))
            {
                poster = ReadDirectString(pic, "large");
            }

            if (string.IsNullOrWhiteSpace(poster))
            {
                poster = ReadDirectString(pic, "poster");
            }
        }

        var rate = ReadDirectString(root, "rate");
        if (string.IsNullOrWhiteSpace(rate) && root.TryGetProperty("rating", out var rating))
        {
            rate = ReadDirectString(rating, "value");
            if (string.IsNullOrWhiteSpace(rate))
            {
                rate = ReadDirectString(rating, "average");
            }
        }

        var year = ReadDirectString(root, "year");
        if (string.IsNullOrWhiteSpace(year))
        {
            year = DoubanMovie.ExtractYear(ReadDirectString(root, "card_subtitle"));
        }

        return new DoubanMovie
        {
            Id = ReadDirectString(root, "id"),
            Title = JsonModelHelpers.DecodeHtmlEntities(ReadDirectString(root, "title")),
            Poster = poster,
            Rate = string.IsNullOrWhiteSpace(rate) ? null : rate,
            Year = year
        };
    }

    public override void Write(Utf8JsonWriter writer, DoubanMovie value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("title", value.Title);
        writer.WriteString("poster", value.Poster);
        writer.WriteString("rate", value.Rate);
        writer.WriteString("year", value.Year);
        writer.WriteEndObject();
    }

    private static string ReadDirectString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.ReadString()
            : string.Empty;
    }
}
