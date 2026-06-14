import SwiftUI

@main
struct SeleneNativeApp: App {
    @State private var sessionStore = SessionStore()
    @State private var favoritesStore = FavoritesStore()
    @State private var historyStore = HistoryStore()
    @State private var themeStore = ThemeStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(sessionStore)
                .environment(favoritesStore)
                .environment(historyStore)
                .environment(themeStore)
                .preferredColorScheme(themeStore.colorScheme)
        }
        .windowStyle(.titleBar)
        .windowResizability(.contentSize)
    }
}
