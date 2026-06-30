import SwiftUI

struct HistoryView: View {
    let historyStore: HistoryStore
    let provider: ContentProvider
    let onPlayRecord: ((PlayRecord) -> Void)?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(
                    title: "历史",
                    subtitle: "自动保存播放进度，方便继续观看。",
                    systemImage: "clock"
                )

            if historyStore.playRecords.isEmpty {
                ContentUnavailableView("暂无播放记录", systemImage: "clock", description: Text("开始播放后会自动记录进度"))
            } else {
                    LazyVStack(spacing: 10) {
                        ForEach(historyStore.playRecords) { record in
                            Button {
                                onPlayRecord?(record)
                            } label: {
                                VideoCardView(
                                    title: record.title,
                                    poster: record.cover,
                                    sourceName: record.sourceName,
                                    year: record.year,
                                    subtitle: "第\(record.episodeNumber)集 \(record.formattedPlayTime) / \(record.formattedTotalTime)",
                                    progress: record.progressPercentage
                                )
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
            }
        }
        .padding(AppTheme.pagePadding)
        .appPageBackground()
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
