using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Services;

public sealed class ThemeSettingsTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(
        Path.GetTempPath(), "SeleneNativeThemeTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(ThemeMode.System, "跟随系统")]
    [InlineData(ThemeMode.Light, "浅色")]
    [InlineData(ThemeMode.Dark, "深色")]
    public void Constructor_ShouldRestoreThePersistedThemeSelection(ThemeMode mode, string label)
    {
        var savedTheme = new ThemeService(_settingsDirectory);
        savedTheme.Mode = ThemeMode.Light;
        savedTheme.Mode = mode;

        var viewModel = new SettingsViewModel(themeService: new ThemeService(_settingsDirectory));

        Assert.Equal(label, viewModel.Theme);
    }

    [Theory]
    [InlineData("跟随系统", ThemeMode.System)]
    [InlineData("浅色", ThemeMode.Light)]
    [InlineData("深色", ThemeMode.Dark)]
    public async Task SavingSelection_ShouldPersistAndNotifyExactlyOnce(string label, ThemeMode mode)
    {
        var theme = new ThemeService(_settingsDirectory)
        {
            Mode = mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark,
        };
        var changes = new List<ThemeMode>();
        theme.ModeChanged += changes.Add;
        var viewModel = new SettingsViewModel(themeService: theme) { Theme = label };

        await viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(mode, theme.Mode);
        Assert.Equal(mode, new ThemeService(_settingsDirectory).Mode);
        Assert.Equal(mode, Assert.Single(changes));
        Assert.Null(viewModel.ErrorMessage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }
    }
}
