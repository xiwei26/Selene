import AVKit
import Foundation
import Observation

@MainActor
@Observable
final class LiveStore {
    var sources: [LiveSource] = []
    var channels: [LiveChannel] = []
    var selectedSource: LiveSource?
    var selectedGroup: String?
    var selectedChannel: LiveChannel?
    var epg: EPGData?
    var phase: LoadPhase = .idle

    var groups: [String] {
        Array(Set(channels.map(\.group))).sorted()
    }

    var filteredChannels: [LiveChannel] {
        guard let selectedGroup else { return channels }
        return channels.filter { $0.group == selectedGroup }
    }

    func loadSources(
        isLocalMode: Bool,
        localSources: [LiveSource],
        client: LunaTVClient?
    ) async {
        phase = .loading
        do {
            if isLocalMode {
                sources = localSources.filter { !$0.disabled }
            } else if let client {
                sources = try await client.liveSources().filter { !$0.disabled }
            } else {
                sources = []
            }
            // Assigning selectedSource drives channel loading via the view task.
            selectedSource = selectedSource.flatMap { selected in
                sources.first { $0.id == selected.id }
            } ?? sources.first
            if selectedSource == nil {
                channels = []
            }
            phase = .loaded
        } catch {
            phase = isCancellation(error) ? .idle : .failed(error.localizedDescription)
        }
    }

    func loadChannels(
        source: LiveSource,
        isLocalMode: Bool,
        client: LunaTVClient?
    ) async {
        selectedSource = source
        selectedGroup = nil
        phase = .loading
        do {
            if isLocalMode {
                channels = try await client?.localLiveChannels(source: source) ?? []
            } else if let client {
                channels = try await client.liveChannels(source: source.key)
            } else {
                channels = []
            }
            phase = .loaded
        } catch {
            channels = []
            phase = isCancellation(error) ? .idle : .failed(error.localizedDescription)
        }
    }

    func loadEPG(
        channel: LiveChannel,
        isLocalMode: Bool,
        client: LunaTVClient?
    ) async {
        guard let source = selectedSource, !channel.tvgID.isEmpty, !isLocalMode else {
            epg = nil
            return
        }
        epg = try? await client?.liveEPG(tvgID: channel.tvgID, source: source.key)
    }
}

@MainActor
@Observable
final class PlayerCoordinator {
    var player: AVPlayer?
    var sourceResults: [SearchResult] = []
    var currentResult: SearchResult?
    var episodeIndex = 0
    var isEpisodeReversed = false
    var playTime = 0
    var totalTime = 0
    var errorMessage: String?
    /// Non-nil while a live channel is playing; live has no play record.
    var liveChannelName: String?
    /// Optional EPG snapshot shown in the unified playback UI for live.
    var liveEPG: EPGData?

    @ObservationIgnored private var statusObservation: NSKeyValueObservation?
    @ObservationIgnored private var timeObservation: Any?

    var orderedEpisodeIndices: [Int] {
        let values = Array(currentResult?.episodes.indices ?? 0..<0)
        return isEpisodeReversed ? values.reversed() : values
    }

    var isPlaying: Bool { player != nil }

    func open(result: SearchResult, episode: Int = 0, seekTo: Int = 0) {
        liveChannelName = nil
        liveEPG = nil
        sourceResults = sourceResults.isEmpty ? [result] : sourceResults
        currentResult = result
        playEpisode(episode, seekTo: seekTo)
    }

