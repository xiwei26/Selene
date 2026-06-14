import SwiftUI

struct PlayerScreen: View {
    @Bindable var playerStore: PlayerStore
    let onClose: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            // Close button bar
            HStack {
                Button {
                    onClose()
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "chevron.left")
                        Text("返回")
                    }
                }
                .buttonStyle(.borderless)
                .padding(.leading, 16)
                .padding(.vertical, 10)

                Spacer()

                if let title = playerStore.currentResult?.title {
                    Text(title)
                        .font(.headline)
                        .lineLimit(1)
                }

                Spacer()
                // Balance the trailing space so the title stays centered-ish
                HStack(spacing: 6) { } .frame(width: 60)
            }

            Divider()

            PlayerView(playerStore: playerStore)
        }
    }
}
