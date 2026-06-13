import SwiftUI

struct RootView: View {
    @Environment(SessionStore.self) private var sessionStore

    var body: some View {
        Group {
            if sessionStore.isLoggedIn {
                MainView()
            } else {
                LoginView()
            }
        }
        .frame(minWidth: 800, minHeight: 500)
    }
}
