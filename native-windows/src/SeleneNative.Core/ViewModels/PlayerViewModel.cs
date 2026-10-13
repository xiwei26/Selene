using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

/// <summary>
/// The single playback coordinator, mirroring <c>PlayerStore.swift</c> in the
/// macOS client. Owns the active <see cref="IMediaPlayer"/>, the current work and
/// episode, the resume position (<see cref="PendingSeekTime"/>) until the engine
/// can really seek, episode and source changes, auto-next, and the playback
/// record snapshots taken before an item is replaced or stopped.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    public const double SeekStepSeconds = 10;
    public const double VolumeStep = 0.05;

    // A seek that lands this close to the end is the user skipping to the end, not a natural finish.
    private const double EndSeekToleranceSeconds = 1;

    public static IReadOnlyList<double> PlaybackRates { get; } = [0.5, 0.75, 1, 1.25, 1.5, 2];

    private readonly IMediaPlayer _player;
    private readonly IPlayerSettingsStore? _settings;
    private bool _isInternalReset;
    private bool _isMediaOpened;
    private bool _playRequested;
    private bool _sawPlaying;
    private bool _endHandled;
    private bool _endReachedBySeek;
    private bool _rateSettled;
    private double _preferredRate = 1;
    private double _lastAudibleVolume = 1;
    private double _pendingSeekTotalTime;
    private int _requestGeneration;
    private int _workGeneration;
    private bool _candidatesSearched;
    private Task _pendingSaves = Task.CompletedTask;

    [ObservableProperty]
    private string? _currentEpisodeUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlayPrevious), nameof(CanPlayNext), nameof(CurrentEpisodeTitle), nameof(StatusText))]
    private SearchResult? _currentResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlayPrevious), nameof(CanPlayNext), nameof(CurrentEpisodeTitle), nameof(StatusText))]
    private int _currentEpisodeIndex;

    [ObservableProperty]
    private bool _isEpisodeReversed;

    [ObservableProperty]
    private double _playTime;

    [ObservableProperty]
    private double _totalTime;

    [ObservableProperty]
    private double? _pendingSeekTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _playbackError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isLoading;

    /// <summary>The engine's real state. Opened media is not reported as playing until it plays.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsPlaybackActive))]
    private MediaPlaybackState _playbackState;

    /// <summary>Whether the opened item can really be seeked. Never inferred from the duration alone.</summary>
    [ObservableProperty]
    private bool _canSeek;

    /// <summary>Volume from 0 to 1, kept even while muted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercent))]
    private double _volume = 1;

    [ObservableProperty]
    private bool _isMuted;

    /// <summary>The speed the engine is actually playing at.</summary>
    [ObservableProperty]
    private double _playbackRate = 1;

    [ObservableProperty]
    private bool _autoPlayNext = true;

    /// <summary>A non-fatal message, such as a source that lacks the current episode.</summary>
    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private IReadOnlyList<SearchResult> _sourceCandidates = [];

    [ObservableProperty]
    private bool _isLoadingSources;

    [ObservableProperty]
    private bool _isSwitchingSource;

    public IMediaPlayer Player => _player;

    /// <summary>
    /// Persists a record. Set by the host; the view model calls it with a snapshot
    /// taken before the played item changes.
    /// </summary>
    public Func<PlayRecord, Task>? RecordSaver { get; set; }

    /// <summary>Completes when every record handed to <see cref="RecordSaver"/> so far has been written.</summary>
    public Task PendingSaves => _pendingSaves;

    public int VolumePercent => (int)Math.Round(Volume * 100);

    public bool HasCurrentItem => !string.IsNullOrWhiteSpace(CurrentEpisodeUrl);

    /// <summary>True while playback is running or about to run, so the primary action is "pause".</summary>
    public bool IsPlaybackActive =>
        PlaybackState is MediaPlaybackState.Playing or MediaPlaybackState.Buffering ||
        (PlaybackState == MediaPlaybackState.Opening && _playRequested);

    public bool CanPlayPrevious => CurrentResult is not null && CurrentEpisodeIndex > 0;

    public bool CanPlayNext => CurrentResult is { } result && CurrentEpisodeIndex + 1 < result.Episodes.Count;

    public string CurrentEpisodeTitle =>
        CurrentResult is { } result ? SourceMatcher.EpisodeTitle(result, CurrentEpisodeIndex) : string.Empty;

    public string StatusText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PlaybackError) &&
                PlaybackState is not (MediaPlaybackState.Playing or MediaPlaybackState.Paused or MediaPlaybackState.Buffering))
            {
                return "播放出错";
            }

            return PlaybackState switch
            {
                MediaPlaybackState.Opening => "正在加载",
                MediaPlaybackState.Buffering => "正在缓冲",
                MediaPlaybackState.Playing => "正在播放",
                MediaPlaybackState.Paused => "已暂停",
                MediaPlaybackState.Ended => CanPlayNext ? "本集播放结束" : "播放完毕",
                MediaPlaybackState.Error => "播放出错",
                _ => IsLoading ? "正在加载" : HasCurrentItem ? "已停止" : "未播放",
            };
        }
    }

    public IReadOnlyList<int> OrderedEpisodeIndices
    {
        get
        {
            var count = CurrentResult?.Episodes.Count ?? 0;
            var indices = Enumerable.Range(0, count).ToList();
            return IsEpisodeReversed ? indices.AsEnumerable().Reverse().ToList() : indices;
        }
    }

    public PlayerViewModel(IMediaPlayer player, IPlayerSettingsStore? settings = null)
    {
        _player = player;
        _settings = settings;
        AutoPlayNext = settings?.AutoPlayNext ?? true;
        AttachPlayerEvents();
        SyncVolumeFromPlayer();
    }

    /// <summary>
    /// Constructor variant for tests: lets the ViewModel be constructed without
    /// resolving a real media engine.
    /// </summary>
    public PlayerViewModel(Func<IMediaPlayer> playerFactory, IPlayerSettingsStore? settings = null)
        : this(playerFactory(), settings)
    {
    }

    private void AttachPlayerEvents()
    {
        _player.StateChanged += OnStateChanged;
        _player.PositionChanged += OnPositionChanged;
        _player.Error += OnError;
        _player.MediaOpened += OnMediaOpened;
        _player.SeekabilityChanged += OnSeekabilityChanged;
        _player.SeekCompleted += OnSeekCompleted;
        _player.VolumeChanged += OnVolumeChanged;
        _player.PlaybackRateChanged += OnPlaybackRateChanged;
    }

    partial void OnAutoPlayNextChanged(bool value)
    {
        if (_settings is not null)
        {
            _settings.AutoPlayNext = value;
        }
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        PlaybackState = e.State;
        IsLoading = e.State is MediaPlaybackState.Opening or MediaPlaybackState.Buffering;

        switch (e.State)
        {
            case MediaPlaybackState.Playing:
                _playRequested = true;
                _sawPlaying = true;
                _endHandled = false;
                PlaybackError = null;
                SettlePlaybackRate();
                TryApplyPendingSeek();
                break;
            case MediaPlaybackState.Paused:
                // Native transport controls pause the engine without going through Pause().
                if (_isMediaOpened)
                {
                    _playRequested = false;
                }

                TryApplyPendingSeek();
                break;
            case MediaPlaybackState.Ended:
                OnPlaybackEnded();
                break;
            case MediaPlaybackState.Error:
                PlaybackError ??= "视频播放失败,请重试";
                break;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _isMediaOpened = true;
        RefreshSeekability();
        ApplyPreferredRate();
        TryApplyPendingSeek();
    }

    private void OnSeekabilityChanged(object? sender, EventArgs e)
    {
        RefreshSeekability();
        TryApplyPendingSeek();
    }

    private void OnSeekCompleted(object? sender, EventArgs e)
    {
        var length = _player.Length;
        _endReachedBySeek = length > 0 && _player.Position >= length - EndSeekToleranceSeconds;
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (_isInternalReset) return;
        RefreshSeekability();
        if (PendingSeekTime is not null)
        {
            // The duration may only become known now; the tick predates the seek it unlocks.
            TryApplyPendingSeek();
            if (PendingSeekTime is null) return;
        }

        PlayTime = e.Position;
    }

    private void OnError(object? sender, MediaErrorEventArgs e)
    {
        PlaybackError = e.Message;
    }

    private void OnVolumeChanged(object? sender, EventArgs e) => SyncVolumeFromPlayer();

    private void OnPlaybackRateChanged(object? sender, EventArgs e)
    {
        // Engines reset the rate while an item loads; the preferred rate is applied
        // again when it opens, so the reset is neither shown nor remembered.
        if (!_isMediaOpened)
        {
            return;
        }

        var rate = _player.PlaybackRate;
        PlaybackRate = rate;

        // Only a change made once the item plays at the preferred rate is a choice
        // to keep for later items.
        if (_rateSettled && rate > 0)
        {
            _preferredRate = rate;
        }
    }

    private void OnPlaybackEnded()
    {
        // Late or repeated end notifications must not skip an episode.
        if (!_isMediaOpened || !_sawPlaying || _endHandled)
        {
            return;
        }

        _endHandled = true;
        RefreshSeekability();
        if (TotalTime > 0)
        {
            PlayTime = TotalTime;
        }

        // Skipping to the end by hand finishes the episode without advancing.
        var reachedBySeek = _endReachedBySeek;
        _endReachedBySeek = false;
        if (reachedBySeek || !AutoPlayNext || !CanPlayNext)
        {
            return;
        }

        PlayEpisode(CurrentEpisodeIndex + 1);
    }

    private void RefreshSeekability()
    {
        var length = _player.Length;
        if (length > 0)
        {
            TotalTime = length;
        }

        CanSeek = _isMediaOpened && _player.CanSeek;
    }

    /// <summary>
    /// Applies the resume position once, and only when the engine can really seek
    /// and knows the duration. Until then the position stays pending.
    /// </summary>
    private void TryApplyPendingSeek()
    {
        if (PendingSeekTime is not double pending)
        {
            return;
        }

        RefreshSeekability();
        if (!CanSeek || TotalTime <= 0)
        {
            return;
        }

        PendingSeekTime = null;
        _pendingSeekTotalTime = 0;
        SeekTo(pending);
    }

    public void Play()
    {
        _playRequested = true;
        _player.Play();
        OnPropertyChanged(nameof(IsPlaybackActive));
    }

    public void Pause()
    {
        _playRequested = false;
        _player.Pause();
        OnPropertyChanged(nameof(IsPlaybackActive));
    }

    public void TogglePlayPause()
    {
        if (!HasCurrentItem)
        {
            return;
        }

        if (IsPlaybackActive)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>Seeks within the legal range. Returns false when the media cannot be seeked.</summary>
    public bool SeekTo(double seconds)
    {
        RefreshSeekability();
        if (!CanSeek)
        {
            return false;
        }

        var target = TotalTime > 0 ? Math.Clamp(seconds, 0, TotalTime) : Math.Max(0, seconds);
        _endReachedBySeek = TotalTime > 0 && target >= TotalTime - EndSeekToleranceSeconds;
        _player.Position = target;
        PlayTime = target;
        return true;
    }

    public bool SeekBy(double deltaSeconds) => SeekTo(_player.Position + deltaSeconds);

    public void SetVolume(double volume)
    {
        _player.Volume = Math.Clamp(volume, 0, 1);
        SyncVolumeFromPlayer();
    }

    /// <summary>Changes the volume by a step. Raising it while muted also unmutes.</summary>
    public void AdjustVolume(double delta)
    {
        if (delta > 0 && _player.IsMuted)
        {
            _player.IsMuted = false;
        }

        SetVolume(Math.Round(_player.Volume + delta, 2));
    }

    public void SetMuted(bool muted)
    {
        // Unmuting must be audible, and restores the volume the user had.
        if (!muted && _player.Volume <= 0)
        {
            _player.Volume = _lastAudibleVolume;
        }

        _player.IsMuted = muted;
        SyncVolumeFromPlayer();
    }

    public void ToggleMute() => SetMuted(!_player.IsMuted);

    private void SyncVolumeFromPlayer()
    {
        var volume = Math.Clamp(_player.Volume, 0, 1);
        if (volume > 0)
        {
            _lastAudibleVolume = volume;
        }

        Volume = volume;
        IsMuted = _player.IsMuted;
    }

    /// <summary>
    /// Why <paramref name="rate"/> cannot be selected right now, or null when it can.
    /// </summary>
    public string? PlaybackRateUnavailableReason(double rate)
    {
        if (!PlaybackRates.Contains(rate))
        {
            return "不支持的倍速";
        }

        if (!_isMediaOpened)
        {
            return null;
        }

        return _player.SupportsPlaybackRate(rate) ? null : "当前视频不支持该倍速";
    }

    /// <summary>
    /// Selects a speed that is kept for later episodes and sources of this session.
    /// Returns false when the rate is not offered or the current media cannot play it.
    /// </summary>
    public bool SetPlaybackRate(double rate)
    {
        if (PlaybackRateUnavailableReason(rate) is not null)
        {
            return false;
        }

        _preferredRate = rate;
        if (_isMediaOpened)
        {
            _player.PlaybackRate = rate;
            PlaybackRate = _player.PlaybackRate;
        }
        else
        {
            // Applied when the item opens.
            PlaybackRate = rate;
        }

        return true;
    }

    private void ApplyPreferredRate()
    {
        if (Math.Abs(_player.PlaybackRate - _preferredRate) > 0.001 && _player.SupportsPlaybackRate(_preferredRate))
        {
            _player.PlaybackRate = _preferredRate;
        }

        PlaybackRate = _player.PlaybackRate;
    }

    private void SettlePlaybackRate()
    {
        if (_rateSettled || !_isMediaOpened)
        {
            return;
        }

        ApplyPreferredRate();
        _rateSettled = true;
    }

    public void Stop()
    {
        BeginRequest();
        ++_workGeneration;
        _playRequested = false;
        _player.Stop();
        _isInternalReset = true;
        ResetItemState();
        CurrentEpisodeUrl = null;
        CurrentResult = null;
        SourceCandidates = [];
        _candidatesSearched = false;
        CurrentEpisodeIndex = 0;
        PlaybackError = null;
        IsLoading = false;
        IsLoadingSources = false;
        _isInternalReset = false;
        OnPropertyChanged(nameof(OrderedEpisodeIndices));
    }

    /// <summary>
    /// Leaves playback: snapshots the record while the engine state is still
    /// intact, stops, and completes once the record has been written.
    /// </summary>
    public Task CloseAsync()
    {
        SaveCurrentRecord();
        Stop();
        return _pendingSaves;
    }

    /// <summary>
    /// Starts a work at an episode. <paramref name="candidates"/> are other results
    /// that may be the same work; only the ones that really are become sources.
    /// </summary>
    public bool Open(
        SearchResult result,
        int episodeIndex,
        IEnumerable<SearchResult>? candidates = null,
        double resumeSeconds = 0)
    {
        if (!TryGetEpisodeUrl(result, episodeIndex, out var url))
        {
            Notice = $"「{result.Title}」的{SourceMatcher.EpisodeTitle(result, episodeIndex)}没有可播放的地址";
            return false;
        }

        SaveCurrentRecord();
        BeginRequest();
        StartWork(result, candidates);
        ReplaceItem(url, result, episodeIndex);
        if (resumeSeconds > 0)
        {
            PendingSeekTime = resumeSeconds;
        }

        Play();
        return true;
    }

    /// <summary>
    /// Loads <paramref name="url"/> into the engine as the current item. A new item
    /// never inherits the previous item's resume position, end state or error.
    /// </summary>
    public void ReplaceItem(string url, SearchResult? result = null, int index = 0)
    {
        _isInternalReset = true;
        ResetItemState();
        CurrentEpisodeUrl = url;
        PlaybackError = null;
        IsLoading = true;
        if (result is not null)
        {
            CurrentResult = result;
            CurrentEpisodeIndex = index;
            OnPropertyChanged(nameof(OrderedEpisodeIndices));
        }

        _player.Load(url);
        _isInternalReset = false;
    }

    private void ResetItemState()
    {
        _isMediaOpened = false;
        _sawPlaying = false;
        _endHandled = false;
        _endReachedBySeek = false;
        _rateSettled = false;
        _pendingSeekTotalTime = 0;
        PendingSeekTime = null;
        Notice = null;
        PlayTime = 0;
        TotalTime = 0;
        CanSeek = false;
    }

    private int BeginRequest()
    {
        IsSwitchingSource = false;
        return ++_requestGeneration;
    }

    private void StartWork(SearchResult result, IEnumerable<SearchResult>? candidates)
    {
        ++_workGeneration;
        IsLoadingSources = false;
        var pool = candidates?.ToList();
        SourceCandidates = SourceMatcher.BuildCandidates(result, pool);

        // A pool that came from a search already answers "which other sources exist".
        _candidatesSearched = pool is { Count: > 0 };
    }

    /// <summary>Plays an episode by its backend index. Display order never changes that index.</summary>
    public bool PlayEpisode(int index)
    {
        var result = CurrentResult;
        if (result is null || !TryGetEpisodeUrl(result, index, out var url))
        {
            return false;
        }

        SaveCurrentRecord();
        BeginRequest();
        ReplaceItem(url, result, index);
        Play();
        return true;
    }

    public bool PlayNextEpisode() => CanPlayNext && PlayEpisode(CurrentEpisodeIndex + 1);

    public bool PlayPreviousEpisode() => CanPlayPrevious && PlayEpisode(CurrentEpisodeIndex - 1);

    public void ToggleEpisodeOrder()
    {
        IsEpisodeReversed = !IsEpisodeReversed;
        OnPropertyChanged(nameof(OrderedEpisodeIndices));
    }

    /// <summary>Reloads the current episode and resumes where it was.</summary>
    public void Retry()
    {
        var url = CurrentEpisodeUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        // A finished episode restarts from the beginning.
        var resume = PlaybackState == MediaPlaybackState.Ended ? 0 : Math.Max(PlayTime, PendingSeekTime ?? 0);
        BeginRequest();
        ReplaceItem(url, CurrentResult, CurrentEpisodeIndex);
        if (resume > 0)
        {
            PendingSeekTime = resume;
        }

        Play();
    }

    /// <summary>
    /// Switches to another source of the same work at the matching episode, keeping
    /// the position and the paused state. Nothing is committed until the target is
    /// known to have that episode; on any failure the current source keeps playing.
    /// </summary>
    public async Task<bool> SwitchSourceAsync(
        SearchResult candidate,
        IContentProvider? provider,
        CancellationToken cancellationToken = default)
    {
        var origin = CurrentResult;
        if (origin is null)
        {
            return false;
        }

        if (SourceMatcher.IsSameSource(origin, candidate))
        {
            return true;
        }

        var generation = BeginRequest();
        var originIndex = CurrentEpisodeIndex;
        var sourceName = string.IsNullOrWhiteSpace(candidate.SourceName) ? candidate.Source : candidate.SourceName;
        Notice = null;
        IsSwitchingSource = true;
        try
        {
            var target = candidate;
            if (target.Episodes.Count == 0)
            {
                SearchResult? detail = null;
                try
                {
                    if (provider is not null)
                    {
                        detail = await provider.DetailAsync(candidate.Source, candidate.Id, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    if (generation == _requestGeneration)
                    {
                        Notice = $"加载来源「{sourceName}」失败，已保留当前来源：{ex.Message}";
                    }

                    return false;
                }

                if (generation != _requestGeneration)
                {
                    return false;
                }

                if (detail is null || detail.Episodes.Count == 0)
                {
                    Notice = $"来源「{sourceName}」没有可播放的集数，已保留当前来源";
                    return false;
                }

                target = detail;
            }

            if (SourceMatcher.MatchEpisodeIndex(origin, originIndex, target) is not int targetIndex ||
                !TryGetEpisodeUrl(target, targetIndex, out var url))
            {
                Notice = $"来源「{sourceName}」没有{SourceMatcher.EpisodeTitle(origin, originIndex)}，已保留当前来源";
                return false;
            }

            var position = Math.Max(PlayTime, PendingSeekTime ?? 0);
            var wasPaused = PlaybackState == MediaPlaybackState.Paused ||
                (PlaybackState == MediaPlaybackState.Opening && !_playRequested);

            SaveCurrentRecord();
            SourceCandidates = SourceCandidates
                .Select(item => SourceMatcher.IsSameSource(item, target) ? target : item)
                .ToList();
            // A paused viewer stays paused; the position is restored when the item opens.
            _playRequested = !wasPaused;
            ReplaceItem(url, target, targetIndex);
            if (position > 0)
            {
                PendingSeekTime = position;
            }

            if (!wasPaused)
            {
                Play();
            }

            return true;
        }
        finally
        {
            if (generation == _requestGeneration)
            {
                IsSwitchingSource = false;
            }
        }
    }

    /// <summary>
    /// Looks up other sources of the current work, once per work. Used when playback
    /// started without candidates, for example from a history record.
    /// </summary>
    public async Task LoadSourceCandidatesAsync(IContentProvider? provider, CancellationToken cancellationToken = default)
    {
        var current = CurrentResult;
        if (current is null || provider is null || _candidatesSearched || IsLoadingSources)
        {
            return;
        }

        var workGeneration = _workGeneration;
        IsLoadingSources = true;
        try
        {
            var results = await provider.SearchAsync(current.Title, cancellationToken);
            if (workGeneration != _workGeneration || CurrentResult is not { } latest)
            {
                return;
            }

            _candidatesSearched = true;
            var known = SourceCandidates;
            var merged = SourceMatcher.BuildCandidates(latest, known.Concat(results));

            // Keep the order the user has already seen; new sources are appended.
            SourceCandidates = known
                .Concat(merged.Where(item => !known.Any(existing => SourceMatcher.IsSameSource(existing, item))))
                .ToList();
        }
        catch (Exception ex)
        {
            if (workGeneration == _workGeneration)
            {
                Notice = $"查找其他来源失败：{ex.Message}";
            }
        }
        finally
        {
            if (workGeneration == _workGeneration)
            {
                IsLoadingSources = false;
            }
        }
    }

    public PlayRecord? MakePlayRecord()
    {
        if (CurrentResult is null || string.IsNullOrEmpty(CurrentEpisodeUrl))
        {
            return null;
        }

        // A resume position that has not been applied yet is still where the viewer is.
        var playTime = PendingSeekTime is double pending ? Math.Max(PlayTime, pending) : PlayTime;
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
            PlayTime = playTime,
            TotalTime = TotalTime > 0 ? TotalTime : _pendingSeekTotalTime,
            SaveTime = DateTimeOffset.UtcNow,
            SearchTitle = CurrentResult.Title,
        };
    }

    /// <summary>
    /// Snapshots the current item's record now and queues the write. The snapshot
    /// is taken synchronously, so later changes to the current item cannot leak
    /// into it, and writes keep the order in which they were taken.
    /// </summary>
    private void SaveCurrentRecord()
    {
        var saver = RecordSaver;
        if (saver is null)
        {
            return;
        }

        // An item that never opened and has no resume position was not watched.
        if (!_isMediaOpened && PendingSeekTime is null)
        {
            return;
        }

        if (MakePlayRecord() is { } record)
        {
            _pendingSaves = SaveInOrderAsync(_pendingSaves, saver, record);
        }
    }

    private static async Task SaveInOrderAsync(Task previous, Func<PlayRecord, Task> saver, PlayRecord record)
    {
        try
        {
            await previous;
        }
        catch
        {
            // An earlier failed write must not block this one.
        }

        try
        {
            await saver(record);
        }
        catch
        {
            // Persistence is best-effort and must never break playback.
        }
    }

    public async Task LoadDetailAndPlayAsync(PlayRecord record, IContentProvider provider)
    {
        SaveCurrentRecord();
        var generation = BeginRequest();
        ++_workGeneration;
        PlaybackError = null;
        IsLoading = true;
        try
        {
            var index = record.EpisodeNumber - 1;

            // Step 1: try exact detail
            var detail = await provider.DetailAsync(record.Source, record.ItemId);
            if (generation != _requestGeneration) return;
            if (detail is not null && TryPlayResultAtIndex(detail, index, record, null))
            {
                return;
            }

            // Step 2: search by title
            var query = string.IsNullOrWhiteSpace(record.SearchTitle) ? record.Title : record.SearchTitle;
            var results = await provider.SearchAsync(query);
            if (generation != _requestGeneration) return;
            var matched = results.FirstOrDefault(r =>
                r.Source == record.Source && r.Id == record.ItemId);
            if (matched is not null && TryPlayResultAtIndex(matched, index, record, results))
            {
                return;
            }

            var titleMatched = FindSameTitleCandidate(results, record);
            if (titleMatched is not null && TryPlayResultAtIndex(titleMatched, index, record, results))
            {
                return;
            }

            PlaybackError = "未找到匹配的视频源";
            IsLoading = false;
        }
        catch (Exception ex)
        {
            if (generation == _requestGeneration)
            {
                PlaybackError = ex.Message;
                IsLoading = false;
            }
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

    private bool TryPlayResultAtIndex(
        SearchResult result,
        int index,
        PlayRecord record,
        IEnumerable<SearchResult>? candidates)
    {
        if (!TryGetEpisodeUrl(result, index, out var url))
        {
            return false;
        }

        StartWork(result, candidates);
        ReplaceItem(url, result, index);
        if (record.PlayTime > 0)
        {
            PendingSeekTime = record.PlayTime;
            _pendingSeekTotalTime = record.TotalTime;
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
        _player.MediaOpened -= OnMediaOpened;
        _player.SeekabilityChanged -= OnSeekabilityChanged;
        _player.SeekCompleted -= OnSeekCompleted;
        _player.VolumeChanged -= OnVolumeChanged;
        _player.PlaybackRateChanged -= OnPlaybackRateChanged;
        _player.Dispose();
    }
}
