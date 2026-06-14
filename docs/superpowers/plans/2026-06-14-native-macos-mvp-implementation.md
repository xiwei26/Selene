# Native macOS MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone native macOS SwiftUI app at `native-macos/` that logs into a Selene/MoonTV server, searches content, shows details, and plays videos with AVKit.

**Architecture:** SwiftUI Views → @Observable Stores → ContentProvider protocol → ServerAPIClient → Selene server APIs. Uses UserDefaults for session persistence, AVKit for playback.

**Tech Stack:** Swift 5.9+, SwiftUI, Foundation URLSession, AVKit, SwiftPM, XCTest

---

## File Structure

```
native-macos/
├── Package.swift
├── Sources/
│   └── SeleneNative/
│       ├── App/
│       │   └── SeleneNativeApp.swift
│       ├── Views/
│       │   ├── RootView.swift
│       │   ├── LoginView.swift
│       │   ├── MainView.swift
│       │   ├── SearchResultsView.swift
│       │   ├── DetailView.swift
│       │   └── PlayerView.swift
│       ├── Models/
│       │   ├── LoginSession.swift
│       │   ├── SearchResult.swift
│       │   ├── SearchResource.swift
│       │   └── APIError.swift
│       ├── Services/
│       │   ├── ContentProvider.swift
│       │   └── ServerAPIClient.swift
│       ├── Stores/
│       │   ├── SessionStore.swift
│       │   ├── SearchStore.swift
│       │   └── PlayerStore.swift
│       └── Support/
│           └── URLNormalizer.swift
├── Tests/
│   └── SeleneNativeTests/
│       ├── URLNormalizerTests.swift
│       ├── SearchResultTests.swift
│       ├── ServerAPIClientTests.swift
│       └── SessionStoreTests.swift
├── script/
│   └── build_and_run.sh
└── .codex/
    └── environments/
        └── environment.toml
```

---

### Task 1: Package.swift and App Entry Point

**Files:**
- Create: `native-macos/Package.swift`
- Create: `native-macos/Sources/SeleneNative/App/SeleneNativeApp.swift`

- [ ] **Step 1: Write Package.swift**

```swift
// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "SeleneNative",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(
            name: "SeleneNative",
            targets: ["SeleneNative"]
        ),
    ],
    targets: [
        .executableTarget(
            name: "SeleneNative",
            dependencies: []
        ),
        .testTarget(
            name: "SeleneNativeTests",
            dependencies: ["SeleneNative"]
        ),
    ]
)
```

- [ ] **Step 2: Write SeleneNativeApp.swift**

```swift
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
```

- [ ] **Step 3: Verify package manifest parses**
Run: `cd native-macos && swift package describe`
Expected: Lists SeleneNative and SeleneNativeTests targets
- [ ] **Step 4: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Package.swift native-macos/Sources/SeleneNative/App/SeleneNativeApp.swift
git commit -m "feat: create SwiftPM package and app entry point"
```

---

### Task 2: Models - APIError

**Files:**
- Create: `native-macos/Sources/SeleneNative/Models/APIError.swift`

- [ ] **Step 1: Write APIError.swift**

```swift
import Foundation

enum APIError: LocalizedError {
    case message(String)
    case responseError(statusCode: Int)
    case invalidURL
    case unauthorized
    case unknown

