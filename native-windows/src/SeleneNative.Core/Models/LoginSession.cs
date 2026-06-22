using System.Text.Json.Serialization;

namespace SeleneNative.Core.Models;

public sealed class LoginSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ServerUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Cookie { get; set; } = string.Empty;

    public bool IsLocalMode { get; set; }

    [JsonIgnore]
    public bool IsLoggedIn => IsLocalMode || !string.IsNullOrWhiteSpace(Cookie);
}
