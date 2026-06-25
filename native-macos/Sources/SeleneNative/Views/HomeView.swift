import SwiftUI

struct HomeView: View {
    let historyStore: HistoryStore
    let doubanProvider: DoubanProviding
    let bangumiProvider: BangumiProviding
    let onPlayRecord: ((PlayRecord) -> Void)?

    @State private var hotMovies: [DoubanMovie] = []
    @State private var hotTVShows: [DoubanMovie] = []
    @State private var hotShows: [DoubanMovie] = []
    @State private var bangumiItems: [BangumiItem] = []
    @State private var isLoading = false
    @State private var errorMessage: String?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(
                    title: "今日片库",
                    subtitle: "继续观看、热门内容和每日番组集中在这里。",
                    systemImage: "play.rectangle.on.rectangle"
                )

                if isLoading {
                    ProgressView("加载首页内容...")
                        .appSurface()
                }

                if let errorMessage {
                    Text(errorMessage)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .appSurface()
                }

                continueWatchingSection
                doubanSection("热门电影", movies: hotMovies)
                doubanSection("热门剧集", movies: hotTVShows)
                bangumiSection
                doubanSection("热门综艺", movies: hotShows)
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
        .task {
            await load()
        }
        .refreshable {
            await load()
        }
    }

    private var continueWatchingSection: some View {
        contentSection("继续观看", subtitle: "从上次中断的位置继续", count: historyStore.playRecords.count) {
            if historyStore.playRecords.isEmpty {
                emptyInline("暂无播放记录", systemImage: "clock")
            } else {
                horizontalCards {
                    ForEach(historyStore.playRecords.prefix(10)) { record in
                        Button {
                            onPlayRecord?(record)
                        } label: {
                            VideoCardView(
                                title: record.title,
                                poster: record.cover,
                                sourceName: record.sourceName,
                                year: record.year,
                                subtitle: "第\(record.episodeNumber)集",
                                progress: record.progressPercentage
                            )
                            .frame(width: 280)
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }

    private var bangumiSection: some View {
        contentSection("今日番组", subtitle: "Bangumi 日历", count: bangumiItems.count) {
            if bangumiItems.isEmpty {
                emptyInline("暂无番组数据", systemImage: "calendar")
            } else {
                horizontalCards {
                    ForEach(bangumiItems.prefix(12)) { item in
                        VideoCardView(
                            title: item.nameCn?.isEmpty == false ? item.nameCn! : item.name,
                            poster: item.images.bestImageUrl,
                            sourceName: "Bangumi",
                            year: item.airDate,
                            subtitle: item.rating.score > 0 ? "评分 \(String(format: "%.1f", item.rating.score))" : nil
                        )
                        .frame(width: 280)
                    }
                }
            }
        }
    }

    private func doubanSection(_ title: String, movies: [DoubanMovie]) -> some View {
        contentSection(title, subtitle: "来自 Douban", count: movies.count) {
            if movies.isEmpty {
                emptyInline("暂无内容", systemImage: "rectangle.stack")
            } else {
                horizontalCards {
                    ForEach(movies.prefix(12)) { movie in
                        VideoCardView(
                            title: movie.title,
                            poster: movie.poster,
                            sourceName: "Douban",
                            year: movie.year,
                            subtitle: movie.rate.map { "评分 \($0)" }
                        )
                        .frame(width: 280)
                    }
                }
            }
        }
    }

    private func contentSection<Content: View>(_ title: String, subtitle: String? = nil, count: Int? = nil, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            AppSectionHeader(title: title, subtitle: subtitle, count: count)
            content()
        }
    }

    private func horizontalCards<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        ScrollView(.horizontal) {
            LazyHStack(spacing: 14) {
                content()
            }
            .padding(.vertical, 1)
        }
        .scrollIndicators(.hidden)
    }

    private func emptyInline(_ text: String, systemImage: String) -> some View {
        HStack(spacing: 8) {
            Image(systemName: systemImage)
            Text(text)
        }
        .font(.callout)
        .foregroundStyle(.secondary)
        .frame(maxWidth: .infinity, minHeight: 72, alignment: .leading)
        .appSurface()
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        async let movies = try? doubanProvider.getHotMovies()
        async let tv = try? doubanProvider.getHotTVShows()
        async let shows = try? doubanProvider.getHotShows()
        async let bangumi = try? bangumiProvider.getTodayCalendar()
        hotMovies = await movies ?? []
        hotTVShows = await tv ?? []
        hotShows = await shows ?? []
        bangumiItems = await bangumi ?? []
        errorMessage = hotMovies.isEmpty && hotTVShows.isEmpty && bangumiItems.isEmpty ? "发现页暂时没有加载到内容" : nil
    }
}