    /// Live playback shares this coordinator so only one AVPlayer is ever alive.
    /// Live streams carry no play record, so `liveChannelName` marks the mode.
    func openLive(channel: LiveChannel, epg: EPGData? = nil) {
        guard let url = URL(string: channel.url) else {
            errorMessage = "播放地址无效"
            return
        }
        stopPlayerOnly()
        activateAudioSession()
        sourceResults = []
        currentResult = nil
        episodeIndex = 0
        playTime = 0
        totalTime = 0
        errorMessage = nil
        liveChannelName = channel.name
        liveEPG = epg
        let item = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: item)
        self.player = player
        observe(player: player, item: item, seekTo: 0)
        player.play()
    }

    func updateLiveEPG(_ epg: EPGData?) {
        guard isLive else { return }
        liveEPG = epg
    }

    func playEpisode(_ index: Int, seekTo: Int = 0) {
        guard let result = currentResult,
              result.episodes.indices.contains(index),
              let url = URL(string: result.episodes[index]) else {
            errorMessage = "播放地址无效"
            return
        }
        stopPlayerOnly()
        activateAudioSession()
        liveChannelName = nil
        liveEPG = nil
        episodeIndex = index
        errorMessage = nil
        let item = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: item)
        self.player = player
        observe(player: player, item: item, seekTo: seekTo)
        player.play()
    }

    func switchSource(_ result: SearchResult) {
        let currentURL = currentResult?.episodes.indices.contains(episodeIndex) == true
            ? currentResult?.episodes[episodeIndex]
            : nil
        currentResult = result
        playEpisode(Self.episodeIndex(in: result, matching: currentURL, fallback: episodeIndex))
    }

    func resume(record: PlayRecord, client: LunaTVClient) async {
        do {
            let result = try await client.detail(source: record.source, id: record.itemID)
            sourceResults = [result]
            open(
                result: result,
                episode: Self.episodeIndex(
                    in: result,
                    matching: record.episodeURL,
                    fallback: record.episodeIndex
                ),
                seekTo: record.playTime
            )
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    /// Prefers the exact episode stream URL, falling back to the stored position
    /// when the backend no longer serves that URL.
    private static func episodeIndex(
        in result: SearchResult,
        matching url: String?,
        fallback: Int
    ) -> Int {
        if let url, !url.isEmpty, let match = result.episodes.firstIndex(of: url) {
            return match
        }
        return min(max(fallback, 0), max(result.episodes.count - 1, 0))
    }

    func makeRecord() -> PlayRecord? {
        guard !isLive, let result = currentResult else { return nil }
        return PlayRecord(
            id: "\(result.source)+\(result.id)",
            source: result.source,
            title: result.title,
            sourceName: result.sourceName,
            year: result.year,
            cover: result.poster,
            index: episodeIndex + 1,
            totalEpisodes: result.episodes.count,
            playTime: playTime,
            totalTime: totalTime,
            saveTime: Int64(Date().timeIntervalSince1970 * 1_000),
            searchTitle: result.title,
            remarks: result.remarks,
            doubanID: result.doubanID,
            type: result.typeName,
            episodeURL: result.episodes.indices.contains(episodeIndex)
                ? result.episodes[episodeIndex]
                : nil
        )
    }

    /// Snapshot progress for backgrounding without tearing the player down.
    func checkpointRecord() -> PlayRecord? {
        makeRecord()
    }

    /// Ends playback and returns the final VOD record (if any) so a single
    /// caller owns both teardown and persistence.
    @discardableResult
    func stopAndMakeRecord() -> PlayRecord? {
        let record = makeRecord()
        stop()
        return record
    }

    func stop() {
        stopPlayerOnly()
        sourceResults = []
        currentResult = nil
        episodeIndex = 0
        playTime = 0
        totalTime = 0
        errorMessage = nil
        liveChannelName = nil
        liveEPG = nil
    }

    /// Live streams have no resumable position, so no record is produced.
    var isLive: Bool { liveChannelName != nil }

    private func observe(player: AVPlayer, item: AVPlayerItem, seekTo: Int) {
        statusObservation = item.observe(\.status, options: [.new]) { [weak self] item, _ in
            Task { @MainActor in
                guard let self else { return }
                if item.status == .failed {
                    self.errorMessage = item.error?.localizedDescription ?? "播放失败"
                } else if item.status == .readyToPlay {
                    let duration = item.duration.seconds
                    self.totalTime = duration.isFinite ? max(Int(duration), 0) : 0
                    if seekTo > 0 {
                        player.seek(to: CMTime(seconds: Double(seekTo), preferredTimescale: 600))
                    }
                }
            }
        }
        timeObservation = player.addPeriodicTimeObserver(
            forInterval: CMTime(seconds: 5, preferredTimescale: 600),
            queue: .main
        ) { [weak self] time in
            Task { @MainActor in
                self?.playTime = time.seconds.isFinite ? max(Int(time.seconds), 0) : 0
            }
        }
    }

    private func stopPlayerOnly() {
        if let timeObservation, let player {
            player.removeTimeObserver(timeObservation)
        }
        timeObservation = nil
        statusObservation?.invalidate()
        statusObservation = nil
        player?.pause()
        // Drop the item so its network loading does not outlive the pause.
        player?.replaceCurrentItem(with: nil)
        player = nil
    }

    /// Plays audio when the ring/silent switch is silent and keeps the session
    /// eligible for background playback (see `UIBackgroundModes` = audio).
    private func activateAudioSession() {
        #if os(iOS)
        let session = AVAudioSession.sharedInstance()
        try? session.setCategory(.playback, mode: .moviePlayback)
        try? session.setActive(true)
        #endif
    }
}
