using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Player;

public sealed class PlayerShortcutsTests
{
    private static PlayerCommand Resolve(
        PlayerKey key,
        PlayerKeyModifiers modifiers = PlayerKeyModifiers.None,
        PlayerFocusKind focus = PlayerFocusKind.Video,
        bool isFullScreen = false,
        bool hasOpenPopup = false)
    {
        return PlayerShortcuts.Resolve(key, modifiers, focus, isFullScreen, hasOpenPopup);
    }

    [Theory]
    [InlineData(PlayerKey.Space, PlayerCommand.TogglePlayPause)]
    [InlineData(PlayerKey.Left, PlayerCommand.SeekBackward)]
    [InlineData(PlayerKey.Right, PlayerCommand.SeekForward)]
    [InlineData(PlayerKey.Up, PlayerCommand.VolumeUp)]
    [InlineData(PlayerKey.Down, PlayerCommand.VolumeDown)]
    [InlineData(PlayerKey.M, PlayerCommand.ToggleMute)]
    [InlineData(PlayerKey.F, PlayerCommand.ToggleFullScreen)]
    [InlineData(PlayerKey.F11, PlayerCommand.ToggleFullScreen)]
    public void VideoFocus_ShouldMapEveryDocumentedShortcut(PlayerKey key, PlayerCommand expected)
    {
        Assert.Equal(expected, Resolve(key));
        Assert.Equal(expected, Resolve(key, isFullScreen: true));
    }

    [Fact]
    public void ControlArrows_ShouldChangeEpisode_InsteadOfSeeking()
    {
        Assert.Equal(PlayerCommand.PreviousEpisode, Resolve(PlayerKey.Left, PlayerKeyModifiers.Control));
        Assert.Equal(PlayerCommand.NextEpisode, Resolve(PlayerKey.Right, PlayerKeyModifiers.Control));
    }

    [Fact]
    public void Escape_ShouldOnlyLeaveFullScreen_AndNeverCloseThePlayer()
    {
        Assert.Equal(PlayerCommand.None, Resolve(PlayerKey.Escape));
        Assert.Equal(PlayerCommand.ExitFullScreen, Resolve(PlayerKey.Escape, isFullScreen: true));
    }

    [Theory]
    [InlineData(PlayerKey.Space)]
    [InlineData(PlayerKey.Left)]
    [InlineData(PlayerKey.Right)]
    [InlineData(PlayerKey.Up)]
    [InlineData(PlayerKey.Down)]
    [InlineData(PlayerKey.M)]
    [InlineData(PlayerKey.F)]
    [InlineData(PlayerKey.F11)]
    [InlineData(PlayerKey.Escape)]
    public void OpenPopup_ShouldKeepEveryKey_SoEscapeClosesItFirst(PlayerKey key)
    {
        Assert.Equal(PlayerCommand.None, Resolve(key, isFullScreen: true, hasOpenPopup: true));
        Assert.Equal(PlayerCommand.None, Resolve(key, PlayerKeyModifiers.Control, isFullScreen: true, hasOpenPopup: true));
    }

    [Theory]
    [InlineData(PlayerFocusKind.TextInput)]
    [InlineData(PlayerFocusKind.Selector)]
    [InlineData(PlayerFocusKind.Outside)]
    public void EditingControls_ShouldKeepTypingAndNavigationKeys(PlayerFocusKind focus)
    {
        foreach (var key in new[] { PlayerKey.Space, PlayerKey.Left, PlayerKey.Right, PlayerKey.Up, PlayerKey.Down, PlayerKey.M, PlayerKey.F, PlayerKey.Enter })
        {
            Assert.Equal(PlayerCommand.None, Resolve(key, focus: focus));
            Assert.Equal(PlayerCommand.None, Resolve(key, PlayerKeyModifiers.Control, focus));
        }

        // Keys that no editing control uses still control the window.
        Assert.Equal(PlayerCommand.ToggleFullScreen, Resolve(PlayerKey.F11, focus: focus));
        Assert.Equal(PlayerCommand.ExitFullScreen, Resolve(PlayerKey.Escape, focus: focus, isFullScreen: true));
    }

    [Fact]
    public void Slider_ShouldKeepArrowKeys_ButNotTheOtherShortcuts()
    {
        foreach (var key in new[] { PlayerKey.Left, PlayerKey.Right, PlayerKey.Up, PlayerKey.Down })
        {
            Assert.Equal(PlayerCommand.None, Resolve(key, focus: PlayerFocusKind.Slider));
            Assert.Equal(PlayerCommand.None, Resolve(key, PlayerKeyModifiers.Control, PlayerFocusKind.Slider));
        }

        Assert.Equal(PlayerCommand.TogglePlayPause, Resolve(PlayerKey.Space, focus: PlayerFocusKind.Slider));
        Assert.Equal(PlayerCommand.ToggleMute, Resolve(PlayerKey.M, focus: PlayerFocusKind.Slider));
        Assert.Equal(PlayerCommand.ToggleFullScreen, Resolve(PlayerKey.F, focus: PlayerFocusKind.Slider));
    }

