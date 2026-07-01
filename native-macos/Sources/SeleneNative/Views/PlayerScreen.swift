import SwiftUI

struct PlayerScreen: View {
    @Bindable var playerStore: PlayerStore
    let provider: ContentProvider?
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Button {
                    onClose()
                } label: {
                    Label("返回", systemImage: "chevron.left")
                }
                .buttonStyle(.borderless)
                .help("返回")

                Spacer()

                if let title = playerStore.currentResult?.title {
                    Text(title)
                        .font(.headline)
                        .lineLimit(1)
                }

                Spacer()
                HStack(spacing: 6) { } .frame(width: 60)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .background(AppTheme.elevatedSurface)

            Divider()

            PlayerView(playerStore: playerStore, provider: provider)
        }
        .appPageBackground()
    }
}
