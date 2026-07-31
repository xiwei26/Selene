import SwiftUI

struct FavoritesView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator

    var body: some View {
        List {
            if appModel.favorites.isEmpty {
                ContentUnavailableView(
                    "暂无收藏",
                    systemImage: "heart",
                    description: Text("在资源详情页点击收藏后会显示在这里。")
                )
                .listRowBackground(Color.clear)
            } else {
                ForEach(appModel.favorites) { item in
                    Button {
                        play(item)
                    } label: {
                        LibraryMediaRow(
                            title: item.title,
                            poster: item.cover,
                            metadata: [item.year, item.sourceName, "共 \(item.totalEpisodes) 集"]
                                .filter { !$0.isEmpty }
                                .joined(separator: " · ")
                        )
                    }
                    .buttonStyle(.plain)
                    .swipeActions {
                        Button("删除", role: .destructive) {
                            Task { await appModel.removeFavorite(item) }
                        }
                    }
                    .listRowBackground(Color.clear)
                }
            }
        }
        .listStyle(.plain)
        .scrollContentBackground(.hidden)
        .navigationTitle("收藏")
        .refreshable { await appModel.refreshLibrary() }
    }

    private func play(_ item: FavoriteItem) {
        guard let client = appModel.client else { return }
        let record = PlayRecord(
            id: item.id,
            source: item.source,
            title: item.title,
            sourceName: item.sourceName,
            year: item.year,
            cover: item.cover,
            index: 1,
            totalEpisodes: item.totalEpisodes,
            playTime: 0,
            totalTime: 0,
            saveTime: item.saveTime,
            searchTitle: item.searchTitle
        )
        Task { await playerCoordinator.resume(record: record, client: client) }
    }
}

struct HistoryView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator

    @State private var showsClearConfirmation = false

    var body: some View {
        List {
            if appModel.playRecords.isEmpty {
                ContentUnavailableView(
                    "暂无播放记录",
                    systemImage: "clock",
                    description: Text("播放视频后会自动保存选集和进度。")
                )
                .listRowBackground(Color.clear)
            } else {
                ForEach(appModel.playRecords) { record in
                    Button {
                        resume(record)
                    } label: {
                        VStack(alignment: .leading, spacing: 8) {
                            LibraryMediaRow(
                                title: record.title,
                                poster: record.cover,
                                metadata: "第 \(record.episodeNumber) 集 · \(record.formattedTime)"
                            )
                            ProgressView(value: record.progress)
                                .tint(AppTheme.accent)
                        }
                    }
                    .buttonStyle(.plain)
                    .listRowBackground(Color.clear)
                }
            }
        }
        .listStyle(.plain)
        .scrollContentBackground(.hidden)
        .navigationTitle("播放历史")
        .refreshable { await appModel.refreshLibrary() }
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("清空", systemImage: "trash", role: .destructive) {
                    showsClearConfirmation = true
                }
                .disabled(appModel.playRecords.isEmpty)
            }
        }
        // Clearing writes through to the server and cannot be undone.
        .confirmationDialog(
            "清空全部播放记录？",
            isPresented: $showsClearConfirmation,
            titleVisibility: .visible
        ) {
            Button("清空", role: .destructive) {
                Task { await appModel.clearHistory() }
            }
            Button("取消", role: .cancel) {}
        } message: {
            Text("服务器上的播放进度会一并删除，且无法恢复。")
        }
    }

    private func resume(_ record: PlayRecord) {
        guard let client = appModel.client else { return }
        Task { await playerCoordinator.resume(record: record, client: client) }
    }
}

private struct LibraryMediaRow: View {
    let title: String
    let poster: String
    let metadata: String

    /// Grows the thumbnail alongside the row's text at larger Dynamic Type sizes.
    @ScaledMetric(relativeTo: .headline) private var posterWidth: CGFloat = 68

    var body: some View {
        HStack(spacing: 14) {
            RemotePosterView(urlString: poster)
                .frame(width: posterWidth)
            VStack(alignment: .leading, spacing: 6) {
                Text(title)
                    .font(.headline)
                    .foregroundStyle(.primary)
                Text(metadata)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
            Spacer()
            Image(systemName: "play.circle.fill")
                .font(.title2)
                .foregroundStyle(AppTheme.accent)
        }
        .padding(.vertical, 5)
        .contentShape(Rectangle())
    }
}