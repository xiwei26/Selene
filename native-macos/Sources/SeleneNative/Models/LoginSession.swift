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
