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
    func getRecommendedShortDramas() async throws -> [SearchResult]
    func searchShortDramas(query: String) async throws -> [SearchResult]
    func getShortDramaDetail(id: String, name: String?) async throws -> SearchResult?
    func getBilibiliPopular() async throws -> [MediaPlatformItem]
    func searchBilibili(query: String) async throws -> [MediaPlatformItem]
    func getYouTubePopular(regionCode: String) async throws -> [MediaPlatformItem]
    func searchYouTube(query: String) async throws -> [MediaPlatformItem]
    func getTMDBBackdrop(title: String, year: String?, type: String?) async throws -> TMDBBackdrop?
    func getDoubanQuickInfo(title: String) async throws -> DoubanQuickInfo?
    func getDoubanComments(doubanId: String) async throws -> [DoubanComment]
    func getDoubanRecommendations(doubanId: String) async throws -> [DoubanRecommendation]
    func getAdminConfig() async throws -> AdminConfig?
    func saveYouTubeConfig(_ config: YouTubeAdminConfig) async throws
    func saveBilibiliConfig(enabled: Bool) async throws
}

extension ContentProvider {
    func getRecommendedShortDramas() async throws -> [SearchResult] { [] }
    func searchShortDramas(query: String) async throws -> [SearchResult] { [] }
    func getShortDramaDetail(id: String, name: String? = nil) async throws -> SearchResult? { nil }
    func getBilibiliPopular() async throws -> [MediaPlatformItem] { [] }
    func searchBilibili(query: String) async throws -> [MediaPlatformItem] { [] }
    func getYouTubePopular(regionCode: String = "US") async throws -> [MediaPlatformItem] { [] }
    func searchYouTube(query: String) async throws -> [MediaPlatformItem] { [] }
    func getTMDBBackdrop(title: String, year: String? = nil, type: String? = nil) async throws -> TMDBBackdrop? { nil }
    func getDoubanQuickInfo(title: String) async throws -> DoubanQuickInfo? { nil }
    func getDoubanComments(doubanId: String) async throws -> [DoubanComment] { [] }
    func getDoubanRecommendations(doubanId: String) async throws -> [DoubanRecommendation] { [] }
    func getAdminConfig() async throws -> AdminConfig? { nil }
    func saveYouTubeConfig(_ config: YouTubeAdminConfig) async throws {}
    func saveBilibiliConfig(enabled: Bool) async throws {}
}
