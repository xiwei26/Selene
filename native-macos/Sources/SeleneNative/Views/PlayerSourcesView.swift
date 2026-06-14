import SwiftUI

struct PlayerSourcesView: View {
    let playerStore: PlayerStore

    var body: some View {
        if !playerStore.currentSourceResults.isEmpty {
            VStack(alignment: .leading, spacing: 8) {
                Text("播放源")
                    .font(.headline)

                ScrollView(.horizontal) {
                    HStack(spacing: 8) {
                        ForEach(playerStore.currentSourceResults) { result in
                            Button {
                                playerStore.switchSource(to: result)
                            } label: {
                                HStack(spacing: 6) {
                                    Image(systemName: result.id == playerStore.currentResult?.id ? "checkmark.circle.fill" : "circle")
                                    Text(result.sourceName.isEmpty ? result.source : result.sourceName)
                                }
                                .font(.caption)
                                .padding(.horizontal, 10)
                                .padding(.vertical, 6)
                                .background(Color.secondary.opacity(0.12))
                                .clipShape(RoundedRectangle(cornerRadius: 6))
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
            }
        }
    }
}
