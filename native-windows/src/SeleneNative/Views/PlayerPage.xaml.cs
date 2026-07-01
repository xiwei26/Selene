using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class PlayerPage : UserControl
{
    private PlayerViewModel? _viewModel;
    private SearchResult? _seed;
    private IContentProvider? _provider;
    private PlayerMetadataViewModel? _metadata;
    private string? _metadataKey;
    private string? _loadingMetadataKey;
    private int _metadataRequestId;
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

    public void SetProvider(IContentProvider? provider)
    {
        if (!ReferenceEquals(_provider, provider))
        {
            _metadataKey = null;
            _loadingMetadataKey = null;
        }

        _provider = provider;
    }

    public async Task OpenAsync(
        SearchResult detail,
        string episodeTitle,
        string episodeUrl,
        int episodeNumber,
        IContentProvider? provider = null)
    {
        SetProvider(provider);
        _seed = detail;
        _metadata = null;
        _metadataKey = null;
        _loadingMetadataKey = null;
        TitleText.Text = detail.Title;
        SubtitleText.Text = episodeTitle;
        BuildSourceRow();
        BuildEpisodeRow();
        RenderPlayerMetadata();
        _ = LoadPlayerMetadataAsync(detail);
        await WaitUntilVideoSurfaceReadyAsync();
        if (_viewModel is not null)
        {
            _viewModel.ReplaceItem(episodeUrl, detail, episodeNumber - 1);
            _viewModel.Play();
        }
    }

    public Task LoadCurrentMetadataAsync()
    {
        var result = _viewModel?.CurrentResult ?? _seed;
        return result is null ? Task.CompletedTask : LoadPlayerMetadataAsync(result);
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
                RenderPlayerMetadata();
                _ = LoadCurrentMetadataAsync();
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

    private async Task LoadPlayerMetadataAsync(SearchResult result)
    {
        var key = BuildMetadataKey(result);
        if (string.Equals(_metadataKey, key, StringComparison.Ordinal) &&
            (_metadata is not null || string.Equals(_loadingMetadataKey, key, StringComparison.Ordinal)))
        {
            RenderPlayerMetadata();
            return;
        }

        _metadataKey = key;
        _loadingMetadataKey = key;
        var requestId = ++_metadataRequestId;
        var metadata = new PlayerMetadataViewModel();
        await metadata.LoadAsync(result, _provider);
        if (requestId != _metadataRequestId)
        {
            return;
        }

        _metadata = metadata;
        _loadingMetadataKey = null;
        DispatcherQueue.TryEnqueue(RenderPlayerMetadata);
    }

    private void RenderPlayerMetadata()
    {
        InfoPanel.Children.Clear();

        var result = _metadata?.Result ?? _seed ?? _viewModel?.CurrentResult;
        if (result is null)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "详情",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        AddChip(chips, result.SourceName);
        AddChip(chips, result.Year);
        AddChip(chips, result.TypeName);

        var tmdb = _metadata?.TmdbBackdrop;
        if (tmdb?.Rating is double tmdbRating)
        {
            AddChip(chips, $"TMDB {tmdbRating:0.0}", Color.FromArgb(255, 245, 197, 24));
        }

        if (tmdb?.NumberOfSeasons is int seasons and > 0)
        {
            AddChip(chips, $"共 {seasons} 季", Color.FromArgb(255, 18, 200, 102));
        }

        if (!string.IsNullOrWhiteSpace(_metadata?.QuickInfo?.Rating))
        {
            AddChip(chips, $"豆瓣 {_metadata!.QuickInfo!.Rating}", Color.FromArgb(255, 255, 215, 0));
        }

        if (chips.Children.Count > 0)
        {
            stack.Children.Add(chips);
        }

        AddBackdropPreview(stack, tmdb);

        var overview = _metadata?.Overview ?? result.Description;
        if (!string.IsNullOrWhiteSpace(overview))
        {
            stack.Children.Add(new TextBlock
            {
                Text = overview,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 1100,
                Foreground = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)),
            });
        }

        if (_metadata?.QuickInfo is DoubanQuickInfo quickInfo)
        {
            stack.Children.Add(CreateQuickInfoPanel(quickInfo));
        }

        if (_metadata?.Comments.Count > 0)
        {
            stack.Children.Add(CreateCommentsPanel(_metadata.Comments));
        }

        if (_metadata?.Recommendations.Count > 0)
        {
            stack.Children.Add(CreateRecommendationsPanel(_metadata.Recommendations));
        }

        if (_provider is not null && _metadata is null)
        {
            stack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new ProgressRing { Width = 16, Height = 16, IsActive = true },
                    new TextBlock
                    {
                        Text = "正在加载更多详情...",
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    }
                }
            });
        }

        InfoPanel.Children.Add(stack);
    }

    private static void AddBackdropPreview(StackPanel stack, TmdbBackdrop? tmdb)
    {
        var imageUrl = tmdb?.Logo ?? tmdb?.Poster ?? tmdb?.Backdrop;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        stack.Children.Add(new Image
        {
            Source = new BitmapImage(uri),
            Stretch = Stretch.Uniform,
            MaxHeight = 92,
            MaxWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
    }

    private static UIElement CreateQuickInfoPanel(DoubanQuickInfo quickInfo)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣详情",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        AddTextLine(stack, JoinLine("类型", quickInfo.Genres));
        AddTextLine(stack, JoinLine("导演", quickInfo.Directors));
        AddTextLine(stack, JoinLine("主演", quickInfo.Cast));
        AddTextLine(stack, string.IsNullOrWhiteSpace(quickInfo.Summary) ? null : quickInfo.Summary);
        return stack;
    }

    private static UIElement CreateCommentsPanel(IEnumerable<DoubanComment> comments)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "豆瓣短评",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        foreach (var comment in comments.Take(3))
        {
            AddTextLine(
                stack,
                string.IsNullOrWhiteSpace(comment.Author)
                    ? comment.Content
                    : $"{comment.Author}: {comment.Content}");
        }

        return stack;
    }

    private static UIElement CreateRecommendationsPanel(IEnumerable<DoubanRecommendation> recommendations)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "相关推荐",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var item in recommendations.Take(8))
        {
            row.Children.Add(CreateChip(
                string.IsNullOrWhiteSpace(item.Rating) ? item.Title : $"{item.Title} {item.Rating}",
                Color.FromArgb(235, 255, 255, 255)));
        }

        stack.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = row,
        });
        return stack;
    }

    private static void AddTextLine(StackPanel stack, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        stack.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1100,
            Foreground = new SolidColorBrush(Color.FromArgb(215, 255, 255, 255)),
        });
    }

    private static void AddChip(StackPanel chips, string? text, Color? foreground = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        chips.Children.Add(CreateChip(text, foreground ?? Color.FromArgb(235, 255, 255, 255)));
    }

    private static Border CreateChip(string text, Color foreground)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = new SolidColorBrush(foreground),
            },
        };
    }

    private static string JoinLine(string label, IReadOnlyList<string> values)
    {
        return values.Count > 0 ? $"{label}: {string.Join("、", values)}" : string.Empty;
    }

    private static string BuildMetadataKey(SearchResult result)
    {
        return $"{result.Source}|{result.Id}|{result.Title}|{result.Year}|{result.DoubanId}";
    }

    private static string Format(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
