import SwiftUI

struct RootView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        Group {
            if appModel.isAuthenticated {
                MainSplitView()
            } else {
                LoginView()
            }
        }
        .animation(.snappy, value: appModel.isAuthenticated)
    }
}