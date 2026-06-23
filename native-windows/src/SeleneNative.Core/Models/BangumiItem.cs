using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class BangumiRating
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("count")]
    public Dictionary<string, int> Count { get; set; } = [];

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public sealed class BangumiImages
{
    [JsonPropertyName("large")]
    public string Large { get; set; } = string.Empty;

    [JsonPropertyName("common")]
    public string Common { get; set; } = string.Empty;

    [JsonPropertyName("medium")]
    public string Medium { get; set; } = string.Empty;

    [JsonPropertyName("small")]
    public string Small { get; set; } = string.Empty;

    [JsonPropertyName("grid")]
    public string Grid { get; set; } = string.Empty;

    public string BestImageUrl => new[] { Large, Common, Medium, Small, Grid }
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class BangumiCollection
{
    [JsonPropertyName("doing")]
    public int Doing { get; set; }

    [JsonPropertyName("on_hold")]
    public int OnHold { get; set; }

    [JsonPropertyName("dropped")]
    public int Dropped { get; set; }

    [JsonPropertyName("wish")]
    public int Wish { get; set; }

    [JsonPropertyName("collect")]
    public int Collect { get; set; }
}

public sealed class BangumiWeekday
{
    [JsonPropertyName("en")]
    public string En { get; set; } = string.Empty;

    [JsonPropertyName("cn")]
    public string Cn { get; set; } = string.Empty;

    [JsonPropertyName("ja")]
    public string Ja { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public sealed class BangumiItem
{
    private string _name = string.Empty;
    private string _nameCn = string.Empty;
    private string _summary = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => _name = JsonModelHelpers.DecodeHtmlEntities(value ?? string.Empty);
    }

    [JsonPropertyName("name_cn")]
    public string NameCn
    {
        get => _nameCn;
        set => _nameCn = JsonModelHelpers.DecodeHtmlEntities(value ?? string.Empty);
    }

    [JsonPropertyName("summary")]
    public string Summary
    {
        get => _summary;
        set => _summary = JsonModelHelpers.DecodeHtmlEntities(value ?? string.Empty);
    }

    [JsonPropertyName("air_date")]
    public string AirDate { get; set; } = string.Empty;

    [JsonPropertyName("air_weekday")]
    public int AirWeekday { get; set; }

    [JsonPropertyName("rating")]
    public BangumiRating Rating { get; set; } = new();

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("images")]
    public BangumiImages Images { get; set; } = new();

    [JsonPropertyName("collection")]
    public BangumiCollection Collection { get; set; } = new();

    public string DisplayTitle => !string.IsNullOrWhiteSpace(NameCn) ? NameCn : Name;
}

public sealed class BangumiCalendarResponse
{
    [JsonPropertyName("weekday")]
    public BangumiWeekday Weekday { get; set; } = new();

    [JsonPropertyName("items")]
    public List<BangumiItem> Items { get; set; } = [];
}