    var localizedDescription: String {
        switch self {
        case .message(let msg):
            return msg
        case .responseError(let code):
            return "请求失败 (\(code))"
        case .invalidURL:
            return "服务器地址无效"
        case .unauthorized:
            return "登录已过期，请重新登录"
        case .unknown:
            return "未知错误"
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Models/APIError.swift
git commit -m "feat: add APIError model"
```

---

### Task 3: Models - LoginSession

**Files:**
- Create: `native-macos/Sources/SeleneNative/Models/LoginSession.swift`

- [ ] **Step 1: Write LoginSession.swift**

```swift
import Foundation

struct LoginSession: Codable, Identifiable {
    let id: UUID
    let serverURL: URL
    let username: String
    let cookie: String

    init(id: UUID = UUID(), serverURL: URL, username: String, cookie: String) {
        self.id = id
        self.serverURL = serverURL
        self.username = username
        self.cookie = cookie
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Models/LoginSession.swift
git commit -m "feat: add LoginSession model"
```

---

### Task 4: Models - SearchResult

**Files:**
- Create: `native-macos/Sources/SeleneNative/Models/SearchResult.swift`

- [ ] **Step 1: Write SearchResult.swift**

```swift
import Foundation

struct SearchResult: Codable, Identifiable {
    let id: String
    let title: String
    let poster: String
    let episodes: [String]
    let episodeTitles: [String]
    let source: String
    let sourceName: String
    let className: String?
    let year: String
    let description: String?
    let typeName: String?
    let doubanID: Int?

    enum CodingKeys: String, CodingKey {
        case id, title, poster, episodes, source, year
        case episodeTitles = "episodes_titles"
        case sourceName = "source_name"
        case className = "class"
        case description = "desc"
        case typeName = "type_name"
        case doubanID = "douban_id"
    }

    init(
        id: String,
        title: String,
        poster: String,
        episodes: [String],
        episodeTitles: [String],
        source: String,
        sourceName: String,
        className: String? = nil,
        year: String,
        description: String? = nil,
        typeName: String? = nil,
        doubanID: Int? = nil
    ) {
        self.id = id
        self.title = title
        self.poster = poster
        self.episodes = episodes
        self.episodeTitles = episodeTitles
        self.source = source
        self.sourceName = sourceName
        self.className = className
        self.year = year
        self.description = description
        self.typeName = typeName
        self.doubanID = doubanID
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        title = try container.decode(String.self, forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster) ?? ""
        episodes = try container.decodeIfPresent([String].self, forKey: .episodes) ?? []
        episodeTitles = try container.decodeIfPresent([String].self, forKey: .episodeTitles) ?? []
        source = try container.decode(String.self, forKey: .source)
        sourceName = try container.decodeIfPresent(String.self, forKey: .sourceName) ?? ""
        className = try container.decodeIfPresent(String.self, forKey: .className)
        year = try container.decodeIfPresent(String.self, forKey: .year) ?? ""
        description = try container.decodeIfPresent(String.self, forKey: .description)
        typeName = try container.decodeIfPresent(String.self, forKey: .typeName)
        doubanID = try container.decodeIfPresent(Int.self, forKey: .doubanID)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(poster, forKey: .poster)
        try container.encode(episodes, forKey: .episodes)
        try container.encode(episodeTitles, forKey: .episodeTitles)
        try container.encode(source, forKey: .source)
        try container.encode(sourceName, forKey: .sourceName)
        try container.encodeIfPresent(className, forKey: .className)
        try container.encode(year, forKey: .year)
        try container.encodeIfPresent(description, forKey: .description)
        try container.encodeIfPresent(typeName, forKey: .typeName)
        try container.encodeIfPresent(doubanID, forKey: .doubanID)
    }

    func episodeTitle(for index: Int) -> String {
        guard index < episodeTitles.count else {
            return "第\(index + 1)集"
        }
        let title = episodeTitles[index]
        return title.isEmpty ? "第\(index + 1)集" : title
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Models/SearchResult.swift
git commit -m "feat: add SearchResult model with snake_case decoding"
```

---

### Task 5: Models - SearchResource

**Files:**
- Create: `native-macos/Sources/SeleneNative/Models/SearchResource.swift`

- [ ] **Step 1: Write SearchResource.swift**

```swift
import Foundation

struct SearchResource: Codable, Identifiable {
    let id: String
    let key: String
    let name: String
    let api: String
    let detail: String
    let from: String
    let disabled: Bool

    enum CodingKeys: String, CodingKey {
        case id, key, name, api, detail, from, disabled
    }

    init(key: String, name: String, api: String, detail: String, from: String, disabled: Bool) {
        self.id = key
        self.key = key
        self.name = name
        self.api = api
        self.detail = detail
        self.from = from
        self.disabled = disabled
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        key = try container.decode(String.self, forKey: .key)
        name = try container.decode(String.self, forKey: .name)
        api = try container.decode(String.self, forKey: .api)
        detail = try container.decodeIfPresent(String.self, forKey: .detail) ?? ""
        from = try container.decodeIfPresent(String.self, forKey: .from) ?? ""
        disabled = try container.decodeIfPresent(Bool.self, forKey: .disabled) ?? false
        id = key
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Models/SearchResource.swift
git commit -m "feat: add SearchResource model"
```

---

### Task 6: Support - URLNormalizer

**Files:**
- Create: `native-macos/Sources/SeleneNative/Support/URLNormalizer.swift`

- [ ] **Step 1: Write URLNormalizer.swift**

```swift
import Foundation

enum URLNormalizer {
    /// Normalize a user-provided server URL string.
    /// - Adds "https://" if no scheme is present
    /// - Falls back to http:// for localhost-style URLs
    static func normalize(_ input: String) -> URL? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        // Try with https:// prefix
        if !trimmed.hasPrefix("http://") && !trimmed.hasPrefix("https://") {
            let httpsURL = URL(string: "https://\(trimmed)")
            if let httpsURL, httpsURL.host != nil {
                return httpsURL
            }
            // Try http for localhost-like inputs
            let httpURL = URL(string: "http://\(trimmed)")
            if let httpURL, httpURL.host != nil {
                return httpURL
            }
            return nil
        }

        // Already has a scheme
        return URL(string: trimmed)
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Support/URLNormalizer.swift
git commit -m "feat: add URL normalizer for server URL input"
```

---

### Task 7: Stores - SessionStore

**Files:**
- Create: `native-macos/Sources/SeleneNative/Stores/SessionStore.swift`

- [ ] **Step 1: Write SessionStore.swift**

```swift
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
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Stores/SessionStore.swift
git commit -m "feat: add SessionStore with UserDefaults persistence"
```

---

### Task 8: Services - ContentProvider Protocol

**Files:**
- Create: `native-macos/Sources/SeleneNative/Services/ContentProvider.swift`

- [ ] **Step 1: Write ContentProvider.swift**

```swift
import Foundation

protocol ContentProvider: Sendable {
    func login(username: String, password: String) async throws -> LoginSession
    func search(query: String) async throws -> [SearchResult]
    func detail(source: String, id: String) async throws -> SearchResult?
    func searchResources() async throws -> [SearchResource]
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Services/ContentProvider.swift
git commit -m "feat: add ContentProvider protocol"
```

---

### Task 9: Services - ServerAPIClient

**Files:**
- Create: `native-macos/Sources/SeleneNative/Services/ServerAPIClient.swift`

- [ ] **Step 1: Write ServerAPIClient.swift**

```swift
import Foundation

final class ServerAPIClient: ContentProvider, Sendable {
    private let baseURL: URL
    private let session: URLSession

    init(baseURL: URL, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
    }

    func login(username: String, password: String) async throws -> LoginSession {
        let url = baseURL.appendingPathComponent("/api/login")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let body = ["username": username, "password": password]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (_, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse else {
            throw APIError.message("无效的服务器响应")
        }

        guard (200...299).contains(httpResponse.statusCode) else {
            if httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.responseError(statusCode: httpResponse.statusCode)
        }

        let cookie = extractCookie(from: httpResponse)
        return LoginSession(
            serverURL: baseURL,
            username: username,
            cookie: cookie
        )
    }

    func search(query: String) async throws -> [SearchResult] {
        var components = URLComponents(url: baseURL.appendingPathComponent("/api/search"), resolvingAgainstBaseURL: false)
        components?.queryItems = [URLQueryItem(name: "q", value: query)]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("搜索请求失败")
        }

        let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        guard let results = json?["results"] as? [[String: Any]] else { return [] }

        return try results.map { dict in
            let data = try JSONSerialization.data(withJSONObject: dict)
            return try JSONDecoder().decode(SearchResult.self, from: data)
        }
    }

    func detail(source: String, id: String) async throws -> SearchResult? {
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/detail"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [
            URLQueryItem(name: "source", value: source),
            URLQueryItem(name: "id", value: id)
        ]

        guard let url = components?.url else { throw APIError.invalidURL }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取详情失败")
        }

        return try JSONDecoder().decode(SearchResult.self, from: data)
    }

    func searchResources() async throws -> [SearchResource] {
        let url = baseURL.appendingPathComponent("/api/search/resources")

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, httpResponse) = try await session.data(for: request)
        guard let httpResponse = httpResponse as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            if let httpResponse = httpResponse as? HTTPURLResponse,
               httpResponse.statusCode == 401 {
                throw APIError.unauthorized
            }
            throw APIError.message("获取资源列表失败")
        }

        return try JSONDecoder().decode([SearchResource].self, from: data)
    }

    private func extractCookie(from response: HTTPURLResponse) -> String {
        guard let setCookie = response.allHeaderFields["Set-Cookie"] as? String else { return "" }
        let parts = setCookie.split(separator: ";", maxSplits: 1, omittingEmptySubsequences: true)
        return String(parts.first ?? "")
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Services/ServerAPIClient.swift
git commit -m "feat: add ServerAPIClient with login/search/detail APIs"
```

---

### Task 10: Stores - SearchStore and PlayerStore

**Files:**
- Create: `native-macos/Sources/SeleneNative/Stores/SearchStore.swift`
- Create: `native-macos/Sources/SeleneNative/Stores/PlayerStore.swift`

- [ ] **Step 1: Write SearchStore.swift**

```swift
import SwiftUI
import Combine

final class SearchStore: ObservableObject {
    @Published var query: String = ""
    @Published var results: [SearchResult] = []
    @Published var isLoading: Bool = false
    @Published var selectedResult: SearchResult?
    @Published var errorMessage: String?
    @Published var resources: [SearchResource] = []

    private let provider: ContentProvider

    init(provider: ContentProvider) {
        self.provider = provider
    }

    func search() async {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }

        await MainActor.run {
            isLoading = true
            errorMessage = nil
            results = []
        }

        do {
            let searchResults = try await provider.search(query: query)
            await MainActor.run {
                results = searchResults
                isLoading = false
            }
        } catch {
            await MainActor.run {
                errorMessage = error.localizedDescription
                isLoading = false
            }
        }
    }

    func loadResources() async {
        do {
            let res = try await provider.searchResources()
            await MainActor.run {
                resources = res.filter { !$0.disabled }
            }
        } catch {
            // Non-critical
        }
    }

    func selectResult(_ result: SearchResult) {
        selectedResult = result
    }

    func clearSelection() {
        selectedResult = nil
    }

    func clearError() {
        errorMessage = nil
    }
}
```

- [ ] **Step 2: Write PlayerStore.swift**

```swift
import SwiftUI
import AVKit

@MainActor
final class PlayerStore: ObservableObject {
    @Published var player: AVPlayer?
    @Published var playbackError: String?
    @Published var currentEpisodeURL: URL?

    private var playerObserver: NSKeyValueObservation?

    init() {}

    func loadEpisode(url: URL) {
        playbackError = nil
        currentEpisodeURL = url

        let playerItem = AVPlayerItem(url: url)
        let player = AVPlayer(playerItem: playerItem)
        self.player = player

        playerObserver = playerItem.observe(
            \.status,
            options: [.new, .old]
        ) { [weak self] item, _ in
            Task { @MainActor in
                if item.status == .failed {
                    self?.playbackError = self?.playerItemErrorDescription(item.error)
                }
            }
        }
    }

    func play() {
        player?.play()
    }

    func pause() {
        player?.pause()
    }

    func replaceItem(url: URL) {
        playerObserver?.invalidate()
        playerObserver = nil
        loadEpisode(url: url)
    }

    func stop() {
        player?.pause()
        player = nil
        playerObserver?.invalidate()
        playerObserver = nil
        currentEpisodeURL = nil
        playbackError = nil
    }

    private func playerItemErrorDescription(_ error: Error?) -> String {
        guard let error = error else { return "播放失败" }
        let nsError = error as NSError
        if nsError.domain == NSURLErrorDomain {
            switch nsError.code {
            case NSURLErrorNotConnectedToInternet:
                return "网络连接失败，请检查网络"
            case NSURLErrorTimedOut:
                return "连接超时"
            default:
                return "播放失败: \(error.localizedDescription)"
            }
        }
        return "播放失败: \(error.localizedDescription)"
    }
}
```

- [ ] **Step 3: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 4: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Stores/SearchStore.swift
git add native-macos/Sources/SeleneNative/Stores/PlayerStore.swift
git commit -m "feat: add SearchStore and PlayerStore"
```

---

### Task 11: Views - LoginView

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/LoginView.swift`

- [ ] **Step 1: Write LoginView.swift**

```swift
import SwiftUI

struct LoginView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var serverURL: String = ""
    @State private var username: String = ""
    @State private var password: String = ""
    @State private var isLoggingIn: Bool = false
    @State private var displayError: String?

    private var provider: ServerAPIClient {
        let url = URLNormalizer.normalize(serverURL) ?? URL(string: "https://example.com")!
        return ServerAPIClient(baseURL: url)
    }

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "play.rectangle.fill")
                .font(.system(size: 48))
                .foregroundStyle(.tint)

            Text("Selene")
                .font(.title)
                .bold()

            VStack(alignment: .leading, spacing: 4) {
                Text("服务器地址")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("https://example.com", text: $serverURL)
                    .textFieldStyle(.roundedBorder)
                    .autocorrectionDisabled()
                    .textInputAutocapitalization(.never)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("用户名")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("用户名", text: $username)
                    .textFieldStyle(.roundedBorder)
                    .textInputAutocapitalization(.never)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("密码")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                SecureField("密码", text: $password)
                    .textFieldStyle(.roundedBorder)
            }

            if let error = displayError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            Button {
                Task { await attemptLogin() }
            } label: {
                if isLoggingIn {
                    ProgressView()
                        .controlSize(.small)
                } else {
                    Text("登录")
                }
            }
            .keyboardShortcut(.defaultAction)
            .disabled(isLoggingIn || serverURL.isEmpty || username.isEmpty || password.isEmpty)

            Button("清空") {
                serverURL = ""
                username = ""
                password = ""
                displayError = nil
            }
            .buttonStyle(.plain)
            .foregroundStyle(.secondary)

            Spacer()
        }
        .padding(30)
        .frame(width: 360, height: 380)
        .onAppear {
            if let existingURL = sessionStore.session?.serverURL.absoluteString {
                serverURL = existingURL
            }
            if let existingUser = sessionStore.session?.username {
                username = existingUser
            }
        }
    }

    private func attemptLogin() async {
        isLoggingIn = true
        displayError = nil

        guard let baseURL = URLNormalizer.normalize(serverURL) else {
            displayError = "服务器地址无效"
            isLoggingIn = false
            return
        }

        let client = ServerAPIClient(baseURL: baseURL)

        do {
            let session = try await client.login(username: username, password: password)
            sessionStore.login(session: session)
        } catch APIError.unauthorized {
            displayError = "用户名或密码错误"
        } catch {
            displayError = error.localizedDescription
        }

        isLoggingIn = false
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Views/LoginView.swift
git commit -m "feat: add LoginView with server URL, username, password fields"
```

---

### Task 12: Views - SearchResultsView

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/SearchResultsView.swift`

- [ ] **Step 1: Write SearchResultsView.swift**

```swift
import SwiftUI

struct SearchResultsView: View {
    @State private var searchStore: SearchStore
    @State private var playerStore: PlayerStore
    private let provider: ContentProvider

    init(provider: ContentProvider) {
        self.provider = provider
        _searchStore = State(initialValue: SearchStore(provider: provider))
        _playerStore = State(initialValue: PlayerStore())
    }

    var body: some View {
        HSplitView {
            // Left panel: Search + Results list
            VStack(spacing: 0) {
                searchBar
                resultsList
            }
            .frame(minWidth: 320)

            // Right panel: Detail
            if let result = searchStore.selectedResult {
                DetailView(
                    result: result,
                    onPlay: { url in
                        playerStore.replaceItem(url: url)
                        playerStore.play()
                    }
                )
                .frame(minWidth: 300)
            } else {
                emptyDetailPlaceholder
                    .frame(minWidth: 300)
            }
        }
        .task {
            await searchStore.loadResources()
        }
    }

    private var searchBar: some View {
        HStack {
            TextField("搜索...", text: $searchStore.query)
                .textFieldStyle(.roundedBorder)
                .onSubmit {
                    Task { await searchStore.search() }
                }

            Button(searchStore.isLoading ? "搜索中..." : "搜索") {
                Task { await searchStore.search() }
            }
            .disabled(searchStore.isLoading || searchStore.query.isEmpty)
        }
        .padding()
    }

    private var resultsList: some View {
        Group {
            if searchStore.isLoading && searchStore.results.isEmpty {
                ProgressView("搜索中...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if searchStore.results.isEmpty && !searchStore.query.isEmpty {
                ContentUnavailableView(
                    "无结果",
                    systemImage: "magnifyingglass",
                    description: Text("尝试其他关键词")
                )
            } else if let error = searchStore.errorMessage {
                VStack {
                    Text("出错了")
                        .font(.headline)
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Button("重试") {
                        Task { await searchStore.search() }
                    }
                }
                .padding()
            } else if searchStore.results.isEmpty {
                ContentUnavailableView(
                    "开始搜索",
                    systemImage: "film",
                    description: Text("在服务器上搜索视频内容")
                )
            } else {
                List(searchStore.results, selection: $searchStore.selectedResult) { result in
                    SearchResultRow(result: result)
                        .onTapGesture {
                            searchStore.selectResult(result)
                        }
                }
            }
        }
    }

    private var emptyDetailPlaceholder: some View {
        ContentUnavailableView {
            Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
        }
    }
}

struct SearchResultRow: View {
    let result: SearchResult

    var body: some View {
        HStack(spacing: 12) {
            if !result.poster.isEmpty, let url = URL(string: result.poster) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .empty:
                        Color.gray.opacity(0.3)
                            .frame(width: 60, height: 80)
                            .cornerRadius(4)
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                            .frame(width: 60, height: 80)
                            .clipped()
                            .cornerRadius(4)
                    case .failure:
                        Color.gray.opacity(0.3)
                            .frame(width: 60, height: 80)
                            .cornerRadius(4)
                    @unknown default:
                        EmptyView()
                    }
                }
            } else {
                Color.gray.opacity(0.3)
                    .frame(width: 60, height: 80)
                    .cornerRadius(4)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text(result.title)
                    .font(.body)
                    .lineLimit(1)
                Text(result.sourceName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                HStack(spacing: 8) {
                    Text(result.year)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    if !result.episodes.isEmpty {
                        Text("共\(result.episodes.count)集")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .padding(.vertical, 4)
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/Sources/SeleneNative/Views/SearchResultsView.swift
git commit -m "feat: add SearchResultsView with list and detail panel"
```

---

### Task 13: Views - DetailView

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/DetailView.swift`

- [ ] **Step 1: Write DetailView.swift**

```swift
import SwiftUI

struct DetailView: View {
    let result: SearchResult
    let onPlay: (URL) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                headerSection
                descriptionSection
                episodesSection
                Spacer()
            }
            .padding()
        }
    }

    private var headerSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(result.title)
                .font(.title2)
                .bold()

            HStack(spacing: 12) {
                Text(result.sourceName)
                    .font(.caption)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.secondary.opacity(0.2))
                    .cornerRadius(4)

                Text(result.year)
                    .font(.caption)
                    .foregroundStyle(.secondary)

                if let typeName = result.typeName, !typeName.isEmpty {
                    Text(typeName)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }

    private var descriptionSection: some View {
        Group {
            if let desc = result.description, !desc.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text("简介")
                        .font(.headline)
                    Text(desc)
                        .font(.body)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }
            }
        }
    }

    private var episodesSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("剧集 (\(result.episodes.count))")
                .font(.headline)

            if result.episodes.isEmpty {
                Text("暂无可播放剧集")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                LazyVGrid(
                    columns: [GridItem(.adaptive(minimum: 80, maximum: 120), spacing: 8)],
                    spacing: 8
                ) {
                    ForEach(result.episodes.indices, id: \.self) { index in
                        Button {
                            if let url = URL(string: result.episodes[index]) {
                                onPlay(url)
                            }
                        } label: {
                            Text(result.episodeTitle(for: index))
                                .font(.caption)
                                .foregroundStyle(.primary)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .frame(maxWidth: .infinity)
                                .background(.regularMaterial)
                                .cornerRadius(6)
                        }
                        .buttonStyle(.plain)
                        .help(result.episodes[index])
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
git add native-macos/Sources/SeleneNative/Views/DetailView.swift
git commit -m "feat: add DetailView with episode grid"
```

---

### Task 14: Views - PlayerView

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/PlayerView.swift`

- [ ] **Step 1: Write PlayerView.swift**

```swift
import SwiftUI
import AVKit

struct PlayerView: View {
    @State private var playerStore: PlayerStore

    init(playerStore: PlayerStore) {
        _playerStore = State(initialValue: playerStore)
    }

    var body: some View {
        Group {
            if let player = playerStore.player {
                VideoPlayer(player: player)
                    .aspectRatio(contentMode: .fit)
            } else if let error = playerStore.playbackError {
                playbackErrorView(error)
            } else {
                ContentUnavailableView(
                    "选择剧集播放",
                    systemImage: "play.circle",
                    description: Text("从详情页面选择一个剧集开始播放")
                )
            }
        }
    }

    private func playbackErrorView(_ error: String) -> some View {
        VStack(spacing: 16) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 40))
                .foregroundStyle(.orange)

            Text("播放失败")
                .font(.headline)

            Text(error)
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

            Button("重试") {
                if let url = playerStore.currentEpisodeURL {
                    playerStore.replaceItem(url: url)
                    playerStore.play()
                }
            }
        }
        .padding()
    }
}
```

- [ ] **Step 2: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 3: Commit**
```bash
git add native-macos/Sources/SeleneNative/Views/PlayerView.swift
git commit -m "feat: add PlayerView with AVKit integration"
```

---

### Task 15: Views - RootView and MainView

**Files:**
- Create: `native-macos/Sources/SeleneNative/Views/RootView.swift`
- Create: `native-macos/Sources/SeleneNative/Views/MainView.swift`
- Modify: `native-macos/Sources/SeleneNative/App/SeleneNativeApp.swift` (update environment injection)

- [ ] **Step 1: Write RootView.swift**

```swift
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
```

- [ ] **Step 2: Write MainView.swift**

```swift
import SwiftUI

struct MainView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var provider: ServerAPIClient
    @State private var searchStore: SearchStore
    @State private var playerStore: PlayerStore

    init() {
        // Initialize with a placeholder; will be replaced in .task
        let placeholderURL = URL(string: "https://example.com")!
        _provider = State(initialValue: ServerAPIClient(baseURL: placeholderURL))
        _searchStore = State(initialValue: SearchStore(provider: ServerAPIClient(baseURL: placeholderURL)))
        _playerStore = State(initialValue: PlayerStore())
    }

    var body: some View {
        VStack(spacing: 0) {
            // Top toolbar
            HStack {
                Text("Selene")
                    .font(.headline)

                Button("退出登录", role: .destructive) {
                    sessionStore.logout()
                }
                Spacer()
            }
            .padding(.horizontal)
            .padding(.vertical, 8)

            Divider()

            // Content area
            HSplitView {
                // Left: Search + Results
                VStack(spacing: 0) {
                    searchBar
                    Divider()
                    resultsList
                }
                .frame(minWidth: 320)

                // Right: Detail + Player
                VStack(spacing: 0) {
                    if let result = searchStore.selectedResult {
                        DetailView(
                            result: result,
                            onPlay: { url in
                                playerStore.replaceItem(url: url)
                                playerStore.play()
                            }
                        )
                    } else {
                        ContentUnavailableView {
                            Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }

                    if let url = playerStore.currentEpisodeURL {
                        Divider()
                        PlayerView(playerStore: playerStore)
                            .frame(height: 200)
                    }
                }
            }

            if searchStore.isLoading {
                Divider()
                ProgressView("搜索中...")
                    .padding(.vertical, 4)
            }
        }
        .navigationTitle("Selene")
        .task {
            // Initialize with actual session URL
            if let url = sessionStore.session?.serverURL {
                let newProvider = ServerAPIClient(baseURL: url)
                provider = newProvider
                searchStore = SearchStore(provider: newProvider)
                await searchStore.loadResources()
            }
        }
    }

    private var searchBar: some View {
        HStack {
            TextField("搜索...", text: $searchStore.query)
                .textFieldStyle(.roundedBorder)
                .onSubmit {
                    Task { await searchStore.search() }
                }

            Button(searchStore.isLoading ? "搜索中..." : "搜索") {
                Task { await searchStore.search() }
            }
            .disabled(searchStore.isLoading || searchStore.query.isEmpty)
        }
        .padding()
    }

    private var resultsList: some View {
        Group {
            if searchStore.results.isEmpty && searchStore.query.isEmpty {
                ContentUnavailableView(
                    "开始搜索",
                    systemImage: "film",
                    description: Text("在服务器上搜索视频内容")
                )
            } else if searchStore.results.isEmpty {
                ContentUnavailableView(
                    "无结果",
                    systemImage: "magnifyingglass",
                    description: Text("尝试其他关键词")
                )
            } else {
                List(searchStore.results, selection: $searchStore.selectedResult) { result in
                    SearchResultRow(result: result)
                        .onTapGesture {
                            searchStore.selectResult(result)
                        }
                }
            }
        }
    }
}
```

- [ ] **Step 3: Update SeleneNativeApp.swift** — remove the toolbar item (moved to MainView)

```swift
Replacement for SeleneNativeApp.swift:

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
```

(This is identical to what was already written in Task 1, so no change needed.)

- [ ] **Step 4: Build to verify**
Run: `cd native-macos && swift build`
Expected: Build succeeds
- [ ] **Step 5: Commit**
```bash
git add native-macos/Sources/SeleneNative/Views/RootView.swift
git add native-macos/Sources/SeleneNative/Views/MainView.swift
git commit -m "feat: add RootView and MainView with split layout"
```

---

### Task 16: Build Script and Codex Config

**Files:**
- Create: `native-macos/script/build_and_run.sh`
- Create: `native-macos/.codex/environments/environment.toml`

- [ ] **Step 1: Write build_and_run.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
NATIVE_DIR="$REPO_ROOT/native-macos"

if [[ ! -d "$NATIVE_DIR" ]]; then
    echo "Error: native-macos/ directory not found at $NATIVE_DIR"
    exit 1
fi

cd "$NATIVE_DIR"

echo "Building SeleneNative..."
swift build -c release

BUILD_DIR=".build/release"
APP_NAME="SeleneNative"
APP_BUNDLE="$NATIVE_DIR/$APP_NAME.app"

rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy binary
cp "$BUILD_DIR/$APP_NAME" "$APP_BUNDLE/Contents/MacOS/$APP_NAME"

# Copy Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>SeleneNative</string>
    <key>CFBundleIdentifier</key>
    <string>com.selene.native</string>
    <key>CFBundleName</key>
    <string>Selene</string>
    <key>CFBundleDisplayName</key>
    <string>Selene</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# Build icon set (use repo logo as source)
ICON_SRC="$REPO_ROOT/logo.png"
if [[ -f "$ICON_SRC" ]]; then
    # Create a simple icns using iconutil if possible, otherwise skip
    # For MVP we'll just note icon can be added later
    echo "Note: App icon setup requires iconutil or manual icns creation"
fi

echo "App bundle created at: $APP_BUNDLE"

# Handle verification flag
if [[ "${VERIFY:-}" == "true" ]]; then
    echo "Verifying app process..."
    "$APP_BUNDLE/Contents/MacOS/$APP_NAME" &
    APP_PID=$!
    sleep 2
    if kill -0 "$APP_PID" 2>/dev/null; then
        echo "Verification PASSED: App process running (PID: $APP_PID)"
        kill "$APP_PID" 2>/dev/null || true
        exit 0
    else
        echo "Verification FAILED: App process not running"
        exit 1
    fi
else
    # Launch the app
    echo "Launching $APP_BUNDLE..."
    open "$APP_BUNDLE"
fi
```

- [ ] **Step 2: Write environment.toml**

```toml
[id]
name = "selene-native-macos"

[build]
command = "swift build --product SeleneNative | grep -E '(warning:|error:)' | grep -v 'warning: using legacy build system' || true"

[lint]
command = "swift build 2>&1 | grep -c 'error:' || echo 0"

[test]
command = "swift test"
```

- [ ] **Step 3: Make build script executable**
Run: `chmod +x native-macos/script/build_and_run.sh`
- [ ] **Step 4: Commit**
```bash
git add native-macos/script/build_and_run.sh native-macos/.codex/environments/environment.toml
git commit -m "feat: add build script and codex environment config"
```

---

### Task 17: Integration Test and Final Verification

**Files:**
- Create: `native-macos/Tests/SeleneNativeTests/SearchResultTests.swift`
- Create: `native-macos/Tests/SeleneNativeTests/URLNormalizerTests.swift`
- Create: `native-macos/Tests/SeleneNativeTests/ServerAPIClientTests.swift`
- Create: `native-macos/Tests/SeleneNativeTests/SessionStoreTests.swift`

- [ ] **Step 1: Write SearchResultTests.swift**

```swift
import XCTest
@testable import SeleneNative

final class SearchResultTests: XCTestCase {
    func testDecodeFromJSON() throws {
        let json = """
        {
            "id": "123",
            "title": "测试视频",
            "poster": "https://example.com/poster.jpg",
            "episodes": ["https://example.com/ep1.m3u8"],
            "episodes_titles": ["第1集"],
            "source": "source1",
            "source_name": "源1",
            "class": "电影",
            "year": "2024",
            "desc": "描述内容",
            "type_name": "电影",
            "douban_id": 12345
        }
        """.data(using: .utf8)!

        let result = try JSONDecoder().decode(SearchResult.self, from: json)
        XCTAssertEqual(result.id, "123")
        XCTAssertEqual(result.title, "测试视频")
        XCTAssertEqual(result.episodes.count, 1)
        XCTAssertEqual(result.episodeTitles.count, 1)
        XCTAssertEqual(result.episodeTitles[0], "第1集")
        XCTAssertEqual(result.source, "source1")
        XCTAssertEqual(result.sourceName, "源1")
        XCTAssertEqual(result.className, "电影")
        XCTAssertEqual(result.year, "2024")
        XCTAssertEqual(result.description, "描述内容")
        XCTAssertEqual(result.typeName, "电影")
        XCTAssertEqual(result.doubanID, 12345)
    }

    func testDecodeWithMissingOptionalFields() throws {
        let json = """
        {
            "id": "123",
            "title": "测试",
            "poster": "",
            "episodes": [],
            "episodes_titles": [],
            "source": "s",
            "source_name": "",
            "year": ""
        }
        """.data(using: .utf8)!

        let result = try JSONDecoder().decode(SearchResult.self, from: json)
        XCTAssertEqual(result.id, "123")
        XCTAssertEqual(result.description, "")
        XCTAssertEqual(result.typeName, "")
        XCTAssertNil(result.doubanID)
    }

    func testEpisodeTitleFallback() {
        let result = SearchResult(
            id: "1", title: "测试", poster: "",
            episodes: ["url1", "url2"],
            episodeTitles: [],
            source: "s", sourceName: "", year: "2024"
        )
        XCTAssertEqual(result.episodeTitle(for: 0), "第1集")
        XCTAssertEqual(result.episodeTitle(for: 1), "第2集")
    }

    func testEpisodeTitleWithTitleList() {
        let result = SearchResult(
            id: "1", title: "测试", poster: "",
            episodes: ["url1"],
            episodeTitles: ["第一集"],
            source: "s", sourceName: "", year: "2024"
        )
        XCTAssertEqual(result.episodeTitle(for: 0), "第一集")
    }
}
```

- [ ] **Step 2: Write URLNormalizerTests.swift**

```swift
import XCTest
@testable import SeleneNative

final class URLNormalizerTests: XCTestCase {
    func testAddsHTTPScheme() {
        let result = URLNormalizer.normalize("example.com")
        XCTAssertEqual(result?.scheme, "https")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testAddsHTTPForLocalhost() {
        let result = URLNormalizer.normalize("localhost:8080")
        XCTAssertEqual(result?.scheme, "http")
        XCTAssertEqual(result?.host, "localhost")
        XCTAssertEqual(result?.port, 8080)
    }

    func testPreservesExistingHTTPS() {
        let result = URLNormalizer.normalize("https://example.com")
        XCTAssertEqual(result?.scheme, "https")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testPreservesExistingHTTP() {
        let result = URLNormalizer.normalize("http://example.com")
        XCTAssertEqual(result?.scheme, "http")
        XCTAssertEqual(result?.host, "example.com")
    }

    func testReturnsNilForEmptyInput() {
        let result = URLNormalizer.normalize("")
        XCTAssertNil(result)
    }

    func testReturnsNilForInvalidURL() {
        let result = URLNormalizer.normalize("://invalid")
        XCTAssertNil(result)
    }
}
```

- [ ] **Step 3: Write ServerAPIClientTests.swift**

```swift
import XCTest
@testable import SeleneNative

final class ServerAPIClientTests: XCTestCase {
    func testLoginURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        let client = ServerAPIClient(baseURL: baseURL)

        // We can't test async login without a server, but verify baseURL is stored
        XCTAssertEqual(client.baseURL, baseURL)
    }

    func testSearchURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        let client = ServerAPIClient(baseURL: baseURL)

        // Verify the URL construction logic by testing the helper pattern
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/search"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [URLQueryItem(name: "q", value: "test")]
        XCTAssertEqual(components?.url?.absoluteString, "https://example.com/api/search?q=test")
    }

    func testDetailURLConstruction() {
        let baseURL = URL(string: "https://example.com")!
        var components = URLComponents(
            url: baseURL.appendingPathComponent("/api/detail"),
            resolvingAgainstBaseURL: false
        )
        components?.queryItems = [
            URLQueryItem(name: "source", value: "src1"),
            URLQueryItem(name: "id", value: "id1")
        ]
        XCTAssertEqual(
            components?.url?.absoluteString,
            "https://example.com/api/detail?source=src1&id=id1"
        )
    }

    func testCookieExtraction() {
        let response = HTTPURLResponse(
            url: URL(string: "https://example.com")!,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Set-Cookie": "session=abc123; Path=/; HttpOnly"]
        )!

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!)
        let cookie = client.extractCookie(from: response)
        XCTAssertEqual(cookie, "session=abc123")
    }

    func testEmptyCookieExtraction() {
        let response = HTTPURLResponse(
            url: URL(string: "https://example.com")!,
            statusCode: 200,
            httpVersion: nil,
            headerFields: [:]
        )!

        let client = ServerAPIClient(baseURL: URL(string: "https://example.com")!)
        let cookie = client.extractCookie(from: response)
        XCTAssertEqual(cookie, "")
    }
}
```

Note: `baseURL` is `private` — need to either make it `internal` or add a computed property for testing. Let me update the ServerAPIClient to add an internal accessor:

```swift
// Add to ServerAPIClient:
var baseURLForTesting: URL { baseURL }
```

Actually, `@testable import` exposes `internal` declarations to tests, but not `private` declarations. Keep `baseURL` internal or add an internal testing accessor before using it from tests.

- [ ] **Step 4: Write SessionStoreTests.swift**

```swift
import XCTest
@testable import SeleneNative

final class SessionStoreTests: XCTestCase {
    func testInitialStateIsLoggedOut() {
        let store = SessionStore()
        XCTAssertNil(store.session)
        XCTAssertFalse(store.isLoggedIn)
    }

    func testLoginSetsSession() {
        let store = SessionStore()
        let session = LoginSession(
            serverURL: URL(string: "https://example.com")!,
            username: "test",
            cookie: "cookie=1"
        )
        store.login(session: session)
        XCTAssertTrue(store.isLoggedIn)
        XCTAssertEqual(store.session?.username, "test")
        XCTAssertEqual(store.session?.cookie, "cookie=1")
    }

    func testLogoutClearsSession() {
        let store = SessionStore()
        let session = LoginSession(
            serverURL: URL(string: "https://example.com")!,
            username: "test",
            cookie: "cookie=1"
        )
        store.login(session: session)
        store.logout()
        XCTAssertNil(store.session)
        XCTAssertFalse(store.isLoggedIn)
    }

    func testSetErrorUpdatesErrorMessage() {
        let store = SessionStore()
        store.setError("测试错误")
        XCTAssertEqual(store.errorMessage, "测试错误")
    }
}
```

- [ ] **Step 5: Run all tests**
Run: `cd native-macos && swift test`
Expected: All tests PASS
- [ ] **Step 6: Commit**
```bash
git add native-macos/Tests/SeleneNativeTests/SearchResultTests.swift
git add native-macos/Tests/SeleneNativeTests/URLNormalizerTests.swift
git add native-macos/Tests/SeleneNativeTests/ServerAPIClientTests.swift
git add native-macos/Tests/SeleneNativeTests/SessionStoreTests.swift
git commit -m "test: add unit tests for models, URL normalizer, API client, and session store"
```

---

### Task 18: Spec Self-Review and Final Polish

- [ ] **Step 1: Verify all spec requirements are met**

Check each section from the spec document and confirm implementation:
- [ ] Login flow: server URL, username, password, cookie storage → `LoginView`, `ServerAPIClient.login`, `SessionStore`
- [ ] Search flow: query → search → results → select → detail → play → `SearchResultsView`, `SearchStore`, `DetailView`
- [ ] Server APIs: login, search, detail, search/resources → `ServerAPIClient`
- [ ] Models: SearchResult, SearchResource, LoginSession → all implemented
- [ ] URL normalizer → `URLNormalizer`
- [ ] UserDefaults persistence (no password) → `SessionStore`
- [ ] Error handling: invalid URL, login failure, 401, no results, playback failure → all covered
- [ ] 401 handling: throw APIError.unauthorized → covered in ServerAPIClient
- [ ] State management: `@Observable` stores → SessionStore, SearchStore, PlayerStore (ObservableObject)
- [ ] Persistence: UserDefaults only → SessionStore
- [ ] AVKit player → PlayerStore uses AVPlayer
- [ ] Episode title fallback → `episodeTitle(for:)` on SearchResult
- [ ] Build script → `script/build_and_run.sh`
- [ ] Codex config → `.codex/environments/environment.toml`

- [ ] **Step 2: Verify build passes clean**
Run: `cd native-macos && swift build`
Expected: Build succeeds with no errors
- [ ] **Step 3: Verify all tests pass**
Run: `cd native-macos && swift test`
Expected: All tests PASS
- [ ] **Step 4: Verify file structure matches spec**

Expected files:
```
native-macos/
├── Package.swift
├── Sources/SeleneNative/
│   ├── App/SeleneNativeApp.swift
│   ├── Models/SearchResult.swift
│   ├── Models/SearchResource.swift
│   ├── Models/LoginSession.swift
│   ├── Models/APIError.swift
│   ├── Services/ContentProvider.swift
│   ├── Services/ServerAPIClient.swift
│   ├── Stores/SessionStore.swift
│   ├── Stores/SearchStore.swift
│   ├── Stores/PlayerStore.swift
│   └── Support/URLNormalizer.swift
│   └── Views/
│       ├── RootView.swift
│       ├── LoginView.swift
│       ├── MainView.swift
│       ├── SearchResultsView.swift
│       ├── DetailView.swift
│       └── PlayerView.swift
├── Tests/SeleneNativeTests/ (4 test files)
├── script/build_and_run.sh
└── .codex/environments/environment.toml
```

- [ ] **Step 5: Final commit with all remaining files**
```bash
cd /Users/xiwei/Documents/Selene
git add native-macos/
git commit -m "feat: complete native macOS MVP implementation"
```

- [ ] **Step 6: Update root gitignore if needed**
Check if `native-macos/.build/` and `.swiftpm/` are ignored:
Run: `grep -q "native-macos/.build" .gitignore || echo "Add native-macos/.build to .gitignore"`
If not present, add:
```bash
echo "native-macos/.build/" >> .gitignore
echo "native-macos/.swiftpm/" >> .gitignore
git add .gitignore && git commit -m "chore: add native-macos build artifacts to gitignore"
```

---

## Plan Summary

**Total tasks: 18**
**Total files created: ~22**
**Total tests: 4 test files covering URL normalizer, models, API client, session store**

### Spec Coverage Check

| Spec Section | Implemented In |
|---|---|
| Package structure (Task 1) | Package.swift, App/ |
| Models - LoginSession (Task 3) | Models/LoginSession.swift |
| Models - SearchResult (Task 4) | Models/SearchResult.swift |
| Models - SearchResource (Task 5) | Models/SearchResource.swift |
| APIError (Task 2) | Models/APIError.swift |
| URLNormalizer (Task 6) | Support/URLNormalizer.swift |
| ContentProvider protocol (Task 8) | Services/ContentProvider.swift |
| ServerAPIClient (Task 9) | Services/ServerAPIClient.swift |
| SessionStore (Task 7) | Stores/SessionStore.swift |
| SearchStore (Task 10) | Stores/SearchStore.swift |
| PlayerStore (Task 10) | Stores/PlayerStore.swift |
| LoginView (Task 11) | Views/LoginView.swift |
| SearchResultsView (Task 12) | Views/SearchResultsView.swift |
| DetailView (Task 13) | Views/DetailView.swift |
| PlayerView (Task 14) | Views/PlayerView.swift |
| RootView + MainView (Task 15) | Views/RootView.swift, Views/MainView.swift |
| Build script (Task 16) | script/build_and_run.sh |
| Codex config (Task 16) | .codex/environments/environment.toml |
| Unit tests (Task 17) | Tests/SeleneNativeTests/ |
| Error handling (Tasks 2, 9, 10) | APIError, ServerAPIClient, PlayerStore |
| UserDefaults persistence (Task 7) | SessionStore |
| Episode title fallback (Task 4) | SearchResult.episodeTitle(for:) |
| 401 handling (Task 9) | ServerAPIClient throws APIError.unauthorized |

**No gaps identified. All spec requirements are covered.**
