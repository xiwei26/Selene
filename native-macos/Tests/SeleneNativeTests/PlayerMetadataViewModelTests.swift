import XCTest
@testable import SeleneNative

@MainActor
final class PlayerMetadataViewModelTests: XCTestCase {
    func testLoadWithoutProviderKeepsBaseMetadata() async {
        let result = makeResult(description: "基础简介")
        let viewModel = PlayerMetadataViewModel()

        await viewModel.load(result: result, provider: nil)

        XCTAssertEqual(viewModel.result, result)
        XCTAssertEqual(viewModel.overview, "基础简介")
        XCTAssertNil(viewModel.tmdbBackdrop)
        XCTAssertNil(viewModel.quickInfo)
        XCTAssertTrue(viewModel.comments.isEmpty)
        XCTAssertTrue(viewModel.recommendations.isEmpty)
        XCTAssertFalse(viewModel.isLoading)
    }

    func testLoadUsesSupplementaryMetadata() async {
        let result = makeResult(description: "基础简介", doubanID: 42)
        let provider = PlayerMetadataProvider(
            backdrop: TMDBBackdrop(
                backdrop: "https://example.com/backdrop.jpg",
                poster: "https://example.com/poster.jpg",
                logo: nil,
                title: "TMDB 标题",
                overview: "TMDB 简介",
                rating: 8.2,
                year: "2024",
                numberOfSeasons: 2
            ),
            quickInfo: DoubanQuickInfo(
                title: "豆瓣标题",
                year: "2024",
                rating: "8.8",
                summary: "豆瓣简介",
                genres: ["剧情"],
                directors: ["导演"],
                cast: ["主演"]
            ),
            comments: [
                DoubanComment(author: "用户", content: "短评", rating: "4")
            ],
            recommendations: [
                DoubanRecommendation(id: "r1", title: "推荐", cover: "", rating: "8.0")
            ]
        )
        let viewModel = PlayerMetadataViewModel()

        await viewModel.load(result: result, provider: provider)

        XCTAssertEqual(viewModel.tmdbBackdrop?.overview, "TMDB 简介")
        XCTAssertEqual(viewModel.quickInfo?.rating, "8.8")
        XCTAssertEqual(viewModel.comments.map(\.content), ["短评"])
        XCTAssertEqual(viewModel.recommendations.map(\.title), ["推荐"])
        XCTAssertEqual(viewModel.overview, "TMDB 简介")
        XCTAssertFalse(viewModel.isLoading)
    }

    func testLoadIgnoresSupplementaryFailures() async {
        let result = makeResult(description: "基础简介", doubanID: 42)
        let provider = PlayerMetadataProvider(shouldFail: true)
        let viewModel = PlayerMetadataViewModel()

        await viewModel.load(result: result, provider: provider)

        XCTAssertNil(viewModel.tmdbBackdrop)
        XCTAssertNil(viewModel.quickInfo)
        XCTAssertTrue(viewModel.comments.isEmpty)
        XCTAssertTrue(viewModel.recommendations.isEmpty)
        XCTAssertEqual(viewModel.overview, "基础简介")
        XCTAssertFalse(viewModel.isLoading)
    }

    private func makeResult(description: String, doubanID: Int? = nil) -> SearchResult {
        SearchResult(
            id: "item-1",
            title: "测试影片",
            poster: "",
            episodes: ["https://example.com/video.m3u8"],
            episodeTitles: ["第1集"],
            source: "movie",
            sourceName: "测试源",
            className: "电影",
            year: "2024",
            description: description,
            typeName: "电影",
            doubanID: doubanID
        )
    }
}

private struct PlayerMetadataProvider: ContentProvider {
    var shouldFail = false
    var backdrop: TMDBBackdrop?
    var quickInfo: DoubanQuickInfo?
    var comments: [DoubanComment] = []
    var recommendations: [DoubanRecommendation] = []

    func login(username: String, password: String) async throws -> LoginSession {
        throw TestError.unimplemented
    }

    func search(query: String) async throws -> [SearchResult] {
        []
    }

    func detail(source: String, id: String) async throws -> SearchResult? {
        nil
    }

    func searchResources() async throws -> [SearchResource] {
        []
    }

    func getFavorites() async throws -> [FavoriteItem] {
        []
    }

    func addFavorite(source: String, id: String, data: [String: Any]) async throws {}

    func removeFavorite(source: String, id: String) async throws {}

    func savePlayRecord(_ record: PlayRecord) async throws {}

    func deletePlayRecord(source: String, id: String) async throws {}

    func clearPlayRecords() async throws {}

    func getPlayRecords() async throws -> [PlayRecord] {
        []
    }

    func getSearchHistory() async throws -> [String] {
        []
    }

    func addSearchHistory(query: String) async throws {}

    func deleteSearchHistory(query: String) async throws {}

    func clearSearchHistory() async throws {}

    func searchSuggestions(query: String) async throws -> [SearchSuggestion] {
        []
    }

    func getLiveSources() async throws -> [LiveSource] {
        []
    }

    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel] {
        []
    }

    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData? {
        nil
    }

    func sseSearchURL(query: String) -> URL? {
        nil
    }

    func getTMDBBackdrop(title: String, year: String? = nil, type: String? = nil) async throws -> TMDBBackdrop? {
        if shouldFail { throw TestError.expected }
        return backdrop
    }

    func getDoubanQuickInfo(title: String) async throws -> DoubanQuickInfo? {
        if shouldFail { throw TestError.expected }
        return quickInfo
    }

    func getDoubanComments(doubanId: String) async throws -> [DoubanComment] {
        if shouldFail { throw TestError.expected }
        return comments
    }

    func getDoubanRecommendations(doubanId: String) async throws -> [DoubanRecommendation] {
        if shouldFail { throw TestError.expected }
        return recommendations
    }

    private enum TestError: Error {
        case expected
        case unimplemented
    }
}