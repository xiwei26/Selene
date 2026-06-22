using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SeleneNative.Core.Services;

public interface IVersionService
{
    Task<VersionInfo?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default);
    void Dismiss(string version);
}

public sealed class VersionInfo
{
    public string Version { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
}

public sealed class VersionService : IVersionService
{
    private readonly HttpClient _httpClient;
    private readonly string? _endpoint;
    private readonly string _dismissedPath;

    public VersionService(HttpClient? httpClient = null, string? endpoint = null, string? localAppDataPath = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _endpoint = endpoint;
        _dismissedPath = Path.Combine(
            localAppDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeleneNative"),
            "dismissed_version.json");
    }

    public async Task<VersionInfo?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(_endpoint, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var info = JsonSerializer.Deserialize<VersionInfo>(json);
            if (info is null || !IsRemoteVersionNewer(info.Version, currentVersion))
            {
                return null;
            }

            var dismissed = await TryReadDismissedAsync(cancellationToken).ConfigureAwait(false);
            if (dismissed == info.Version) return null;

            return info;
        }
        catch
        {
            return null;
        }
    }

    public void Dismiss(string version)
    {
        File.WriteAllText(_dismissedPath, JsonSerializer.Serialize(new { version }));
    }

    public static bool IsRemoteVersionNewer(string remoteVersion, string currentVersion)
    {
        try
        {
            var remote = ParseVersion(remoteVersion);
            var current = ParseVersion(currentVersion);
            for (var i = 0; i < Math.Max(remote.Length, current.Length); i++)
            {
                var r = i < remote.Length ? remote[i] : 0;
                var c = i < current.Length ? current[i] : 0;
                if (r > c) return true;
                if (r < c) return false;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> TryReadDismissedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_dismissedPath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(_dismissedPath, cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static int[] ParseVersion(string version)
    {
        return (version ?? string.Empty).Split('.')
            .Select(segment => int.TryParse(new string(segment.Where(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToArray();
    }
}
