import Foundation

struct UserSession: Codable, Identifiable, Hashable {
    let id: UUID
    let serverURL: URL
    let username: String
    let cookie: String
    let isLocalMode: Bool
    let localSearchResources: [SearchResource]
    let localLiveSources: [LiveSource]

    enum CodingKeys: String, CodingKey {
        case id, serverURL, username, cookie, isLocalMode, localSearchResources, localLiveSources
    }

    init(
        id: UUID = UUID(),
        serverURL: URL,
        username: String,
        cookie: String,
        isLocalMode: Bool = false,
        localSearchResources: [SearchResource] = [],
        localLiveSources: [LiveSource] = []
    ) {
        self.id = id
        self.serverURL = serverURL
        self.username = username
        self.cookie = cookie
        self.isLocalMode = isLocalMode
        self.localSearchResources = localSearchResources
        self.localLiveSources = localLiveSources
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decodeIfPresent(UUID.self, forKey: .id) ?? UUID()
        serverURL = try container.decode(URL.self, forKey: .serverURL)
        username = try container.decode(String.self, forKey: .username)
        cookie = try container.decodeIfPresent(String.self, forKey: .cookie) ?? ""
        isLocalMode = try container.decodeIfPresent(Bool.self, forKey: .isLocalMode) ?? false
        localSearchResources = try container.decodeIfPresent([SearchResource].self, forKey: .localSearchResources) ?? []
        localLiveSources = try container.decodeIfPresent([LiveSource].self, forKey: .localLiveSources) ?? []
    }
}