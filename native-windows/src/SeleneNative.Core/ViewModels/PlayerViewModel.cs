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
        if (!TryGetEpisodeUrl(source, CurrentEpisodeIndex, out var url)) return;
        ReplaceItem(url, source, CurrentEpisodeIndex);
        Play();
    }

    public void PlayEpisode(int index)
    {
        var source = CurrentSource ?? CurrentResult;
        if (source is null) return;
        if (!TryGetEpisodeUrl(source, index, out var url)) return;
        CurrentEpisodeIndex = index;
        ReplaceItem(url, source, index);
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
                var index = record.EpisodeNumber - 1;
                if (TryPlayResultAtIndex(detail, index, record))
                {
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
                var index = record.EpisodeNumber - 1;
                if (TryPlayResultAtIndex(matched, index, record))
                {
                    return;
                }
            }

            var titleMatched = FindSameTitleCandidate(results, record);
            if (titleMatched is not null)
            {
                CurrentSourceResults = [titleMatched];
                var index = record.EpisodeNumber - 1;
                if (TryPlayResultAtIndex(titleMatched, index, record))
                {
                    return;
                }
            }

            PlaybackError = "未找到匹配的视频源";
        }
        catch (Exception ex)
        {
            PlaybackError = ex.Message;
        }
    }

    private static SearchResult? FindSameTitleCandidate(IEnumerable<SearchResult> results, PlayRecord record)
    {
        var candidates = results
            .Where(result => result.Episodes.Count > 0 && TitleMatchesRecord(result.Title, record))
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates.FirstOrDefault(result =>
                SameText(result.Source, record.Source) ||
                SameText(result.SourceName, record.SourceName))
            ?? candidates[0];
    }

    private static bool TitleMatchesRecord(string title, PlayRecord record)
    {
        return SameText(title, record.SearchTitle) || SameText(title, record.Title);
    }

    private static bool SameText(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private bool TryPlayResultAtIndex(SearchResult result, int index, PlayRecord record)
    {
        if (!TryGetEpisodeUrl(result, index, out var url))
        {
            return false;
        }

        CurrentResult = result;
        CurrentSource = result;
        CurrentEpisodeIndex = index;
        ReplaceItem(url, result, index);
        if (record.PlayTime > 0)
        {
            PendingSeekTime = (int)record.PlayTime;
        }
        Play();
        return true;
    }

    private static bool TryGetEpisodeUrl(SearchResult result, int index, out string url)
    {
        url = string.Empty;
        if (index < 0 || index >= result.Episodes.Count)
        {
            return false;
        }

        url = result.Episodes[index];
        return !string.IsNullOrWhiteSpace(url);
    }

    public void Dispose()
    {
        _player.StateChanged -= OnStateChanged;
        _player.PositionChanged -= OnPositionChanged;
        _player.Error -= OnError;
        _player.Dispose();
    }
}
