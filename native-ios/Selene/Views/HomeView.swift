import SwiftUI

struct HomeView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator
    @State private var store = HomeStore()

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                PageHeader(
                    title: "今日片库",
                    subtitle: "从 LunaTV 获取的热门内容，针对 iPad 大屏重新排版。",
                    systemImage: "play.rectangle.on.rectangle"
                )

                switch store.phase {
                case .idle, .loading:
                    loadingContent
                case .failed(let message):
                    ContentUnavailableView(
                        "首页加载失败",
                        systemImage: "wifi.exclamationmark",
                        description: Text(message)
                    )
                    .appSurface()
                case .loaded:
                    ContinueWatchingSection(records: appModel.playRecords, action: resume)
                    sections
                }
            }
            .padding(AppTheme.pagePadding)
            .frame(maxWidth: 1300, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        .refreshable {
            guard let client = appModel.client else { return }
            await store.load(using: client, force: true)
        }
        .task(id: appModel.session?.id) {
            guard let client = appModel.client else { return }
            await store.load(using: client)
        }
    }

    private var sections: some View {
        Group {
            mediaSection("热门电影", subtitle: "正在热映与高热度影片", movies: store.movies)
            mediaSection("热门剧集", subtitle: "连续剧与流媒体剧集", movies: store.tvShows)
            mediaSection("电视动画", subtitle: "LunaTV 动画分类", movies: store.anime)
            BangumiSection(items: store.bangumi)
            mediaSection("热门综艺", subtitle: "近期热门节目", movies: store.shows)
        }
    }

    private var loadingContent: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                ProgressView()
                Text("正在加载内容…")
                    .foregroundStyle(.secondary)
            }
            ForEach(0..<2, id: \.self) { _ in
                RoundedRectangle(cornerRadius: AppTheme.cornerRadius)
                    .fill(Color.secondary.opacity(0.1))
                    .frame(height: 220)
            }
        }
        .appSurface()
        .redacted(reason: .placeholder)
    }

    private func mediaSection(
        _ title: String,
        subtitle: String,
        movies: [DoubanMovie]
    ) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.title2.bold())
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            if movies.isEmpty {
                Label("此分类暂时没有内容", systemImage: "rectangle.stack")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, minHeight: 100, alignment: .leading)
                    .appSurface()
            } else {
                ScrollView(.horizontal) {
                    LazyHStack(alignment: .top, spacing: 16) {
                        ForEach(movies.prefix(16)) { movie in
                            NavigationLink(value: movie) {
                                PosterCard(movie: movie)
                                    .frame(width: 152)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding(.vertical, 2)
                }
                .scrollIndicators(.hidden)
            }
        }
    }

    private func resume(_ record: PlayRecord) {
        guard let client = appModel.client else { return }
        Task {
            await playerCoordinator.resume(record: record, client: client)
        }
    }
}

private struct ContinueWatchingSection: View {
    let records: [PlayRecord]
    let action: (PlayRecord) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("继续观看")
                .font(.title2.bold())

            if records.isEmpty {
                Label("暂无播放记录", systemImage: "clock")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, minHeight: 90, alignment: .leading)
                    .appSurface()
            } else {
                ScrollView(.horizontal) {
                    LazyHStack(spacing: 14) {
                        ForEach(records.prefix(10)) { record in
                            Button {
                                action(record)
                            } label: {
                                VStack(alignment: .leading, spacing: 8) {
                                    RemotePosterView(urlString: record.cover)
                                        .frame(width: 128)
                                    Text(record.title)
                                        .font(.subheadline.weight(.semibold))
                                        .lineLimit(1)
                                    ProgressView(value: record.progress)
                                    Text("第 \(record.episodeNumber) 集 · \(record.formattedTime)")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                                .frame(width: 128, alignment: .leading)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .scrollIndicators(.hidden)
            }
        }
    }
}

private struct BangumiSection: View {
    let items: [BangumiItem]

    var body: some View {
        if !items.isEmpty {
            VStack(alignment: .leading, spacing: 14) {
                Text("今日番组")
                    .font(.title2.bold())
                ScrollView(.horizontal) {
                    LazyHStack(alignment: .top, spacing: 16) {
                        ForEach(items.prefix(16)) { item in
                            VStack(alignment: .leading, spacing: 8) {
                                RemotePosterView(urlString: item.poster)
                                Text(item.displayName)
                                    .font(.subheadline.weight(.semibold))
                                    .lineLimit(2)
                                if let score = item.rating?.score, score > 0 {
                                    Label(score.formatted(.number.precision(.fractionLength(1))), systemImage: "star.fill")
                                        .font(.caption)
                                        .foregroundStyle(.orange)
                                }
                            }
                            .frame(width: 152)
                        }
                    }
                }
                .scrollIndicators(.hidden)
            }
        }
    }
}