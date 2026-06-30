import SwiftUI

struct DetailView: View {
    let result: SearchResult
    var isFavorited: Bool = false
    var onToggleFavorite: ((SearchResult) -> Void)?
    let onPlay: (SearchResult, Int, URL) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                headerSection
                descriptionSection
                episodesSection
                Spacer()
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
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
}
