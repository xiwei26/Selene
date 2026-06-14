import Foundation

protocol ContentProvider: Sendable {
    func login(username: String, password: String) async throws -> LoginSession
    func search(query: String) async throws -> [SearchResult]
    func detail(source: String, id: String) async throws -> SearchResult?
    func searchResources() async throws -> [SearchResource]
    func getFavorites() async throws -> [FavoriteItem]
    func addFavorite(source: String, id: String, data: [String: Any]) async throws
    func removeFavorite(source: String, id: String) async throws
    func savePlayRecord(_ record: PlayRecord) async throws
    func deletePlayRecord(source: String, id: String) async throws
    func clearPlayRecords() async throws
    func getPlayRecords() async throws -> [PlayRecord]
    func getSearchHistory() async throws -> [String]
    func addSearchHistory(query: String) async throws
    func deleteSearchHistory(query: String) async throws
    func clearSearchHistory() async throws
    func searchSuggestions(query: String) async throws -> [SearchSuggestion]
    func getLiveSources() async throws -> [LiveSource]
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel]
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData?
    func sseSearchURL(query: String) -> URL?
}
