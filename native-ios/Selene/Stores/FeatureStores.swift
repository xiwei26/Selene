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

    func loadSources(appModel: AppModel) async {
        phase = .loading
        do {
            if appModel.session?.isLocalMode == true {
                sources = appModel.session?.localLiveSources.filter { !$0.disabled } ?? []
            } else if let client = appModel.client {
                sources = try await client.liveSources().filter { !$0.disabled }
            }
            selectedSource = selectedSource.flatMap { selected in
                sources.first { $0.id == selected.id }
            } ?? sources.first
            if let selectedSource {
                await loadChannels(source: selectedSource, appModel: appModel)
            } else {
                channels = []
                phase = .loaded
            }
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }

    func loadChannels(source: LiveSource, appModel: AppModel) async {
        selectedSource = source
        selectedGroup = nil
        phase = .loading
        do {
            if appModel.session?.isLocalMode == true {
                channels = try await LocalLiveService.channels(source: source)
            } else if let client = appModel.client {
                channels = try await client.liveChannels(source: source.key)
            }
            phase = .loaded
        } catch {
            channels = []
            phase = .failed(error.localizedDescription)
        }
    }

    func loadEPG(channel: LiveChannel, appModel: AppModel) async {
        guard let source = selectedSource, !channel.tvgID.isEmpty, appModel.session?.isLocalMode != true else {
            epg = nil
            return
        }
        epg = try? await appModel.client?.liveEPG(tvgID: channel.tvgID, source: source.key)
    }
}

enum LocalLiveService {
    static func channels(source: LiveSource) async throws -> [LiveChannel] {
        guard let url = URL(string: source.url) else { throw APIError.invalidServerURL }
        var request = URLRequest(url: url, timeoutInterval: 25)
        if !source.ua.isEmpty {
            request.setValue(source.ua, forHTTPHeaderField: "User-Agent")
        }
        let (data, response) = try await URLSession.shared.data(for: request)
        guard let response = response as? HTTPURLResponse, (200...299).contains(response.statusCode) else {
            throw APIError.invalidResponse
        }
        guard let text = String(data: data, encoding: .utf8) else {
            throw APIError.decodingFailed
        }
        return parseM3U(text)
    }

    private static func parseM3U(_ text: String) -> [LiveChannel] {
        var channels: [LiveChannel] = []
        var pendingMetadata: String?

        for rawLine in text.components(separatedBy: .newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.hasPrefix("#EXTINF") {
                pendingMetadata = line
            } else if !line.isEmpty, !line.hasPrefix("#"), let metadata = pendingMetadata {
                let name = metadata.split(separator: ",", maxSplits: 1).last.map(String.init) ?? "未知频道"
                let tvgID = attribute("tvg-id", in: metadata)
                let logo = attribute("tvg-logo", in: metadata)
                let group = attribute("group-title", in: metadata).nonEmpty ?? "未分组"
                channels.append(
                    LiveChannel(
                        id: "\(tvgID)-\(line)",
                        tvgID: tvgID,
                        name: name,
                        logo: logo,
                        group: group,
                        url: line
                    )
                )
                pendingMetadata = nil
            }
        }
        return channels
    }

    private static func attribute(_ key: String, in text: String) -> String {
        guard let range = text.range(of: "\(key)=\"") else { return "" }
        let suffix = text[range.upperBound...]
        guard let end = suffix.firstIndex(of: "\"") else { return "" }
        return String(suffix[..<end])
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

    @ObservationIgnored private var statusObservation: NSKeyValueObservation?
    @ObservationIgnored private var timeObservation: Any?

    var orderedEpisodeIndices: [Int] {
        let values = Array(currentResult?.episodes.indices ?? 0..<0)
        return isEpisodeReversed ? values.reversed() : values
    }

    var isPlaying: Bool { player != nil }

    func open(result: SearchResult, episode: Int = 0, seekTo: Int = 0) {
        sourceResults = sourceResults.isEmpty ? [result] : sourceResults
        currentResult = result
        playEpisode(episode, seekTo: seekTo)
    }

    func playEpisode(_ index: Int, seekTo: Int = 0) {
        guard let result = currentResult,
              result.episodes.indices.contains(index),
              let url = URL(string: result.episodes[index]) else {
            errorMessage = "播放地址无效"
            return
        }
        stopPlayerOnly()
        episodeIndex = index
        errorMessage = nil
        let item = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: item)
        self.player = player
        observe(player: player, item: item, seekTo: seekTo)
        player.play()
    }

    func switchSource(_ result: SearchResult) {
        currentResult = result
        playEpisode(min(episodeIndex, max(result.episodes.count - 1, 0)))
    }

    func resume(record: PlayRecord, client: LunaTVClient) async {
        do {
            let result = try await client.detail(source: record.source, id: record.itemID)
            sourceResults = [result]
            open(result: result, episode: min(record.episodeIndex, max(result.episodes.count - 1, 0)), seekTo: record.playTime)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func makeRecord() -> PlayRecord? {
        guard let result = currentResult else { return nil }
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
            type: result.typeName
        )
    }

    func stop() {
        stopPlayerOnly()
        sourceResults = []
        currentResult = nil
        episodeIndex = 0
        playTime = 0
        totalTime = 0
        errorMessage = nil
    }

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
        player = nil
    }
}

private extension String {
    var nonEmpty: String? { isEmpty ? nil : self }
}