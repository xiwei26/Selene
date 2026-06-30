using CommunityToolkit.Mvvm.ComponentModel;

namespace SeleneNative.Core.Services;

public interface IThemeService
{
    ThemeMode Mode { get; set; }
    event Action<ThemeMode>? ModeChanged;
}

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

public sealed class ThemeService : ObservableObject, IThemeService
{
    private ThemeMode _mode = ThemeMode.System;
    private readonly string _settingsPath;

    public ThemeService(string? localAppDataPath = null)
    {
        _settingsPath = Path.Combine(
            localAppDataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeleneNative"),
            "theme.json");
        Load();
    }

    public event Action<ThemeMode>? ModeChanged;

    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                OnPropertyChanged();
                ModeChanged?.Invoke(value);
                Save();
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var text = File.ReadAllText(_settingsPath);
            if (Enum.TryParse<ThemeMode>(text.Trim('"'), out var parsed))
            {
                _mode = parsed;
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
            File.WriteAllText(_settingsPath, $"\"{_mode}\"");
        }
        catch
        {
            // Best effort
        }
    }
}
