using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

/// <summary>
/// State machine for in-app playback, mirroring <c>PlayerStore.swift</c> in the
/// macOS client. Owns the active <see cref="IMediaPlayer"/> and the resume
/// position (<see cref="PendingSeekTime"/>) until the engine reports ready.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IMediaPlayer _player;
    private readonly Func<IMediaPlayer> _playerFactory;
    private bool _isInternalReset;

    [ObservableProperty]
    private string? _currentEpisodeUrl;

    [ObservableProperty]
    private SearchResult? _currentResult;

    [ObservableProperty]
    private SearchResult? _currentSource;

    [ObservableProperty]
    private int _currentEpisodeIndex;

    [ObservableProperty]
    private bool _isEpisodeReversed;

    [ObservableProperty]
    private double _playTime;

    [ObservableProperty]
    private double _totalTime;

    [ObservableProperty]
    private int? _pendingSeekTime;

    [ObservableProperty]
    private string? _playbackError;

    [ObservableProperty]
    private bool _isLoading;

    public IReadOnlyList<SearchResult> CurrentSourceResults { get; private set; } = [];

    public IMediaPlayer Player => _player;

    public IReadOnlyList<int> OrderedEpisodeIndices
    {
        get
        {
            var count = CurrentResult?.Episodes.Count ?? 0;
            var indices = Enumerable.Range(0, count).ToList();
            return IsEpisodeReversed ? indices.AsEnumerable().Reverse().ToList() : indices;
        }
    }

    public PlayerViewModel(IMediaPlayer player)
    {
        _player = player;
        _playerFactory = () => player;
        AttachPlayerEvents();
    }

    /// <summary>
    /// Constructor variant for tests: lets the ViewModel be constructed without
    /// resolving a real media engine.
    /// </summary>
    public PlayerViewModel(Func<IMediaPlayer> playerFactory)
    {
        _playerFactory = playerFactory;
        _player = playerFactory();
        AttachPlayerEvents();
    }

    private void AttachPlayerEvents()
    {
        _player.StateChanged += OnStateChanged;
        _player.PositionChanged += OnPositionChanged;
        _player.Error += OnError;
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsLoading = e.State is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;

        if (e.State == MediaPlaybackState.Playing)
        {
            RefreshTotalTime();
            if (PendingSeekTime is int seek)
            {
                SeekTo(seek);
                PendingSeekTime = null;
            }
            PlaybackError = null;
        }
        else if (e.State == MediaPlaybackState.Error)
        {
            PlaybackError = "视频播放失败,请重试";
        }
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (_isInternalReset) return;
        RefreshTotalTime();
        PlayTime = e.Position;
    }

    private void OnError(object? sender, MediaErrorEventArgs e)
    {
        PlaybackError = e.Message;
    }

    private void RefreshTotalTime()
    {
        var length = _player.Length;
        if (length > 0)
        {
            TotalTime = length;
        }
    }

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void SeekTo(double seconds)
    {
        RefreshTotalTime();
        if (TotalTime <= 0) return;

        var target = Math.Clamp(seconds, 0, TotalTime);
        _player.Position = target;
        PlayTime = target;
    }

    public void Stop()
    {
        _player.Stop();
        _isInternalReset = true;
        CurrentEpisodeUrl = null;
        PlayTime = 0;
        TotalTime = 0;
        CurrentResult = null;
        CurrentSource = null;
        CurrentSourceResults = [];
        CurrentEpisodeIndex = 0;
        PendingSeekTime = null;
        PlaybackError = null;
        IsLoading = false;
        _isInternalReset = false;
    }

    public void ReplaceItem(string url, SearchResult? result = null, int index = 0)
    {
        _isInternalReset = true;
        CurrentEpisodeUrl = url;
        PlayTime = 0;
        TotalTime = 0;
        PlaybackError = null;
        IsLoading = true;
        _player.Load(url);
        _isInternalReset = false;
        if (result is not null)
        {
            CurrentResult = result;
            CurrentEpisodeIndex = index;
            OnPropertyChanged(nameof(OrderedEpisodeIndices));
        }
    }

    public void SwitchSource(SearchResult source)
    {
        if (CurrentResult is null) return;
        CurrentSource = source;
        var urls = M3U8Service.SortedByLikelyQuality(source.Episodes);
        if (CurrentEpisodeIndex < 0 || CurrentEpisodeIndex >= urls.Count) return;
        ReplaceItem(urls[CurrentEpisodeIndex], source, CurrentEpisodeIndex);
        Play();
    }

    public void PlayEpisode(int index)
    {
        var source = CurrentSource ?? CurrentResult;
        if (source is null) return;
        var urls = M3U8Service.SortedByLikelyQuality(source.Episodes);
        if (index < 0 || index >= urls.Count) return;
        CurrentEpisodeIndex = index;
        ReplaceItem(urls[index], source, index);
        Play();
    }

    public void ToggleEpisodeOrder()
    {
        IsEpisodeReversed = !IsEpisodeReversed;
        OnPropertyChanged(nameof(OrderedEpisodeIndices));
    }

    public void Retry()
    {
        if (string.IsNullOrWhiteSpace(CurrentEpisodeUrl)) return;
        _player.Load(CurrentEpisodeUrl);
        Play();
    }

    public PlayRecord? MakePlayRecord()
    {
        if (CurrentResult is null || string.IsNullOrEmpty(CurrentEpisodeUrl))
        {
            return null;
        }

        return new PlayRecord
        {
            Id = $"{CurrentResult.Source}+{CurrentResult.Id}",
            Source = CurrentResult.Source,
            Title = CurrentResult.Title,
            SourceName = CurrentResult.SourceName,
            Year = CurrentResult.Year,
            Cover = CurrentResult.Poster,
            EpisodeNumber = CurrentEpisodeIndex + 1,
            TotalEpisodes = CurrentResult.Episodes.Count,
            PlayTime = PlayTime,
            TotalTime = TotalTime,
            SaveTime = DateTimeOffset.UtcNow,
            SearchTitle = CurrentResult.Title,
        };
    }

    public async Task LoadDetailAndPlayAsync(PlayRecord record, IContentProvider provider)
    {
        try
        {
            // Step 1: try exact detail
            var detail = await provider.DetailAsync(record.Source, record.ItemId).ConfigureAwait(false);
            if (detail is not null)
            {
                CurrentSourceResults = [detail];
                var urls = M3U8Service.SortedByLikelyQuality(detail.Episodes);
                var index = record.EpisodeNumber - 1;
                if (index >= 0 && index < urls.Count)
                {
                    CurrentResult = detail;
                    CurrentSource = detail;
                    CurrentEpisodeIndex = index;
                    ReplaceItem(urls[index], detail, index);
                    if (record.PlayTime > 0)
                    {
                        PendingSeekTime = (int)record.PlayTime;
                    }
                    Play();
                    return;
                }
            }

            // Step 2: search by title
            var query = string.IsNullOrWhiteSpace(record.SearchTitle) ? record.Title : record.SearchTitle;
            var results = await provider.SearchAsync(query).ConfigureAwait(false);
            var matched = results.FirstOrDefault(r =>
                r.Source == record.Source && r.Id == record.ItemId);
            if (matched is not null)
            {
                CurrentSourceResults = [matched];
                var urls = M3U8Service.SortedByLikelyQuality(matched.Episodes);
                var index = record.EpisodeNumber - 1;
                if (index >= 0 && index < urls.Count)
                {
                    CurrentResult = matched;
                    CurrentSource = matched;
                    CurrentEpisodeIndex = index;
                    ReplaceItem(urls[index], matched, index);
                    if (record.PlayTime > 0)
                    {
                        PendingSeekTime = (int)record.PlayTime;
                    }
                    Play();
                    return;
                }
            }

            // Step 3: fallback to first search hit with episodes
            var fallback = results.FirstOrDefault(r => r.Episodes.Count > 0);
            if (fallback is not null)
            {
                CurrentSourceResults = [fallback];
                var urls = M3U8Service.SortedByLikelyQuality(fallback.Episodes);
                CurrentResult = fallback;
                CurrentSource = fallback;
                CurrentEpisodeIndex = 0;
                ReplaceItem(urls[0], fallback, 0);
                Play();
            }
            else
            {
                PlaybackError = "未找到可播放的源";
            }
        }
        catch (Exception ex)
        {
            PlaybackError = ex.Message;
        }
    }

    public void Dispose()
    {
        _player.StateChanged -= OnStateChanged;
        _player.PositionChanged -= OnPositionChanged;
        _player.Error -= OnError;
        _player.Dispose();
    }
}
