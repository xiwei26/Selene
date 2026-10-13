using SeleneNative.Core.Services;
using SeleneNative.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace SeleneNative.Views;

/// <summary>
/// In-app video surface. Hosts the Windows built-in player through a
/// <see cref="MediaPlayerElement"/> with the native transport controls, while
/// keeping the Core layer behind <see cref="IMediaPlayer"/>.
/// </summary>
public sealed partial class NativeVideoPlayerView : UserControl
{
    private MediaPlayerElement? _playerElement;
    private TaskCompletionSource _readySource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly DependencyProperty PlayerProperty = DependencyProperty.Register(
        nameof(Player),
        typeof(IMediaPlayer),
        typeof(NativeVideoPlayerView),
        new PropertyMetadata(null, OnPlayerChanged));

    public IMediaPlayer? Player
    {
        get => (IMediaPlayer?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>Raised for a tap on the picture itself, not on a transport control.</summary>
    public event EventHandler? VideoTapped;

    /// <summary>Raised for a double tap on the picture itself.</summary>
    public event EventHandler? VideoDoubleTapped;

    public NativeVideoPlayerView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
        VideoHost.AddHandler(TappedEvent, new TappedEventHandler(OnVideoTapped), true);
        VideoHost.AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(OnVideoDoubleTapped), true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_playerElement is not null)
        {
            Attach();
            return;
        }

        _playerElement = new MediaPlayerElement
        {
            AreTransportControlsEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Uniform,

            // The picture is dark in every app theme, so its overlay controls are too.
            RequestedTheme = ElementTheme.Dark,
        };
        ConfigureTransportControls(_playerElement.TransportControls);
        _playerElement.TransportControls.Loaded += (_, _) => HideUnsupportedNativeButtons();

        // Full screen belongs to the window, never to the element's own full-window mode.
        _playerElement.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, (_, _) =>
        {
            if (_playerElement.IsFullWindow)
            {
                _playerElement.IsFullWindow = false;
            }
        });
        VideoHost.Children.Insert(0, _playerElement);
        _readySource.TrySetResult();
        Attach();
    }

    public Task WaitUntilReadyAsync() => _readySource.Task;

    /// <summary>Enables the native seek bar only when the media can really be seeked.</summary>
    public void SetSeekEnabled(bool canSeek)
    {
        if (_playerElement?.TransportControls is { } controls && controls.IsSeekEnabled != canSeek)
        {
            controls.IsSeekEnabled = canSeek;
        }
    }

    /// <summary>Brings the auto-hidden native controls back, for example after a key press.</summary>
    public void ShowTransportControls() => _playerElement?.TransportControls?.Show();

    /// <summary>
    /// The native controls own play/pause, time, seeking, volume and mute. Commands
    /// with different semantics here (10-second steps, six speeds, window full
    /// screen, episodes) live on the page toolbar instead of being duplicated.
    /// </summary>
    private static void ConfigureTransportControls(MediaTransportControls controls)
    {
        controls.IsCompact = false;
        controls.ShowAndHideAutomatically = true;
        controls.IsSeekBarVisible = true;
        controls.IsSeekEnabled = false;
        controls.IsVolumeButtonVisible = true;
        controls.IsVolumeEnabled = true;

        // The WinUI 3 transport controls ship no full-window button and expose no
        // property for one, so there is nothing to hide: window full screen is the
        // only full-screen state.
        controls.IsZoomButtonVisible = false;
        controls.IsZoomEnabled = false;
        controls.IsPlaybackRateButtonVisible = false;
        controls.IsPlaybackRateEnabled = false;
        controls.IsSkipBackwardButtonVisible = false;
        controls.IsSkipForwardButtonVisible = false;
        controls.IsFastRewindButtonVisible = false;
        controls.IsFastForwardButtonVisible = false;
        controls.IsPreviousTrackButtonVisible = false;
        controls.IsNextTrackButtonVisible = false;
        controls.IsStopButtonVisible = false;
        controls.IsRepeatButtonVisible = false;
    }

    /// <summary>
    /// The default template shows a cast button that has no property to turn it
    /// off, and casting is not offered by this app. The transport controls manage
    /// each button's Visibility themselves as the width changes, so the button is
    /// taken out of view, input and the accessibility tree without touching it.
    /// </summary>
    private void HideUnsupportedNativeButtons()
    {
        if (_playerElement?.TransportControls is not { } controls ||
            FindDescendant(controls, "CastButton") is not Control castButton)
        {
            return;
        }

        castButton.Width = 0;
        castButton.MinWidth = 0;
        castButton.Opacity = 0;
        castButton.IsEnabled = false;
        castButton.IsTabStop = false;
        castButton.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(castButton, AccessibilityView.Raw);
    }

    private static FrameworkElement? FindDescendant(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element && element.Name == name)
            {
                return element;
            }

            if (FindDescendant(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void OnPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NativeVideoPlayerView view)
        {
            view.Attach();
        }
    }

    private void Attach()
    {
        if (_playerElement is null || Player is null)
        {
            return;
        }

        if (Player is WindowsMediaPlayer windowsMediaPlayer)
        {
            windowsMediaPlayer.AttachTo(_playerElement);
        }

        Player.StateChanged -= OnPlayerStateChanged;
        Player.StateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(object? sender, Core.Services.MediaStateChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LoadingRing.IsActive = e.State is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;
        });
    }

    private void OnVideoTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!IsOnTransportControl(e.OriginalSource))
        {
            VideoTapped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnVideoDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!IsOnTransportControl(e.OriginalSource))
        {
            e.Handled = true;
            VideoDoubleTapped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Whether a pointer gesture landed on a button, slider or the control panel of
    /// the native transport controls rather than on the picture.
    /// </summary>
    private bool IsOnTransportControl(object originalSource)
    {
        for (var node = originalSource as DependencyObject;
             node is not null && !ReferenceEquals(node, VideoHost);
             node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase or Slider or CommandBar ||
                node is FrameworkElement { Name: "ControlPanelGrid" })
            {
                return true;
            }
        }

        return false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Leaving the page is coordinated by the shell, which saves the record and
        // stops playback; here only the element is released.
        if (Player is not null)
        {
            Player.StateChanged -= OnPlayerStateChanged;
            if (Player is WindowsMediaPlayer windowsMediaPlayer && _playerElement is not null)
            {
                windowsMediaPlayer.DetachFrom(_playerElement);
            }
        }
    }
}
