import SwiftUI

@main
struct SeleneApp: App {
    @State private var appModel = AppModel()
    @State private var playerCoordinator = PlayerCoordinator()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(appModel)
                .environment(playerCoordinator)
                .tint(AppTheme.accent)
                .preferredColorScheme(appModel.preferredColorScheme)
        }
    }
}