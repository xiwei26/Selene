import SwiftUI
import AVKit

struct PlayerView: View {
    let playerStore: PlayerStore
    var provider: ContentProvider?

    @State private var tmdbBackdrop: TMDBBackdrop?
    @State private var quickInfo: DoubanQuickInfo?
    @State private var comments: [DoubanComment] = []
    @State private var recommendations: [DoubanRecommendation] = []

    var body: some View {
        VStack(spacing: 0) {
            Group {
                if let player = playerStore.player {
                    NativeVideoPlayerView(player: player)
                        .aspectRatio(contentMode: .fit)
                } else if let error = playerStore.playbackError {
                    playbackErrorView(error)
                } else {
                    ContentUnavailableView(
                        "选择剧集播放",
                        systemImage: "play.circle",
                        description: Text("从详情页面选择一个剧集开始播放")
                    )
                }
            }

            if let result = playerStore.currentResult {
                playerInfo(result)
                    .padding(.horizontal, AppTheme.pagePadding)
                    .padding(.vertical, 12)
            }
        }
        .appPageBackground()
        .task(id: playerStore.currentResult.map { "\($0.source)-\($0.id)" } ?? "none") {
            await loadSupplementaryInfo()
        }
    }

    private func playerInfo(_ result: SearchResult) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 6) {
                    Text(result.title)
                        .font(.headline)
                        .lineLimit(1)
                    metadataRow(result)
                }

                Spacer(minLength: 16)

                Text("\(PlayRecord.formatForDisplay(playerStore.playTime)) / \(PlayRecord.formatForDisplay(playerStore.totalTime))")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
            }

            PlayerSourcesView(playerStore: playerStore)
            PlayerEpisodesView(playerStore: playerStore)

            LunaPlayInfoPanel(
                result: result,
                episodeIndex: playerStore.currentEpisodeIndex,
                tmdbBackdrop: tmdbBackdrop,
                quickInfo: quickInfo,
                comments: comments,
                recommendations: recommendations
            )
        }
        .appSurface()
    }

    private func metadataRow(_ result: SearchResult) -> some View {
        ScrollView(.horizontal) {
            HStack(spacing: 8) {
                if !result.sourceName.isEmpty || !result.source.isEmpty {
                    metadataChip(result.sourceName.isEmpty ? result.source : result.sourceName)
                }
                if !result.year.isEmpty {
                    metadataChip(result.year)
                }
                if let typeName = result.typeName, !typeName.isEmpty {
                    metadataChip(typeName)
                }
                if let remarks = result.remarks, !remarks.isEmpty {
                    metadataChip(remarks)
                }
                if !result.episodes.isEmpty {
                    metadataChip("共\(result.episodes.count)集")
                }
            }
        }
        .scrollIndicators(.hidden)
    }

    private func metadataChip(_ text: String) -> some View {
        Text(text)
            .font(.caption2.weight(.medium))
            .foregroundStyle(.primary.opacity(0.72))
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background(AppTheme.softAccent)
            .clipShape(Capsule())
    }

    private func loadSupplementaryInfo() async {
        tmdbBackdrop = nil
        quickInfo = nil
        comments = []
        recommendations = []

        guard let provider, let result = playerStore.currentResult else { return }

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

    private func playbackErrorView(_ error: String) -> some View {
        VStack(spacing: 16) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 40))
                .foregroundStyle(.orange)

            Text("播放失败")
                .font(.headline)

            Text(error)
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

            Button("重试") {
                if let url = playerStore.currentEpisodeURL {
                    playerStore.replaceItem(url: url)
                    playerStore.play()
                }
            }
        }
        .appSurface()
        .padding()
    }
}

private struct LunaPlayInfoPanel: View {
    let result: SearchResult
    let episodeIndex: Int
    let tmdbBackdrop: TMDBBackdrop?
    let quickInfo: DoubanQuickInfo?
    let comments: [DoubanComment]
    let recommendations: [DoubanRecommendation]

    private var title: String {
        tmdbBackdrop?.title?.nonEmpty ?? result.title
    }

    private var overview: String? {
        tmdbBackdrop?.overview?.nonEmpty
            ?? quickInfo?.summary?.nonEmpty
            ?? result.description?.nonEmpty
    }

    private var ratingText: String? {
        if let rating = tmdbBackdrop?.rating, rating > 0 {
            return "TMDB \(String(format: "%.1f", rating))"
        }
        if let rating = quickInfo?.rating, !rating.isEmpty {
            return "豆瓣 \(rating)"
        }
        return nil
    }

    var body: some View {
        if hasContent {
            Divider()
                .opacity(0.5)

            VStack(alignment: .leading, spacing: 12) {
                if tmdbBackdrop?.backdrop?.nonEmpty != nil || tmdbBackdrop?.poster?.nonEmpty != nil {
                    hero
                }

                overviewTab

                if !comments.isEmpty {
                    commentsSection
                }

                if !recommendations.isEmpty {
                    recommendationsSection
                }
            }
        }
    }

    private var hasContent: Bool {
        overview != nil ||
            tmdbBackdrop != nil ||
            quickInfo != nil ||
            !comments.isEmpty ||
            !recommendations.isEmpty
    }

