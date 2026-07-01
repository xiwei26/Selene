using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class AdminConfig
{
    [JsonPropertyName("YouTubeConfig")]
    public YouTubeAdminConfig? YouTubeConfig { get; set; }

    [JsonPropertyName("BilibiliConfig")]
    public BilibiliAdminConfig? BilibiliConfig { get; set; }

    [JsonPropertyName("ShortDramaConfig")]
    public ShortDramaAdminConfig? ShortDramaConfig { get; set; }

    [JsonPropertyName("SiteConfig")]
    public AdminSiteConfig? SiteConfig { get; set; }

    [JsonPropertyName("Role")]
    public string? Role { get; set; }
}

public sealed class YouTubeAdminConfig
{
    public static readonly string[] DefaultRegions = ["US", "CN", "JP", "KR", "GB", "DE", "FR"];
    public static readonly string[] DefaultCategories = ["Film & Animation", "Music", "Gaming", "News & Politics", "Entertainment"];

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("enableDemo")]
    public bool EnableDemo { get; set; } = true;

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 25;

    [JsonPropertyName("enabledRegions")]
    public List<string> EnabledRegions { get; set; } = [.. DefaultRegions];

    [JsonPropertyName("enabledCategories")]
    public List<string> EnabledCategories { get; set; } = [.. DefaultCategories];
}

public sealed class BilibiliAdminConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("loginStatus")]
    public string? LoginStatus { get; set; }

    [JsonPropertyName("loginTime")]
    [JsonIgnore]
    public long? LoginTimeUnix { get; set; }

    [JsonPropertyName("expireTime")]
    [JsonIgnore]
    public long? ExpireTimeUnix { get; set; }

    [JsonPropertyName("userInfo")]
    public BilibiliAdminUserInfo? UserInfo { get; set; }
}

public sealed class BilibiliAdminUserInfo
{
    [JsonPropertyName("mid")]
    public long Mid { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("face")]
    public string Face { get; set; } = string.Empty;

    [JsonPropertyName("isVip")]
    public bool IsVip { get; set; }
}

public sealed class ShortDramaAdminConfig
{
    [JsonPropertyName("primaryApiUrl")]
    public string PrimaryApiUrl { get; set; } = string.Empty;

    [JsonPropertyName("alternativeApiUrl")]
    public string AlternativeApiUrl { get; set; } = string.Empty;

    [JsonPropertyName("enableAlternative")]
    public bool EnableAlternative { get; set; }
}

public sealed class AdminSiteConfig
{
    [JsonPropertyName("SiteName")]
    public string SiteName { get; set; } = string.Empty;

    [JsonPropertyName("Announcement")]
    public string Announcement { get; set; } = string.Empty;
}
