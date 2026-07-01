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
                .frame(width: 70, height: 96)
                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
                .overlay(alignment: .topTrailing) {
                    if let progress, progress > 0 {
                        Text("\(Int(progress * 100))%")
                            .font(.system(size: 10, weight: .semibold))
                            .foregroundStyle(.white)
                            .padding(.horizontal, 5)
                            .padding(.vertical, 2)
                            .background(Color.black.opacity(0.58))
                            .clipShape(Capsule())
                            .padding(5)
                    }
                }

            VStack(alignment: .leading, spacing: 6) {
                Text(title)
                    .font(.callout.weight(.semibold))
                    .lineLimit(2)

                HStack(spacing: 6) {
                    if !sourceName.isEmpty {
                        Text(sourceName)
                            .font(.caption2.weight(.medium))
                            .foregroundStyle(.primary.opacity(0.72))
                            .padding(.horizontal, 7)
                            .padding(.vertical, 3)
                            .background(AppTheme.softAccent)
                            .clipShape(Capsule())
                    }
                    if !year.isEmpty {
                        Text(year)
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }

                HStack(spacing: 8) {
                    if let subtitle, !subtitle.isEmpty {
                        Text(subtitle)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }

                if let progress {
                    ProgressView(value: progress)
                        .controlSize(.small)
                        .tint(AppTheme.accent)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(10)
        .frame(maxWidth: .infinity, minHeight: 116, alignment: .leading)
        .background(AppTheme.elevatedSurface)
        .overlay {
            RoundedRectangle(cornerRadius: AppTheme.radius)
                .stroke(AppTheme.border, lineWidth: 1)
        }
        .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
    }

    @ViewBuilder
    private var posterView: some View {
        if !poster.isEmpty, let url = URL(string: poster) {
            AsyncImage(url: url) { phase in
                switch phase {
                case .empty:
                    placeholder
                case .success(let image):
                    image.resizable().scaledToFit()
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
            AppTheme.surface
            Image(systemName: "film")
                .font(.title2)
                .foregroundStyle(.secondary.opacity(0.75))
        }
    }
}
