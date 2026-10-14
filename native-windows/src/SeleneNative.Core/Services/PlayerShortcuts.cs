namespace SeleneNative.Core.Services;

public enum PlayerKey
{
    Space,
    Enter,
    Left,
    Right,
    Up,
    Down,
    M,
    F,
    F11,
    Escape,
}

[Flags]
public enum PlayerKeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Windows = 8,
}

/// <summary>What currently owns keyboard focus, as far as shortcut routing cares.</summary>
public enum PlayerFocusKind
{
    /// <summary>The video surface, the page itself, or nothing.</summary>
    Video,

    /// <summary>A button-like control inside the player, activated by Space or Enter.</summary>
    Button,

    /// <summary>
    /// A command on a toolbar. Besides Space and Enter, the toolbar itself uses the
    /// arrow keys to move between its commands, which is how they are all reached.
    /// </summary>
    ToolbarCommand,

    Slider,

    /// <summary>A text box or other control that accepts typed characters.</summary>
    TextInput,

    /// <summary>A combo box or list that uses arrows and type-ahead.</summary>
    Selector,

    /// <summary>A control outside the player page, such as the navigation pane.</summary>
    Outside,
}

public enum PlayerCommand
{
    None,
    TogglePlayPause,
    SeekBackward,
    SeekForward,
    VolumeUp,
    VolumeDown,
    ToggleMute,
    ToggleFullScreen,
    ExitFullScreen,
    PreviousEpisode,
    NextEpisode,
}

/// <summary>
/// Maps a key press on the player page to at most one command. Returning
/// <see cref="PlayerCommand.None"/> leaves the key to the focused control, so a
/// press is never handled both by a shortcut and by the control that has focus.
/// </summary>
public static class PlayerShortcuts
{
    public static PlayerCommand Resolve(
        PlayerKey key,
        PlayerKeyModifiers modifiers,
        PlayerFocusKind focus,
        bool isFullScreen,
        bool hasOpenPopup)
    {
        // An open menu or flyout owns the keyboard; Escape closes it before anything else.
        if (hasOpenPopup)
        {
            return PlayerCommand.None;
        }

        if ((modifiers & (PlayerKeyModifiers.Alt | PlayerKeyModifiers.Windows | PlayerKeyModifiers.Shift)) != 0)
        {
            return PlayerCommand.None;
        }

        var control = (modifiers & PlayerKeyModifiers.Control) != 0;
        var command = key switch
        {
            PlayerKey.Space when !control => PlayerCommand.TogglePlayPause,
            PlayerKey.Left => control ? PlayerCommand.PreviousEpisode : PlayerCommand.SeekBackward,
            PlayerKey.Right => control ? PlayerCommand.NextEpisode : PlayerCommand.SeekForward,
            PlayerKey.Up when !control => PlayerCommand.VolumeUp,
            PlayerKey.Down when !control => PlayerCommand.VolumeDown,
            PlayerKey.M when !control => PlayerCommand.ToggleMute,
            PlayerKey.F when !control => PlayerCommand.ToggleFullScreen,
            PlayerKey.F11 when !control => PlayerCommand.ToggleFullScreen,
            // Escape leaves full screen; it never closes the player.
            PlayerKey.Escape when !control && isFullScreen => PlayerCommand.ExitFullScreen,
            _ => PlayerCommand.None,
        };

        if (command == PlayerCommand.None)
        {
            return PlayerCommand.None;
        }

        var windowKey = key is PlayerKey.F11 or PlayerKey.Escape;
        var arrowKey = key is PlayerKey.Left or PlayerKey.Right or PlayerKey.Up or PlayerKey.Down;
        return focus switch
        {
            PlayerFocusKind.Video => command,
            // Space and Enter activate the focused button instead.
            PlayerFocusKind.Button => key is PlayerKey.Space or PlayerKey.Enter ? PlayerCommand.None : command,
            // Plain arrows also stay with the toolbar; Ctrl+arrow is not a toolbar key.
            PlayerFocusKind.ToolbarCommand =>
                key is PlayerKey.Space or PlayerKey.Enter || (arrowKey && !control) ? PlayerCommand.None : command,
            // Arrows move the slider thumb.
            PlayerFocusKind.Slider => arrowKey ? PlayerCommand.None : command,
            // Typing, caret movement, selection changes and type-ahead stay with the control.
            PlayerFocusKind.TextInput or PlayerFocusKind.Selector or PlayerFocusKind.Outside =>
                windowKey ? command : PlayerCommand.None,
            _ => PlayerCommand.None,
        };
    }
}
