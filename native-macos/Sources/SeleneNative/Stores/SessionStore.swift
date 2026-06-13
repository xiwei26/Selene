import SwiftUI
import Foundation

final class SessionStore: ObservableObject {
    @Published var session: LoginSession?
    @Published var errorMessage: String?

    private let userDefaultsKey = "selene_session_data"

    init() {
        loadSession()
    }

    var isLoggedIn: Bool {
        session != nil
    }

    func login(session: LoginSession) {
        self.session = session
        self.errorMessage = nil
        persistSession(session)
    }

    func logout() {
        session = nil
        errorMessage = nil
        UserDefaults.standard.removeObject(forKey: userDefaultsKey)
    }

    func setError(_ message: String?) {
        errorMessage = message
    }

    private func persistSession(_ session: LoginSession) {
        do {
            let data = try JSONEncoder().encode(session)
            UserDefaults.standard.set(data, forKey: userDefaultsKey)
        } catch {
            // Persistence failure is non-fatal
        }
    }

    private func loadSession() {
        guard let data = UserDefaults.standard.data(forKey: userDefaultsKey) else { return }
        do {
            let session = try JSONDecoder().decode(LoginSession.self, from: data)
            self.session = session
        } catch {
            // Corrupted data
            UserDefaults.standard.removeObject(forKey: userDefaultsKey)
        }
    }
}
