import SwiftUI

struct DetailEnhancementsView: View {
    @Bindable var store: DetailEnhancementStore

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            backdropSection
            quickInfoSection
            commentsSection
            recommendationsSection
            trailerSection
        }
    }

    @ViewBuilder
    private var backdropSection: some View {
        if let value = store.backdrop?.backdropUrl, let url = URL(string: value) {
            AsyncImage(url: url) { phase in
                switch phase {
                case .empty:
                    ProgressView().frame(maxWidth: .infinity, minHeight: 180)
                case .success(let image):
                    image.resizable()
                        .scaledToFill()
                        .frame(maxWidth: .infinity, minHeight: 180, maxHeight: 260)
                        .clipped()
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                case .failure:
                    EmptyView()
                @unknown default:
                    EmptyView()
                }
            }
        }
    }

    @ViewBuilder
    private var quickInfoSection: some View {
        if let quickInfo = store.quickInfo {
            VStack(alignment: .leading, spacing: 6) {
                Text("Douban").font(.headline)
                if let rating = quickInfo.rating, !rating.isEmpty {
                    Text("Rating \(rating)").font(.caption).foregroundStyle(.secondary)
                }
                if let summary = quickInfo.summary, !summary.isEmpty {
                    Text(summary).foregroundStyle(.secondary).textSelection(.enabled)
                }
            }
        }
    }

    @ViewBuilder
    private var commentsSection: some View {
        if !store.comments.isEmpty {
            VStack(alignment: .leading, spacing: 8) {
                Text("Comments").font(.headline)
                ForEach(store.comments.prefix(5)) { comment in
                    VStack(alignment: .leading, spacing: 4) {
                        Text(comment.username).font(.caption).bold()
                        Text(comment.content).font(.caption).foregroundStyle(.secondary)
                    }
                    Divider()
                }
            }
        }
    }

    @ViewBuilder
    private var recommendationsSection: some View {
        if !store.recommendations.isEmpty {
            VStack(alignment: .leading, spacing: 8) {
                Text("Related").font(.headline)
                ScrollView(.horizontal) {
                    LazyHStack(spacing: 12) {
                        ForEach(store.recommendations.prefix(10)) { movie in
                            VideoCardView(
                                title: movie.title,
                                poster: movie.poster,
                                sourceName: "Douban",
                                year: movie.year,
                                subtitle: movie.rate
                            )
                            .frame(width: 260)
                        }
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var trailerSection: some View {
        if let value = store.trailer?.trailerUrl, let url = URL(string: value) {
            Link(destination: url) {
                Label("Trailer", systemImage: "play.rectangle")
            }
        }
    }
}
