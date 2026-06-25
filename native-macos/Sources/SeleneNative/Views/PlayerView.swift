import SwiftUI
import AVKit

struct PlayerView: View {
    let playerStore: PlayerStore

    var body: some View {
        VStack(spacing: 10) {
            Group {
                if let player = playerStore.player {
                    NativeVideoPlayerView(player: player)
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

            if playerStore.currentResult != nil {
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Text(playerStore.currentResult?.title ?? "")
                            .font(.headline)
                            .lineLimit(1)
                        Spacer()
                        Text("\(PlayRecord.formatForDisplay(playerStore.playTime)) / \(PlayRecord.formatForDisplay(playerStore.totalTime))")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    PlayerSourcesView(playerStore: playerStore)
                    PlayerEpisodesView(playerStore: playerStore)
                }
                .appSurface()
                .padding(.horizontal, AppTheme.pagePadding)
                .padding(.bottom, AppTheme.pagePadding)
            }
        }
        .appPageBackground()
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
        .appSurface()
        .padding()
    }
}

private extension PlayRecord {
    static func formatForDisplay(_ seconds: Int) -> String {
        let clamped = max(seconds, 0)
        let hours = clamped / 3600
        let minutes = (clamped % 3600) / 60
        let seconds = clamped % 60
        if hours > 0 {
            return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
        }
        return String(format: "%02d:%02d", minutes, seconds)
    }
}
