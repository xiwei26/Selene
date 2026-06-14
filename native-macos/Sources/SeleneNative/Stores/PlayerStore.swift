import SwiftUI
import AVKit

@MainActor
@Observable
final class PlayerStore {
    var player: AVPlayer?
    var playbackError: String?
    var currentEpisodeURL: URL?
    var currentResult: SearchResult?
    var currentSourceResults: [SearchResult] = []
    var currentEpisodeIndex: Int = 0
    var isEpisodeReversed: Bool = false
    var playTime: Int = 0
    var totalTime: Int = 0
    var pendingSeekTime: Int?

    @ObservationIgnored private var playerObserver: NSKeyValueObservation?
    @ObservationIgnored private var timeObserver: Any?

    init() {}

    deinit {
        MainActor.assumeIsolated {
            invalidateObservers()
        }
    }

    var orderedEpisodeIndices: [Int] {
        let count = currentResult?.episodes.count ?? 0
        let indices = Array(0..<count)
        return isEpisodeReversed ? indices.reversed() : indices
    }

    func loadEpisode(url: URL, result: SearchResult? = nil, index: Int = 0) {
        playbackError = nil
        currentEpisodeURL = url
        currentResult = result ?? currentResult
        currentEpisodeIndex = index

        let playerItem = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: playerItem)
        self.player = player
        observe(player: player, item: playerItem)
    }

    func play() {
        player?.play()
    }

    func pause() {
        player?.pause()
    }

    func replaceItem(url: URL, result: SearchResult? = nil, index: Int = 0) {
        invalidateObservers()
        loadEpisode(url: url, result: result, index: index)
    }

    func switchSource(to result: SearchResult) {
        currentResult = result
        currentEpisodeIndex = min(currentEpisodeIndex, max(result.episodes.count - 1, 0))
        guard result.episodes.indices.contains(currentEpisodeIndex),
              let url = URL(string: result.episodes[currentEpisodeIndex]) else {
            return
        }
        replaceItem(url: url, result: result, index: currentEpisodeIndex)
        play()
    }

    func playEpisode(at index: Int) {
        guard let result = currentResult,
              result.episodes.indices.contains(index),
              let url = URL(string: result.episodes[index]) else {
            return
        }
        replaceItem(url: url, result: result, index: index)
        play()
    }

    func toggleEpisodeOrder() {
        isEpisodeReversed.toggle()
    }

    func loadDetailAndPlay(record: PlayRecord, provider: ContentProvider) async {
        do {
            guard let result = try await provider.detail(source: record.source, id: record.id) else {
                playbackError = "未找到该视频详情"
                return
            }
            currentSourceResults = [result]

            guard result.episodes.indices.contains(record.index),
                  let url = URL(string: result.episodes[record.index]) else {
                playbackError = "剧集链接不可用"
                return
            }

            currentResult = result
            currentEpisodeIndex = record.index
            replaceItem(url: url, result: result, index: record.index)
            if record.playTime > 0 {
                pendingSeekTime = record.playTime
            }
            play()
        } catch {
            playbackError = "获取视频详情失败: \(error.localizedDescription)"
        }
    }

    func makePlayRecord() -> PlayRecord? {
        guard let result = currentResult else { return nil }
        return PlayRecord(
            id: "\(result.source)+\(result.id)",
            source: result.source,
            title: result.title,
            sourceName: result.sourceName,
            year: result.year,
            cover: result.poster,
            index: currentEpisodeIndex,
            totalEpisodes: result.episodes.count,
            playTime: playTime,
            totalTime: totalTime,
            saveTime: Int64(Date().timeIntervalSince1970 * 1000),
            searchTitle: result.title
        )
    }

    func stop() {
        player?.pause()
        player = nil
        invalidateObservers()
        currentEpisodeURL = nil
        playbackError = nil
        playTime = 0
        totalTime = 0
    }

    private func observe(player: AVPlayer, item: AVPlayerItem) {
        playerObserver = item.observe(
            \.status,
            options: [.new, .old]
        ) { [weak self] item, _ in
            Task { @MainActor in
                if item.status == .failed {
                    self?.playbackError = self?.playerItemErrorDescription(item.error)
                } else if item.status == .readyToPlay {
                    self?.totalTime = Self.seconds(from: item.duration)
                    if let seekTime = self?.pendingSeekTime {
                        let target = CMTime(seconds: Double(seekTime), preferredTimescale: 600)
                        self?.player?.seek(to: target, toleranceBefore: .zero, toleranceAfter: .zero)
                        self?.pendingSeekTime = nil
                    }
                }
            }
        }

        timeObserver = player.addPeriodicTimeObserver(
            forInterval: CMTime(seconds: 10, preferredTimescale: 600),
            queue: .main
        ) { [weak self] time in
            Task { @MainActor in
                self?.playTime = Self.seconds(from: time)
            }
        }
    }

    private func invalidateObservers() {
        if let timeObserver {
            player?.removeTimeObserver(timeObserver)
        }
        timeObserver = nil
        playerObserver?.invalidate()
        playerObserver = nil
    }

    private static func seconds(from time: CMTime) -> Int {
        guard time.isNumeric && !time.seconds.isNaN && !time.seconds.isInfinite else { return 0 }
        return max(Int(time.seconds), 0)
    }

    private func playerItemErrorDescription(_ error: Error?) -> String {
        guard let error = error else { return "播放失败" }
        let nsError = error as NSError
        if nsError.domain == NSURLErrorDomain {
            switch nsError.code {
            case NSURLErrorNotConnectedToInternet:
                return "网络连接失败，请检查网络"
            case NSURLErrorTimedOut:
                return "连接超时"
            default:
                return "播放失败: \(error.localizedDescription)"
            }
        }
        return "播放失败: \(error.localizedDescription)"
    }
}
