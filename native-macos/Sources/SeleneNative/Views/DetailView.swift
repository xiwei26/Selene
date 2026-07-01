import SwiftUI

struct DetailView: View {
    let result: SearchResult
    var provider: ContentProvider?
    var isFavorited: Bool = false
    var onToggleFavorite: ((SearchResult) -> Void)?
    let onPlay: (SearchResult, Int, URL) -> Void

    @State private var tmdbBackdrop: TMDBBackdrop?
    @State private var quickInfo: DoubanQuickInfo?
    @State private var comments: [DoubanComment] = []
    @State private var recommendations: [DoubanRecommendation] = []

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                if let tmdbBackdrop {
                    tmdbHero(tmdbBackdrop)
                }
                headerSection
                descriptionSection
                quickInfoSection
                episodesSection
                commentsSection
                recommendationsSection
                Spacer()
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
        .task(id: "\(result.source)-\(result.id)") {
            await loadSupplementaryInfo()
        }
    }

    private var headerSection: some View {
        HStack(alignment: .top, spacing: 16) {
            posterView
                .frame(width: 112, height: 156)
                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))

            VStack(alignment: .leading, spacing: 12) {
                Text(result.title)
                    .font(.title2.weight(.semibold))
                    .lineLimit(3)

                HStack(spacing: 8) {
                    if !result.sourceName.isEmpty {
                        Text(result.sourceName)
                            .font(.caption.weight(.medium))
                            .padding(.horizontal, 9)
                            .padding(.vertical, 4)
                            .background(AppTheme.softAccent)
                            .clipShape(Capsule())
                    }

                    if !result.year.isEmpty {
                        Text(result.year)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }

                    if let typeName = result.typeName, !typeName.isEmpty {
                        Text(typeName)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }

                Text("\(result.episodes.count) 集")
                    .font(.caption)
                    .foregroundStyle(.secondary)

                if let onToggleFavorite {
                    Button {
                        onToggleFavorite(result)
                    } label: {
                        Label(isFavorited ? "取消收藏" : "收藏", systemImage: isFavorited ? "heart.fill" : "heart")
                    }
                    .help(isFavorited ? "取消收藏" : "收藏")
                }
            }

            Spacer(minLength: 0)
        }
        .appSurface()
    }

    private var descriptionSection: some View {
        Group {
            if let desc = result.description, !desc.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    AppSectionHeader(title: "简介")
                    Text(desc)
                        .font(.body)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }
                .appSurface()
            }
        }
    }

    private var quickInfoSection: some View {
        Group {
            if let quickInfo {
                VStack(alignment: .leading, spacing: 8) {
                    AppSectionHeader(title: "豆瓣详情")
                    if !quickInfo.genres.isEmpty {
                        Text("类型：\(quickInfo.genres.joined(separator: " / "))")
                    }
                    if !quickInfo.directors.isEmpty {
                        Text("导演：\(quickInfo.directors.joined(separator: " / "))")
                    }
                    if !quickInfo.cast.isEmpty {
                        Text("主演：\(quickInfo.cast.prefix(8).joined(separator: " / "))")
                    }
                    if let rating = quickInfo.rating, !rating.isEmpty {
                        Text("豆瓣评分：\(rating)")
                            .foregroundStyle(.yellow)
                    }
                    if let summary = quickInfo.summary, !summary.isEmpty {
                        Text(summary)
                            .foregroundStyle(.secondary)
                            .textSelection(.enabled)
                    }
                }
                .font(.caption)
                .appSurface()
            }
        }
    }

    private var episodesSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            AppSectionHeader(title: "剧集", count: result.episodes.count)

            if result.episodes.isEmpty {
                Text("暂无可播放剧集")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                LazyVGrid(
                    columns: [GridItem(.adaptive(minimum: 80, maximum: 120), spacing: 8)],
                    spacing: 8
                ) {
                    ForEach(result.episodes.indices, id: \.self) { index in
                        Button {
                            if let url = URL(string: result.episodes[index]) {
                                onPlay(result, index, url)
                            }
                        } label: {
                            Text(result.episodeTitle(for: index))
                                .font(.caption)
                                .foregroundStyle(.primary)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .frame(maxWidth: .infinity)
                                .background(AppTheme.surface)
                                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
                        }
                        .buttonStyle(.plain)
                        .help(result.episodes[index])
                    }
                }
            }
        }
        .appSurface()
    }

    private var commentsSection: some View {
        Group {
            if !comments.isEmpty {
                VStack(alignment: .leading, spacing: 8) {
                    AppSectionHeader(title: "豆瓣短评")
                    ForEach(comments.prefix(5)) { comment in
                        Text(comment.author.isEmpty ? comment.content : "\(comment.author)：\(comment.content)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .textSelection(.enabled)
                    }
                }
                .appSurface()
            }
        }
    }

    private var recommendationsSection: some View {
        Group {
            if !recommendations.isEmpty {
                VStack(alignment: .leading, spacing: 10) {
                    AppSectionHeader(title: "相关推荐")
                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 120), spacing: 10)], spacing: 10) {
                        ForEach(recommendations.prefix(8)) { item in
                            VStack(alignment: .leading, spacing: 6) {
                                poster(urlString: item.cover)
                                    .frame(height: 160)
                                    .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
                                Text(item.title)
                                    .font(.caption.weight(.semibold))
                                    .lineLimit(2)
                                if !item.rating.isEmpty {
                                    Text(item.rating)
                                        .font(.caption2)
                                        .foregroundStyle(.yellow)
                                }
                            }
                        }
                    }
                }
                .appSurface()
            }
        }
    }

    private func tmdbHero(_ backdrop: TMDBBackdrop) -> some View {
        ZStack(alignment: .bottomLeading) {
            poster(urlString: backdrop.backdrop ?? backdrop.poster ?? result.poster, systemImage: "film")
                .frame(height: 280)
                .clipped()
            LinearGradient(colors: [.clear, .black.opacity(0.78)], startPoint: .top, endPoint: .bottom)
            VStack(alignment: .leading, spacing: 8) {
                Text(backdrop.title ?? result.title)
                    .font(.largeTitle.weight(.bold))
                    .lineLimit(2)
                HStack(spacing: 8) {
                    if let rating = backdrop.rating {
                        Text("TMDB \(rating, specifier: "%.1f")")
                    }
                    if let year = backdrop.year, !year.isEmpty {
                        Text(year)
                    }
                    if let seasons = backdrop.numberOfSeasons, seasons > 0 {
                        Text("共 \(seasons) 季")
                    }
                }
                .font(.caption.weight(.semibold))
                if let overview = backdrop.overview, !overview.isEmpty {
                    Text(overview)
                        .font(.caption)
                        .lineLimit(3)
                }
            }
            .foregroundStyle(.white)
            .padding(18)
        }
        .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
    }

    @ViewBuilder
    private var posterView: some View {
        if !result.poster.isEmpty, let url = URL(string: result.poster) {
            AsyncImage(url: url) { phase in
                switch phase {
                case .empty:
                    posterPlaceholder
                case .success(let image):
                    image.resizable().scaledToFill()
                case .failure:
                    posterPlaceholder
                @unknown default:
                    posterPlaceholder
                }
            }
        } else {
            posterPlaceholder
        }
    }

    private var posterPlaceholder: some View {
        ZStack {
            AppTheme.surface
            Image(systemName: "film")
                .font(.largeTitle)
                .foregroundStyle(.secondary)
        }
    }

    private func poster(urlString: String, systemImage: String = "film") -> some View {
        ZStack {
            AppTheme.surface
            if let url = URL(string: urlString), !urlString.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    default:
                        Image(systemName: systemImage)
                            .font(.largeTitle)
                            .foregroundStyle(.secondary)
                    }
                }
            } else {
                Image(systemName: systemImage)
                    .font(.largeTitle)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func loadSupplementaryInfo() async {
        guard let provider else { return }
        async let backdrop = try? provider.getTMDBBackdrop(
            title: result.title,
            year: result.year.isEmpty ? nil : result.year,
            type: result.typeName
        )
        async let quick = try? provider.getDoubanQuickInfo(title: result.title)
        let doubanId = result.doubanID.map(String.init)
        async let loadedComments: [DoubanComment] = {
            guard let doubanId else { return [] }
            return (try? await provider.getDoubanComments(doubanId: doubanId)) ?? []
        }()
        async let loadedRecommendations: [DoubanRecommendation] = {
            guard let doubanId else { return [] }
            return (try? await provider.getDoubanRecommendations(doubanId: doubanId)) ?? []
        }()
        tmdbBackdrop = await backdrop
        quickInfo = await quick
        comments = await loadedComments
        recommendations = await loadedRecommendations
    }
}
