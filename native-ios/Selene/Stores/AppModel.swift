import Foundation
import Observation
import SwiftUI

@MainActor
@Observable
final class AppModel {
    enum ThemeMode: String, CaseIterable, Identifiable {
        case system
        case light
        case dark

        var id: String { rawValue }
        var title: String {
            switch self {
            case .system: "跟随系统"
            case .light: "浅色"
            case .dark: "深色"
            }
        }
    }

    private(set) var session: UserSession?
    private(set) var client: LunaTVClient?
    var authenticationError: String?
    var favorites: [FavoriteItem] = []
    var playRecords: [PlayRecord] = []
    var searchHistory: [String] = []
    var libraryError: String?
    var themeMode: ThemeMode {
        didSet { UserDefaults.standard.set(themeMode.rawValue, forKey: themeKey) }
    }

    /// Legacy UserDefaults key; only used to migrate older installs into Keychain.
    private let legacyPersistenceKey = "selene.native-ios.session"
    private let keychainAccount = "session"
    private let themeKey = "selene.native-ios.theme"

    init() {
        themeMode = ThemeMode(rawValue: UserDefaults.standard.string(forKey: themeKey) ?? "") ?? .system
        restoreSession()
    }

    var isAuthenticated: Bool {
        session != nil && client != nil
    }

    func signIn(serverAddress: String, username: String, password: String) async {
        authenticationError = nil

        guard let serverURL = URLNormalizer.normalize(serverAddress) else {
            authenticationError = APIError.invalidServerURL.localizedDescription
            return
        }

        do {
            let loginClient = LunaTVClient(baseURL: serverURL)
            let newSession = try await loginClient.login(username: username, password: password)
            install(newSession)
        } catch {
            authenticationError = error.localizedDescription
        }
    }

    func signOut() {
        session = nil
        client = nil
        authenticationError = nil
        favorites = []
        playRecords = []
        searchHistory = []
        libraryError = nil
        KeychainStore.delete(account: keychainAccount)
        UserDefaults.standard.removeObject(forKey: legacyPersistenceKey)
    }

    func signInLocally(subscription: String) {
        authenticationError = nil
        guard let content = SubscriptionService.parse(subscription) else {
            authenticationError = "订阅内容无法解析"
            return
        }
        install(
            UserSession(
                serverURL: URL(string: "http://localhost")!,
                username: "本地模式",
                cookie: "",
                isLocalMode: true,
                localSearchResources: content.searchResources,
                localLiveSources: content.liveSources
            )
        )
    }

    func refreshLibrary() async {
        guard let client = serverClient else { return }
        libraryError = nil
        async let favoritesRequest = client.favorites()
        async let recordsRequest = client.playRecords()
        async let historyRequest = client.searchHistory()
        do {
            favorites = try await favoritesRequest
            playRecords = try await recordsRequest
            searchHistory = try await historyRequest
        } catch {
            libraryError = error.localizedDescription
        }
    }

    /// Server-backed library and admin routes are unavailable in local
    /// subscription mode, where `client` points at a placeholder base URL.
    var isLocalMode: Bool {
        session?.isLocalMode == true
    }

    private var serverClient: LunaTVClient? {
        isLocalMode ? nil : client
    }

    func addFavorite(_ result: SearchResult) async {
        guard let client = serverClient else { return }
        do {
            try await client.addFavorite(result)
            favorites = try await client.favorites()
        } catch {
            libraryError = error.localizedDescription
        }
    }

    func removeFavorite(_ item: FavoriteItem) async {
        guard let client = serverClient else { return }
        do {
            try await client.removeFavorite(source: item.source, id: splitMediaKey(item.id).itemID)
            favorites.removeAll { $0.id == item.id }
        } catch {
            libraryError = error.localizedDescription
        }
    }

    func savePlayRecord(_ record: PlayRecord) async {
        guard let client = serverClient else { return }
        do {
            try await client.savePlayRecord(record)
            playRecords.removeAll { $0.id == record.id }
            playRecords.insert(record, at: 0)
        } catch {
            libraryError = error.localizedDescription
        }
    }

    func clearHistory() async {
        guard let client = serverClient else { return }
        do {
            try await client.clearPlayRecords()
            playRecords = []
        } catch {
            libraryError = error.localizedDescription
        }
    }

    func deleteSearchHistory(_ query: String) async {
        do {
            if session?.isLocalMode != true {
                try await client?.deleteSearchHistory(query)
            }
            searchHistory.removeAll { $0 == query }
        } catch {
            libraryError = error.localizedDescription
        }
    }

    func clearSearchHistory() async {
        do {
            if session?.isLocalMode != true {
                try await client?.clearSearchHistory()
            }
            searchHistory = []
        } catch {
            libraryError = error.localizedDescription
        }
    }

    /// Clears both the shared URL cache and the dedicated poster disk/memory caches.
    func clearCaches() {
        URLCache.shared.removeAllCachedResponses()
        PosterImageCache.shared.clear()
    }

    var preferredColorScheme: ColorScheme? {
        switch themeMode {
        case .system: nil
        case .light: .light
        case .dark: .dark
        }
    }

    private func install(_ session: UserSession) {
        self.session = session
        client = LunaTVClient(
            baseURL: session.serverURL,
            cookie: session.cookie
        )
        authenticationError = nil
        // Never carry one account's library into another.
        favorites = []
        playRecords = []
        searchHistory = []
        libraryError = nil

        if let data = try? JSONEncoder().encode(session) {
            try? KeychainStore.save(data: data, account: keychainAccount)
        }
        // Drop any legacy plaintext copy left by older builds.
        UserDefaults.standard.removeObject(forKey: legacyPersistenceKey)
    }

    private func restoreSession() {
        if let data = try? KeychainStore.load(account: keychainAccount),
           let restored = try? JSONDecoder().decode(UserSession.self, from: data) {
            install(restored)
            return
        }

        // One-shot migration from pre-Keychain builds.
        guard let data = UserDefaults.standard.data(forKey: legacyPersistenceKey),
              let restored = try? JSONDecoder().decode(UserSession.self, from: data) else {
            return
        }
        install(restored)
    }
}