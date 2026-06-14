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
            LazyVStack(alignment: .leading, spacing: 22) {
                if isLoading {
                    ProgressView("加载首页内容...")
                }

                if let errorMessage {
                    Text(errorMessage)
                        .font(.caption)
                        .foregroundStyle(.red)
                }

                continueWatchingSection
                doubanSection("热门电影", movies: hotMovies)
                doubanSection("热门剧集", movies: hotTVShows)
                bangumiSection
                doubanSection("热门综艺", movies: hotShows)
            }
            .padding()
        }
        .task {
            await load()
        }
        .refreshable {
            await load()
        }
    }

    private var continueWatchingSection: some View {
        contentSection("继续观看") {
            if historyStore.playRecords.isEmpty {
                Text("暂无播放记录")
                    .foregroundStyle(.secondary)
            } else {
                horizontalCards {
                    ForEach(historyStore.playRecords.prefix(10)) { record in
                        VideoCardView(
                            title: record.title,
                            poster: record.cover,
                            sourceName: record.sourceName,
                            year: record.year,
                            subtitle: "第\(record.index + 1)集",
                            progress: record.progressPercentage
                        )
                        .frame(width: 260)
                        .onTapGesture { onPlayRecord?(record) }
                    }
                }
            }
        }
    }

    private var bangumiSection: some View {
        contentSection("今日番组") {
            if bangumiItems.isEmpty {
                Text("暂无番组数据")
                    .foregroundStyle(.secondary)
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
                        .frame(width: 260)
                    }
                }
            }
        }
    }

    private func doubanSection(_ title: String, movies: [DoubanMovie]) -> some View {
        contentSection(title) {
            if movies.isEmpty {
                Text("暂无内容")
                    .foregroundStyle(.secondary)
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
                        .frame(width: 260)
                    }
                }
            }
        }
    }

    private func contentSection<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title)
                .font(.title3)
                .bold()
            content()
        }
    }

    private func horizontalCards<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        ScrollView(.horizontal) {
            LazyHStack(spacing: 14) {
                content()
            }
        }
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
