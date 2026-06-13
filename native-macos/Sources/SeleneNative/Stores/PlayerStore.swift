import SwiftUI
import AVKit

@MainActor
final class PlayerStore: ObservableObject {
    @Published var player: AVPlayer?
    @Published var playbackError: String?
    @Published var currentEpisodeURL: URL?

    private var playerObserver: NSKeyValueObservation?

    init() {}

    func loadEpisode(url: URL) {
        playbackError = nil
        currentEpisodeURL = url

        let playerItem = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: playerItem)
        self.player = player

        playerObserver = playerItem.observe(
            \.status,
            options: [.new, .old]
        ) { [weak self] item, _ in
            Task { @MainActor in
                if item.status == .failed {
                    self?.playbackError = self?.playerItemErrorDescription(item.error)
                }
            }
        }
    }

    func play() {
        player?.play()
    }

    func pause() {
        player?.pause()
    }

    func replaceItem(url: URL) {
        playerObserver?.invalidate()
        playerObserver = nil
        loadEpisode(url: url)
    }

    func stop() {
        player?.pause()
        player = nil
        playerObserver?.invalidate()
        playerObserver = nil
        currentEpisodeURL = nil
        playbackError = nil
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
