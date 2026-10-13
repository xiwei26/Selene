using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeleneNative.Core.Services;

public interface IPlayerSettingsStore
{
    /// <summary>Whether a finished episode starts the next one. On by default.</summary>
    bool AutoPlayNext { get; set; }
}

public sealed class PlayerSettingsStore : IPlayerSettingsStore
{
    private readonly string _settingsPath;
    private bool _autoPlayNext = true;

    public PlayerSettingsStore(string? localAppDataPath = null)
    {
        _settingsPath = Path.Combine(
            localAppDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeleneNative"),
            "player.json");
        Load();
    }

    public bool AutoPlayNext
    {
        get => _autoPlayNext;
        set
        {
            if (_autoPlayNext != value)
            {
                _autoPlayNext = value;
                Save();
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(_settingsPath));
            if (settings?.AutoPlayNext is bool autoPlayNext)
            {
                _autoPlayNext = autoPlayNext;
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new PlayerSettings { AutoPlayNext = _autoPlayNext }));
        }
        catch
        {
            // Best effort
        }
    }

    private sealed class PlayerSettings
    {
        [JsonPropertyName("autoPlayNext")]
        public bool? AutoPlayNext { get; set; }
    }
}
