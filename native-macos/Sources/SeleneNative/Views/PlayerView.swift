import SwiftUI
import AVKit

struct PlayerView: View {
    @State private var playerStore: PlayerStore

    init(playerStore: PlayerStore) {
        _playerStore = State(initialValue: playerStore)
    }

    var body: some View {
        Group {
            if let player = playerStore.player {
                VideoPlayer(player: player)
                    .aspectRatio(contentMode: .fit)
            } else if let error = playerStore.playbackError {
                playbackErrorView(error)
            } else {
                ContentUnavailableView(
                    "选择剧集播放",
                    systemImage: "play.circle",
                    description: Text("从详情页面选择一个剧集开始播放")
                )
            }
        }
    }

    private func playbackErrorView(_ error: String) -> some View {
        VStack(spacing: 16) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 40))
                .foregroundStyle(.orange)

            Text("播放失败")
                .font(.headline)

            Text(error)
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

            Button("重试") {
                if let url = playerStore.currentEpisodeURL {
                    playerStore.replaceItem(url: url)
                    playerStore.play()
                }
            }
        }
        .padding()
    }
}
