import SwiftUI

enum AppTheme {
    static let accent = Color(red: 0.11, green: 0.69, blue: 0.42)
    static let accentGradient = LinearGradient(
        colors: [
            Color(red: 0.08, green: 0.75, blue: 0.52),
            Color(red: 0.08, green: 0.55, blue: 0.78)
        ],
        startPoint: .topLeading,
        endPoint: .bottomTrailing
    )
    static let pagePadding: CGFloat = 24
    static let sectionSpacing: CGFloat = 28
    static let cornerRadius: CGFloat = 18
}

struct AppBackground: View {
    var body: some View {
        ZStack {
            Color(.systemGroupedBackground)
            LinearGradient(
                colors: [
                    AppTheme.accent.opacity(0.09),
                    Color.clear,
                    Color.blue.opacity(0.04)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
        }
        .ignoresSafeArea()
    }
}

extension View {
    func appSurface(padding: CGFloat = 16) -> some View {
        self
            .padding(padding)
            .background(.regularMaterial, in: RoundedRectangle(cornerRadius: AppTheme.cornerRadius))
            .overlay {
                RoundedRectangle(cornerRadius: AppTheme.cornerRadius)
                    .stroke(Color.primary.opacity(0.07), lineWidth: 1)
            }
    }
}