import SwiftUI

struct SearchDetailView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator
    @State private var store = SearchDetailStore()
    @State private var selectedSource: SearchResult?

    let result: SearchResult

    private var displayedResult: SearchResult {
        selectedSource ?? store.detail ?? result
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 26) {
                header

                if store.phase.isLoading {
                    ProgressView("正在加载剧集信息…")
                        .frame(maxWidth: .infinity, minHeight: 120)
                }

                if case .failed(let message) = store.phase {
                    Label("已显示搜索结果中的基础信息：\(message)", systemImage: "exclamationmark.circle")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .appSurface()
                }

                SourcePickerSection(
                    sources: store.alternatives,
                    selected: displayedResult,
                    action: { selectedSource = $0 }
                )
                episodeGrid
            }
            .padding(AppTheme.pagePadding)
            .frame(maxWidth: 1000, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        .background(AppBackground())
        .navigationTitle(result.title)
        .navigationBarTitleDisplayMode(.inline)
        .task(id: result.id) {
            guard let client = appModel.client else { return }
            await store.load(result: result, using: client)
            selectedSource = store.detail
        }
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    toggleFavorite()
                } label: {
                    Label(
                        isFavorite ? "取消收藏" : "收藏",
                        systemImage: isFavorite ? "heart.fill" : "heart"
                    )
                }
            }
        }
    }

    private var header: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 24) {
                RemotePosterView(urlString: displayedResult.poster)
                    .frame(width: 190)
                detailText
            }

            VStack(alignment: .leading, spacing: 18) {
                RemotePosterView(urlString: displayedResult.poster)
                    .frame(maxWidth: 190)
                detailText
            }
        }
    }

    private var detailText: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(displayedResult.title)
                .font(.largeTitle.bold())

            if !displayedResult.metadata.isEmpty {
                Text(displayedResult.metadata)
                    .font(.headline)
                    .foregroundStyle(.secondary)
            }

            if let description = displayedResult.description, !description.isEmpty {
                Text(description)
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Label(
                "\(displayedResult.episodes.count) 个播放地址",
                systemImage: "play.rectangle.on.rectangle"
            )
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(AppTheme.accent)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var episodeGrid: some View {
        if displayedResult.episodes.isEmpty, !store.phase.isLoading {
            ContentUnavailableView(
                "暂无播放地址",
                systemImage: "play.slash",
                description: Text("此资源没有返回可播放剧集。")
            )
            .appSurface()
        } else if !displayedResult.episodes.isEmpty {
            VStack(alignment: .leading, spacing: 14) {
                Text("选集")
                    .font(.title2.bold())

                LazyVGrid(
                    columns: [GridItem(.adaptive(minimum: 132), spacing: 12)],
                    alignment: .leading,
                    spacing: 12
                ) {
                    ForEach(Array(displayedResult.episodes.enumerated()), id: \.offset) { index, address in
                        Button {
                            guard URL(string: address) != nil else { return }
                            playerCoordinator.sourceResults = [displayedResult]
                            playerCoordinator.open(result: displayedResult, episode: index)
                        } label: {
                            Label(displayedResult.episodeTitle(at: index), systemImage: "play.fill")
                                .lineLimit(1)
                                .frame(maxWidth: .infinity, minHeight: 36)
                        }
                        .buttonStyle(.bordered)
                    }
                }
            }
        }
    }

    private var isFavorite: Bool {
        appModel.favorites.contains {
            $0.source == displayedResult.source && splitMediaKey($0.id).itemID == displayedResult.id
        }
    }

    private func toggleFavorite() {
        Task {
            if let favorite = appModel.favorites.first(where: {
                $0.source == displayedResult.source && splitMediaKey($0.id).itemID == displayedResult.id
            }) {
                await appModel.removeFavorite(favorite)
            } else {
                await appModel.addFavorite(displayedResult)
            }
        }
    }
}

private struct SourcePickerSection: View {
    let sources: [SearchResult]
    let selected: SearchResult
    let action: (SearchResult) -> Void

    var body: some View {
        if sources.count > 1 {
            VStack(alignment: .leading, spacing: 12) {
                Text("片源")
                    .font(.title2.bold())
                ScrollView(.horizontal) {
                    HStack {
                        ForEach(sources) { source in
                            Button {
                                action(source)
                            } label: {
                                Label(
                                    source.sourceName.isEmpty ? source.source : source.sourceName,
                                    systemImage: source.id == selected.id && source.source == selected.source
                                        ? "checkmark.circle.fill"
                                        : "circle"
                                )
                            }
                            .buttonStyle(.bordered)
                            .tint(source.id == selected.id && source.source == selected.source ? AppTheme.accent : .secondary)
                        }
                    }
                }
                .scrollIndicators(.hidden)
            }
        }
    }
}