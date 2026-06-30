using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class PlayerPage : UserControl
{
    private PlayerViewModel? _viewModel;
    private SearchResult? _seed;
    private bool _isSeeking;
    private bool _isSyncingSlider;

    public event EventHandler? CloseRequested;
    public event Func<PlayRecord, Task>? SaveRecordRequested;

    public PlayerPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        BackButton.Click += OnBackButtonClick;
        TogglePlayPauseButton.Click += OnTogglePlayPauseButtonClick;
        RetryButton.Click += OnRetryButtonClick;
        ToggleOrderButton.Click += OnToggleOrderButtonClick;
        SeekSlider.ValueChanged += OnSeekSliderValueChanged;
        SeekSlider.PointerPressed += OnSeekSliderPointerPressed;
        SeekSlider.PointerReleased += OnSeekSliderPointerReleased;
        SeekSlider.PointerCanceled += OnSeekSliderPointerReleased;
    }

    public void Bind(PlayerViewModel viewModel)
    {
        _viewModel = viewModel;
        VideoSurface.Player = viewModel.Player;
    }

    public async Task OpenAsync(SearchResult detail, string episodeTitle, string episodeUrl, int episodeNumber)
    {
        _seed = detail;
        TitleText.Text = detail.Title;
        SubtitleText.Text = episodeTitle;
        BuildSourceRow();
        BuildEpisodeRow();
        await WaitUntilVideoSurfaceReadyAsync();
        if (_viewModel is not null)
        {
            _viewModel.ReplaceItem(episodeUrl, detail, episodeNumber - 1);
            _viewModel.Play();
        }
    }

    public Task WaitUntilVideoSurfaceReadyAsync()
    {
        return VideoSurface.WaitUntilReadyAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        _ = PersistCurrentRecordAsync();
    }

    public async Task PersistCurrentRecordAsync()
    {
        if (_viewModel is null || SaveRecordRequested is null) return;
        var record = _viewModel.MakePlayRecord();
        if (record is not null)
        {
            await SaveRecordRequested(record);
        }
    }

    private void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRetryButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.Retry();
    }

    private void OnTogglePlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.TogglePlayPause();
        SyncState();
    }

    private void OnToggleOrderButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleEpisodeOrder();
        BuildEpisodeRow();
    }

    private void OnSeekSliderPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!SeekSlider.IsEnabled) return;
        _isSeeking = true;
    }

    private void OnSeekSliderPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isSeeking) return;
        _isSeeking = false;
        CommitSeek(SeekSlider.Value);
    }

    private void OnSeekSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingSlider) return;

        PlayTimeText.Text = Format(e.NewValue);
        if (!_isSeeking)
        {
            CommitSeek(e.NewValue);
        }
    }

    private void CommitSeek(double seconds)
    {
        if (_viewModel is null || _viewModel.TotalTime <= 0) return;
        _viewModel.SeekTo(seconds);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SyncState();
            if (e.PropertyName == nameof(PlayerViewModel.CurrentResult))
            {
                _seed = _viewModel?.CurrentResult;
                TitleText.Text = _seed?.Title ?? string.Empty;
                BuildSourceRow();
                BuildEpisodeRow();
            }
            if (e.PropertyName == nameof(PlayerViewModel.CurrentEpisodeIndex))
            {
                if (_viewModel is not null && _seed is not null)
                {
                    var index = _viewModel.CurrentEpisodeIndex;
                    var title = _seed.EpisodeTitles.Count > index && !string.IsNullOrWhiteSpace(_seed.EpisodeTitles[index])
                        ? _seed.EpisodeTitles[index]
                        : $"第{index + 1}集";
                    SubtitleText.Text = title;
                    BuildEpisodeRow();
                }
            }
        });
    }

    private void SyncState()
    {
        if (_viewModel is null) return;

        var total = Math.Max(_viewModel.TotalTime, 0);
        var playTime = Math.Clamp(_viewModel.PlayTime, 0, total > 0 ? total : double.MaxValue);
        var canSeek = total > 0;

        SeekSlider.IsEnabled = canSeek;
        SeekSlider.Maximum = canSeek ? total : 1;

        if (!_isSeeking)
        {
            _isSyncingSlider = true;
            SeekSlider.Value = canSeek ? playTime : 0;
            _isSyncingSlider = false;
            PlayTimeText.Text = Format(playTime);
        }

        TotalTimeText.Text = canSeek ? Format(total) : "--:--";
        TogglePlayPauseButton.Content = _viewModel.Player.State == MediaPlaybackState.Playing ? "暂停" : "继续";
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(_viewModel.PlaybackError);
        ErrorBar.Message = _viewModel.PlaybackError ?? string.Empty;
    }

    private void BuildSourceRow()
    {
        SourcesRow.Children.Clear();
        if (_seed is null) return;
        var button = new Button
        {
            Content = _seed.SourceName,
        };
        SourcesRow.Children.Add(button);
    }

    private void BuildEpisodeRow()
    {
        EpisodesRow.Children.Clear();
        if (_seed?.Episodes is null) return;
        var urls = _seed.Episodes;
        var titles = _seed.EpisodeTitles;
        var indices = _viewModel?.OrderedEpisodeIndices ?? Enumerable.Range(0, urls.Count).ToList();
        foreach (var index in indices)
        {
            if (index < 0 || index >= urls.Count) continue;
            var title = titles.Count > index && !string.IsNullOrWhiteSpace(titles[index])
                ? titles[index]
                : $"第{index + 1}集";
            var isActive = index == (_viewModel?.CurrentEpisodeIndex ?? 0);
            var button = new Button { Content = title, Tag = index };
            if (isActive)
            {
                button.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            }
            button.Click += (_, _) =>
            {
                if (button.Tag is int i)
                {
                    _viewModel?.PlayEpisode(i);
                    BuildEpisodeRow();
                }
            };
            EpisodesRow.Children.Add(button);
        }
    }

    private static string Format(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