    [Fact]
    public void FocusedButton_ShouldBeActivatedBySpaceAndEnter_NotByTheShortcut()
    {
        Assert.Equal(PlayerCommand.None, Resolve(PlayerKey.Space, focus: PlayerFocusKind.Button));
        Assert.Equal(PlayerCommand.None, Resolve(PlayerKey.Enter, focus: PlayerFocusKind.Button));

        Assert.Equal(PlayerCommand.SeekForward, Resolve(PlayerKey.Right, focus: PlayerFocusKind.Button));
        Assert.Equal(PlayerCommand.VolumeDown, Resolve(PlayerKey.Down, focus: PlayerFocusKind.Button));
        Assert.Equal(PlayerCommand.ToggleMute, Resolve(PlayerKey.M, focus: PlayerFocusKind.Button));
        Assert.Equal(PlayerCommand.NextEpisode, Resolve(PlayerKey.Right, PlayerKeyModifiers.Control, PlayerFocusKind.Button));
    }

    [Fact]
    public void ToolbarCommand_ShouldKeepItsOwnArrowNavigation_SoEveryCommandStaysReachable()
    {
        foreach (var key in new[] { PlayerKey.Space, PlayerKey.Enter, PlayerKey.Left, PlayerKey.Right, PlayerKey.Up, PlayerKey.Down })
        {
            Assert.Equal(PlayerCommand.None, Resolve(key, focus: PlayerFocusKind.ToolbarCommand));
        }

        // Keys the toolbar does not use still work from it.
        Assert.Equal(PlayerCommand.ToggleMute, Resolve(PlayerKey.M, focus: PlayerFocusKind.ToolbarCommand));
        Assert.Equal(PlayerCommand.ToggleFullScreen, Resolve(PlayerKey.F, focus: PlayerFocusKind.ToolbarCommand));
        Assert.Equal(PlayerCommand.ToggleFullScreen, Resolve(PlayerKey.F11, focus: PlayerFocusKind.ToolbarCommand));
        Assert.Equal(PlayerCommand.ExitFullScreen, Resolve(PlayerKey.Escape, focus: PlayerFocusKind.ToolbarCommand, isFullScreen: true));
        Assert.Equal(PlayerCommand.PreviousEpisode, Resolve(PlayerKey.Left, PlayerKeyModifiers.Control, PlayerFocusKind.ToolbarCommand));
        Assert.Equal(PlayerCommand.NextEpisode, Resolve(PlayerKey.Right, PlayerKeyModifiers.Control, PlayerFocusKind.ToolbarCommand));
    }

    [Theory]
    [InlineData(PlayerKeyModifiers.Alt)]
    [InlineData(PlayerKeyModifiers.Windows)]
    [InlineData(PlayerKeyModifiers.Shift)]
    [InlineData(PlayerKeyModifiers.Control | PlayerKeyModifiers.Shift)]
    public void SystemModifiers_ShouldNeverTriggerAShortcut(PlayerKeyModifiers modifiers)
    {
        foreach (var key in Enum.GetValues<PlayerKey>())
        {
            Assert.Equal(PlayerCommand.None, Resolve(key, modifiers, isFullScreen: true));
        }
    }

    [Theory]
    [InlineData(PlayerKey.Space)]
    [InlineData(PlayerKey.Up)]
    [InlineData(PlayerKey.Down)]
    [InlineData(PlayerKey.M)]
    [InlineData(PlayerKey.F)]
    [InlineData(PlayerKey.F11)]
    [InlineData(PlayerKey.Escape)]
    [InlineData(PlayerKey.Enter)]
    public void Control_ShouldOnlyCombineWithLeftAndRight(PlayerKey key)
    {
        Assert.Equal(PlayerCommand.None, Resolve(key, PlayerKeyModifiers.Control, isFullScreen: true));
    }

    [Fact]
    public void EveryKeyPress_ShouldResolveToAtMostOneCommand_ForEveryFocus()
    {
        // The same press must never be both a shortcut and the focused control's own key.
        foreach (var focus in Enum.GetValues<PlayerFocusKind>())
        {
            var spaceCommand = Resolve(PlayerKey.Space, focus: focus);
            var spaceBelongsToControl = focus is PlayerFocusKind.Button or PlayerFocusKind.ToolbarCommand
                or PlayerFocusKind.TextInput or PlayerFocusKind.Selector or PlayerFocusKind.Outside;
            Assert.Equal(spaceBelongsToControl, spaceCommand == PlayerCommand.None);

            var arrowCommand = Resolve(PlayerKey.Right, focus: focus);
            var arrowBelongsToControl = focus is PlayerFocusKind.Slider or PlayerFocusKind.ToolbarCommand
                or PlayerFocusKind.TextInput or PlayerFocusKind.Selector or PlayerFocusKind.Outside;
            Assert.Equal(arrowBelongsToControl, arrowCommand == PlayerCommand.None);
        }
    }
}
