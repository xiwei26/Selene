import SwiftUI

struct PosterCard: View {
    let movie: DoubanMovie

    var body: some View {
        VStack(alignment: .leading, spacing: 9) {
            RemotePosterView(urlString: movie.poster)

            Text(movie.title)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.primary)
                .lineLimit(1)

            HStack(spacing: 5) {
                if let rate = movie.rate, !rate.isEmpty {
                    Image(systemName: "star.fill")
                        .foregroundStyle(.orange)
                    Text(rate)
                }
                if !movie.year.isEmpty {
                    Text(movie.year)
                }
            }
            .font(.caption)
            .foregroundStyle(.secondary)
            .lineLimit(1)
        }
        .contentShape(Rectangle())
        .accessibilityElement(children: .combine)
        .accessibilityLabel([movie.title, movie.year, movie.rate].compactMap { $0 }.joined(separator: "，"))
    }
}

struct SearchResultRow: View {
    let result: SearchResult

    var body: some View {
        HStack(alignment: .top, spacing: 14) {
            RemotePosterView(urlString: result.poster)
                .frame(width: 78)

            VStack(alignment: .leading, spacing: 7) {
                Text(result.title)
                    .font(.headline)
                    .foregroundStyle(.primary)
                    .lineLimit(2)

                if !result.metadata.isEmpty {
                    Text(result.metadata)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
                }

                if let description = result.description, !description.isEmpty {
                    Text(description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(3)
                }

                Label("\(result.episodes.count) 个播放地址", systemImage: "play.rectangle.on.rectangle")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(AppTheme.accent)
            }

            Spacer(minLength: 0)
        }
        .padding(.vertical, 6)
        .contentShape(Rectangle())
    }
}

struct PageHeader: View {
    let title: String
    let subtitle: String
    let systemImage: String

    var body: some View {
        HStack(alignment: .center, spacing: 16) {
            Image(systemName: systemImage)
                .font(.title2.weight(.semibold))
                .foregroundStyle(.white)
                .frame(width: 48, height: 48)
                .background(AppTheme.accentGradient, in: RoundedRectangle(cornerRadius: 14))

            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.largeTitle.bold())
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}