import SwiftUI

struct DetailView: View {
    let result: SearchResult
    var isFavorited: Bool = false
    var onToggleFavorite: ((SearchResult) -> Void)?
    var metadataProvider: MetadataEnhancementProviding?
    let onPlay: (SearchResult, Int, URL) -> Void
    @State private var enhancementStore: DetailEnhancementStore?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                headerSection
                descriptionSection
                if let enhancementStore {
                    DetailEnhancementsView(store: enhancementStore)
                }
                episodesSection
                Spacer()
            }
            .padding()
        }
        .task(id: result.id) {
            guard let metadataProvider else { return }
            let store = DetailEnhancementStore(provider: metadataProvider)
            enhancementStore = store
            await store.load(
                title: result.title,
                year: result.year,
                sourceType: result.typeName ?? result.className,
                doubanId: result.doubanID
            )
        }
    }

    private var headerSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(result.title)
                .font(.title2)
                .bold()

            HStack(spacing: 12) {
                Text(result.sourceName)
                    .font(.caption)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.secondary.opacity(0.2))
                    .cornerRadius(4)

                Text(result.year)
                    .font(.caption)
                    .foregroundStyle(.secondary)

                if let typeName = result.typeName, !typeName.isEmpty {
                    Text(typeName)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                if let onToggleFavorite {
                    Button {
                        onToggleFavorite(result)
                    } label: {
                        Label(isFavorited ? "取消收藏" : "收藏", systemImage: isFavorited ? "heart.fill" : "heart")
                    }
                    .labelStyle(.iconOnly)
                    .help(isFavorited ? "取消收藏" : "收藏")
                }
            }
        }
    }

    private var descriptionSection: some View {
        Group {
            if let desc = result.description, !desc.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text("简介")
                        .font(.headline)
                    Text(desc)
                        .font(.body)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }
            }
        }
    }

    private var episodesSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("剧集 (\(result.episodes.count))")
                .font(.headline)

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
                                .background(.regularMaterial)
                                .cornerRadius(6)
                        }
                        .buttonStyle(.plain)
                        .help(result.episodes[index])
                    }
                }
            }
        }
    }
}
