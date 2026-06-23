using LibVLCSharp.Shared;
using SeleneNative.Core.Services;
using SeleneNative.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace SeleneNative.Views;

/// <summary>
/// In-app video surface. Constructs a <see cref="VideoView"/> element programmatically
/// to work around a WinUI XAML compiler issue where <c>LibVLCSharp.WinUI</c> isn't
/// discovered. Attaches it to the backing <see cref="IMediaPlayer"/> on property change
/// and detaches + pauses on <see cref="Unloaded"/>.
/// </summary>
public sealed partial class NativeVideoPlayerView : UserControl
{
    private LibVLCSharp.Platforms.Windows.VideoView? _videoView;
    private IReadOnlyList<string>? _swapChainOptions;
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
        if (_videoView is not null)
        {
            Attach();
            return;
        }

        _videoView = new LibVLCSharp.Platforms.Windows.VideoView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _videoView.Initialized += OnVideoViewInitialized;
        VideoHost.Children.Insert(0, _videoView);
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
        if (_videoView is null ||
            Player is null ||
            _swapChainOptions is null ||
            !_readySource.Task.IsCompleted)
        {
            return;
        }

        if (Player is LibVlcMediaPlayer libVlc)
        {
            libVlc.InitializeVideoView(_videoView, _swapChainOptions);
        }

        Player.StateChanged -= OnPlayerStateChanged;
        Player.StateChanged += OnPlayerStateChanged;
    }

    private void OnVideoViewInitialized(
        object? sender,
        LibVLCSharp.Platforms.Windows.InitializedEventArgs e)
    {
        _swapChainOptions = e.SwapChainOptions;
        _readySource.TrySetResult();
        Attach();
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
            if (Player is LibVlcMediaPlayer libVlc && _videoView is not null)
            {
                libVlc.DetachFromView(_videoView);
            }

            try { Player.Pause(); } catch { /* ignore */ }
        }
    }
}
