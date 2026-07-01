using SeleneNative.Core.Services;
using SeleneNative.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SeleneNative.Views;

/// <summary>
/// In-app video surface. Hosts the Windows built-in player through a
/// <see cref="MediaPlayerElement"/> while keeping the Core layer behind
/// <see cref="IMediaPlayer"/>.
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

    public NativeVideoPlayerView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
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
            AreTransportControlsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Uniform,
        };
        VideoHost.Children.Insert(0, _playerElement);
        _readySource.TrySetResult();
        Attach();
    }

    public Task WaitUntilReadyAsync() => _readySource.Task;

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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Player is not null)
        {
            Player.StateChanged -= OnPlayerStateChanged;
            if (Player is WindowsMediaPlayer windowsMediaPlayer && _playerElement is not null)
            {
                windowsMediaPlayer.DetachFrom(_playerElement);
            }

            try { Player.Pause(); } catch { /* ignore */ }
        }
    }
}