    private var hero: some View {
        ZStack(alignment: .bottomLeading) {
            AsyncPanelImage(urlString: tmdbBackdrop?.backdrop ?? tmdbBackdrop?.poster ?? result.poster)
                .frame(height: 260)
                .clipped()

            LinearGradient(
                colors: [.black.opacity(0.88), .black.opacity(0.52), .black.opacity(0.18)],
                startPoint: .leading,
                endPoint: .trailing
            )

            LinearGradient(
                colors: [.clear, .black.opacity(0.88)],
                startPoint: .top,
                endPoint: .bottom
            )

            HStack(alignment: .bottom, spacing: 18) {
                VStack(alignment: .leading, spacing: 10) {
                    chipRow(foreground: .white)

                    if let logo = tmdbBackdrop?.logo?.nonEmpty {
                        AsyncPanelImage(urlString: logo, systemImage: "textformat")
                            .scaledToFit()
                            .frame(maxWidth: 240, maxHeight: 86, alignment: .leading)
                    } else {
                        Text(title)
                            .font(.title.weight(.bold))
                            .foregroundStyle(.white)
                            .lineLimit(2)
                    }

                    if let overview {
                        Text(overview)
                            .font(.callout)
                            .foregroundStyle(.white.opacity(0.82))
                            .lineLimit(3)
                            .lineSpacing(3)
                    }
                }

                Spacer(minLength: 0)

                if let poster = (tmdbBackdrop?.poster?.nonEmpty ?? result.poster.nonEmpty) {
                    AsyncPanelImage(urlString: poster)
                        .frame(width: 92, height: 138)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                        .overlay {
                            RoundedRectangle(cornerRadius: 8)
                                .stroke(.white.opacity(0.28), lineWidth: 1)
                        }
                        .shadow(radius: 14)
                }
            }
            .padding(18)
        }
        .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
    }

    private var overviewTab: some View {
        VStack(alignment: .leading, spacing: 10) {
            AppSectionHeader(title: "概览")
            chipRow(foreground: .primary)

            if let overview {
                Text(overview)
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .lineSpacing(3)
                    .textSelection(.enabled)
            }

            if let quickInfo {
                VStack(alignment: .leading, spacing: 6) {
                    if !quickInfo.genres.isEmpty {
                        infoLine("类型", quickInfo.genres)
                    }
                    if !quickInfo.directors.isEmpty {
                        infoLine("导演", quickInfo.directors)
                    }
                    if !quickInfo.cast.isEmpty {
                        infoLine("主演", Array(quickInfo.cast.prefix(8)))
                    }
                }
                .font(.caption)
                .foregroundStyle(.secondary)
            }
        }
    }

    private var commentsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            AppSectionHeader(title: "短评")
            ForEach(comments.prefix(3)) { comment in
                Text(comment.author.isEmpty ? comment.content : "\(comment.author)：\(comment.content)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
                    .textSelection(.enabled)
            }
        }
    }

    private var recommendationsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            AppSectionHeader(title: "推荐")
            ScrollView(.horizontal) {
                HStack(spacing: 10) {
                    ForEach(recommendations.prefix(8)) { item in
                        VStack(alignment: .leading, spacing: 6) {
                            AsyncPanelImage(urlString: item.cover)
                                .frame(width: 88, height: 126)
                                .clipShape(RoundedRectangle(cornerRadius: 8))
                            Text(item.title)
                                .font(.caption.weight(.semibold))
                                .lineLimit(2)
                                .frame(width: 88, alignment: .leading)
                            if !item.rating.isEmpty {
                                Text(item.rating)
                                    .font(.caption2)
                                    .foregroundStyle(.yellow)
                            }
                        }
                    }
                }
            }
            .scrollIndicators(.hidden)
        }
    }

    private func chipRow(foreground: Color) -> some View {
        HStack(spacing: 8) {
            if !result.sourceName.isEmpty || !result.source.isEmpty {
                panelChip(result.sourceName.isEmpty ? result.source : result.sourceName, foreground: foreground)
            }
            if let year = (tmdbBackdrop?.year?.nonEmpty ?? result.year.nonEmpty) {
                panelChip(year, foreground: foreground)
            }
            if let ratingText {
                panelChip(ratingText, foreground: foreground, emphasized: true)
            }
            if let className = result.className?.nonEmpty {
                panelChip(className, foreground: foreground, emphasized: true)
            }
            if result.episodes.count > 1 {
                panelChip(result.episodeTitle(for: episodeIndex), foreground: foreground)
                panelChip("共\(result.episodes.count)集", foreground: foreground)
            }
            if let seasons = tmdbBackdrop?.numberOfSeasons, seasons > 1 {
                panelChip("共 \(seasons) 季", foreground: foreground)
            }
        }
    }

    private func panelChip(_ text: String, foreground: Color, emphasized: Bool = false) -> some View {
        Text(text)
            .font(.caption2.weight(.medium))
            .foregroundStyle(emphasized ? AppTheme.accent : foreground.opacity(0.86))
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background(foreground == .white ? Color.white.opacity(0.16) : AppTheme.surface)
            .clipShape(Capsule())
    }

    private func infoLine(_ label: String, _ values: [String]) -> some View {
        Text("\(label)：\(values.joined(separator: "、"))")
    }
}

private struct AsyncPanelImage: View {
    let urlString: String?
    var systemImage = "film"

    var body: some View {
        ZStack {
            Color.black.opacity(0.08)
            if let urlString, let url = URL(string: urlString), !urlString.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        placeholder
                    }
                }
            } else {
                placeholder
            }
        }
    }

    private var placeholder: some View {
        Image(systemName: systemImage)
            .font(.title2)
            .foregroundStyle(.secondary)
    }
}

private extension String {
    var nonEmpty: String? {
        isEmpty ? nil : self
    }
}

private extension PlayRecord {
    static func formatForDisplay(_ seconds: Int) -> String {
        let clamped = max(seconds, 0)
        let hours = clamped / 3600
        let minutes = (clamped % 3600) / 60
        let seconds = clamped % 60
        if hours > 0 {
            return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
        }
        return String(format: "%02d:%02d", minutes, seconds)
    }
}
