import Foundation

protocol ContentProvider: Sendable {
    func login(username: String, password: String) async throws -> LoginSession
    func search(query: String) async throws -> [SearchResult]
    func detail(source: String, id: String) async throws -> SearchResult?
    func searchResources() async throws -> [SearchResource]
}
