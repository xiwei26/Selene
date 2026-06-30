import SwiftUI

enum AppTheme {
    static let radius: CGFloat = 12
    static let pagePadding: CGFloat = 20
    static let sectionSpacing: CGFloat = 18
    static let accent = Color(red: 0.05, green: 0.65, blue: 0.39)
    static let accentEnd = Color(red: 0.05, green: 0.73, blue: 0.63)

    static var pageBackground: Color {
        Color(red: 0.91, green: 0.96, blue: 0.96)
    }

    static var surface: Color {
        Color.white.opacity(0.58)
    }

    static var elevatedSurface: Color {
        Color.white.opacity(0.74)
    }

    static var border: Color {
        Color.white.opacity(0.55)
    }

    static var softAccent: Color {
        accent.opacity(0.13)
    }

    static var warningAccent: Color {
        Color.orange.opacity(0.14)
    }

    static var accentGradient: LinearGradient {
        LinearGradient(
            colors: [accent, accentEnd],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }
}

struct AppBackdrop: View {
    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.84, green: 0.92, blue: 0.97),
                    Color(red: 0.92, green: 0.96, blue: 0.93),
                    Color(red: 0.88, green: 0.92, blue: 0.90)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            HStack(spacing: 0) {
                ForEach(0..<8, id: \.self) { index in
                    Rectangle()
                        .fill(
                            LinearGradient(
                                colors: [
                                    Color(red: 0.12, green: 0.28, blue: 0.29).opacity(index.isMultiple(of: 2) ? 0.10 : 0.04),
                                    Color(red: 0.07, green: 0.42, blue: 0.28).opacity(index.isMultiple(of: 3) ? 0.08 : 0.03),
                                    .clear
                                ],
                                startPoint: .top,
                                endPoint: .bottom
                            )
                        )
                        .frame(maxWidth: .infinity)
                }
            }
            .blur(radius: 18)
            .opacity(0.75)

            LinearGradient(
                colors: [.white.opacity(0.42), .white.opacity(0.08), .black.opacity(0.06)],
                startPoint: .top,
                endPoint: .bottom
            )
        }
        .ignoresSafeArea()
    }
}

struct AppPageHeader: View {
    let title: String
    var subtitle: String?
    var systemImage: String?

    var body: some View {
        HStack(alignment: .center, spacing: 12) {
            if let systemImage {
                Image(systemName: systemImage)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundStyle(.white)
                    .frame(width: 36, height: 36)
                    .background(AppTheme.accentGradient)
                    .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
            }

            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.title2.weight(.semibold))
                    .lineLimit(1)
                if let subtitle, !subtitle.isEmpty {
                    Text(subtitle)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
                }
            }

            Spacer(minLength: 0)
        }
    }
}

struct AppSectionHeader: View {
    let title: String
    var subtitle: String?
    var count: Int?

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.headline)
                if let subtitle, !subtitle.isEmpty {
                    Text(subtitle)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            if let count {
                Text("\(count)")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(.secondary)
                    .padding(.horizontal, 7)
                    .padding(.vertical, 2)
                    .background(AppTheme.surface)
                    .clipShape(Capsule())
            }

            Spacer(minLength: 0)
        }
    }
}

struct AppSurfaceModifier: ViewModifier {
    var padding: CGFloat = 14

    func body(content: Content) -> some View {
        content
            .padding(padding)
            .background(.ultraThinMaterial)
            .background(AppTheme.elevatedSurface)
            .overlay {
                RoundedRectangle(cornerRadius: AppTheme.radius)
                    .stroke(AppTheme.border, lineWidth: 1)
            }
            .shadow(color: Color(red: 0.05, green: 0.18, blue: 0.16).opacity(0.10), radius: 18, x: 0, y: 10)
            .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
    }
}

extension View {
    func appSurface(padding: CGFloat = 14) -> some View {
        modifier(AppSurfaceModifier(padding: padding))
    }

    func appPageBackground() -> some View {
        background {
            AppBackdrop()
        }
    }
}
