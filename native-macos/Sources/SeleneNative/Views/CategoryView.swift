import SwiftUI

enum DiscoveryCategory: String, Identifiable {
    case movie, tv, anime, show

    var id: String { rawValue }

    var title: String {
        switch self {
        case .movie: return "电影"
        case .tv: return "电视剧"
        case .anime: return "动漫"
        case .show: return "综艺"
        }
    }
}

struct CategoryView: View {
    let category: DiscoveryCategory
    let doubanProvider: DoubanProviding
    let bangumiProvider: BangumiProviding

    @State private var movies: [DoubanMovie] = []
    @State private var bangumiItems: [BangumiItem] = []
    @State private var isLoading = false
    @State private var errorMessage: String?

    private let columns = [GridItem(.adaptive(minimum: 280, maximum: 360), spacing: 12)]

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(
                    title: category.title,
                    subtitle: category == .anime ? "今日番组与热门动画" : "热门条目和评分信息",
                    systemImage: category.icon
                )

                if isLoading {
                    ProgressView("加载\(category.title)...")
                        .appSurface()
                }

                if let errorMessage {
                    Text(errorMessage)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .appSurface()
                }

                LazyVGrid(columns: columns, spacing: 12) {
                    if category == .anime {
                        ForEach(bangumiItems) { item in
                            VideoCardView(
                                title: item.nameCn?.isEmpty == false ? item.nameCn! : item.name,
                                poster: item.images.bestImageUrl,
                                sourceName: "Bangumi",
                                year: item.airDate,
                                subtitle: item.rating.score > 0 ? "评分 \(String(format: "%.1f", item.rating.score))" : nil
                            )
                        }
                    } else {
                        ForEach(movies) { movie in
                            VideoCardView(
                                title: movie.title,
                                poster: movie.poster,
                                sourceName: "Douban",
                                year: movie.year,
                                subtitle: movie.rate.map { "评分 \($0)" }
                            )
                        }
                    }
                }
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

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        errorMessage = nil

        do {
            switch category {
            case .movie:
                movies = try await doubanProvider.getHotMovies()
            case .tv:
                movies = try await doubanProvider.getHotTVShows()
            case .show:
                movies = try await doubanProvider.getHotShows()
            case .anime:
                bangumiItems = try await bangumiProvider.getTodayCalendar()
            }
        } catch {
            errorMessage = error.localizedDescription
        }
    }
}

private extension DiscoveryCategory {
    var icon: String {
        switch self {
        case .movie: return "film"
        case .tv: return "tv"
        case .anime: return "sparkles.tv"
        case .show: return "theatermasks"
        }
    }
}
