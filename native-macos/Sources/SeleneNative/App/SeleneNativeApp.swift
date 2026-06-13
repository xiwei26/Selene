import SwiftUI

@main
struct SeleneNativeApp: App {
    @State private var sessionStore = SessionStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(sessionStore)
        }
        .windowStyle(.titleBar)
        .windowResizability(.contentSize)
    }
}
