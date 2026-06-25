import SwiftUI

struct PlayerEpisodesView: View {
    let playerStore: PlayerStore

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("剧集")
                    .font(.headline)
                Spacer()
                Button {
                    playerStore.toggleEpisodeOrder()
                } label: {
                    Label("倒序", systemImage: "arrow.up.arrow.down")
                }
                .labelStyle(.iconOnly)
                .help("切换剧集顺序")
            }

            ScrollView(.horizontal) {
                HStack(spacing: 8) {
                    ForEach(playerStore.orderedEpisodeIndices, id: \.self) { index in
                        Button {
                            playerStore.playEpisode(at: index)
                        } label: {
                            Text(playerStore.currentResult?.episodeTitle(for: index) ?? "第\(index + 1)集")
                                .font(.caption)
                                .padding(.horizontal, 10)
                                .padding(.vertical, 6)
                                .background(index == playerStore.currentEpisodeIndex ? AppTheme.softAccent : AppTheme.surface)
                                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }
}
