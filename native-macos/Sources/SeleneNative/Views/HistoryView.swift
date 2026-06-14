import SwiftUI

struct HistoryView: View {
    let historyStore: HistoryStore
    let provider: ContentProvider
    let onPlayRecord: ((PlayRecord) -> Void)?

    var body: some View {
        Group {
            if historyStore.playRecords.isEmpty {
                ContentUnavailableView("暂无播放记录", systemImage: "clock", description: Text("开始播放后会自动记录进度"))
            } else {
                List(historyStore.playRecords) { record in
                    VideoCardView(
                        title: record.title,
                        poster: record.cover,
                        sourceName: record.sourceName,
                        year: record.year,
                        subtitle: "第\(record.index + 1)集 \(record.formattedPlayTime) / \(record.formattedTotalTime)",
                        progress: record.progressPercentage
                    )
                    .onTapGesture { onPlayRecord?(record) }
                }
            }
        }
        .task {
            await historyStore.loadRecords(provider: provider)
        }
        .toolbar {
            Button(role: .destructive) {
                Task { await historyStore.clearRecords(provider: provider) }
            } label: {
                Label("清空", systemImage: "trash")
            }
        }
    }
}
