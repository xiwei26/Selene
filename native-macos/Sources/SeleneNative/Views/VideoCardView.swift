import SwiftUI

struct VideoCardView: View {
    let title: String
    let poster: String
    let sourceName: String
    let year: String
    var subtitle: String?
    var progress: Double?

    var body: some View {
        HStack(spacing: 12) {
            posterView
                .frame(width: 64, height: 88)
                .clipShape(RoundedRectangle(cornerRadius: 6))

            VStack(alignment: .leading, spacing: 6) {
                Text(title)
                    .font(.body)
                    .lineLimit(2)
                Text(sourceName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                HStack(spacing: 8) {
                    if !year.isEmpty {
                        Text(year)
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                    if let subtitle, !subtitle.isEmpty {
                        Text(subtitle)
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
                if let progress {
                    ProgressView(value: progress)
                        .controlSize(.small)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(.vertical, 4)
    }

    @ViewBuilder
    private var posterView: some View {
        if !poster.isEmpty, let url = URL(string: poster) {
            AsyncImage(url: url) { phase in
                switch phase {
                case .empty:
                    placeholder
                case .success(let image):
                    image.resizable().scaledToFill()
                case .failure:
                    placeholder
                @unknown default:
                    placeholder
                }
            }
        } else {
            placeholder
        }
    }

    private var placeholder: some View {
        ZStack {
            Color.secondary.opacity(0.18)
            Image(systemName: "film")
                .foregroundStyle(.secondary)
        }
    }
}
