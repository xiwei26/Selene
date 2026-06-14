import Foundation

struct LoginSession: Codable, Identifiable {
    let id: UUID
    let serverURL: URL
    let username: String
    let cookie: String
    let isLocalMode: Bool
    let localLiveSources: [LiveSource]

    enum CodingKeys: String, CodingKey {
        case id, serverURL, username, cookie, isLocalMode, localLiveSources
    }

    init(
        id: UUID = UUID(),
        serverURL: URL,
        username: String,
        cookie: String,
        isLocalMode: Bool = false,
        localLiveSources: [LiveSource] = []
    ) {
        self.id = id
        self.serverURL = serverURL
        self.username = username
        self.cookie = cookie
        self.isLocalMode = isLocalMode
        self.localLiveSources = localLiveSources
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decodeIfPresent(UUID.self, forKey: .id) ?? UUID()
        serverURL = try container.decode(URL.self, forKey: .serverURL)
        username = try container.decode(String.self, forKey: .username)
        cookie = try container.decode(String.self, forKey: .cookie)
        isLocalMode = try container.decodeIfPresent(Bool.self, forKey: .isLocalMode) ?? false
        localLiveSources = try container.decodeIfPresent([LiveSource].self, forKey: .localLiveSources) ?? []
    }
}
