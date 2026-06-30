import XCTest
@testable import SeleneNative

final class ShortDramaStoreTests: XCTestCase {
    @MainActor
    func testLoadInitialLoadsCategoriesAndRecommendedItems() async {
        let provider = FakeShortDramaProvider(
            categories: [ShortDramaCategory(id: "1", name: "Urban")],
            recommended: ShortDramaListResult(items: [ShortDramaItem(id: "s1", name: "Short One", cover: "c.jpg")], total: 1)
        )
        let store = ShortDramaStore(provider: provider)

        await store.loadInitial()

        XCTAssertFalse(store.isLoading)
        XCTAssertNil(store.errorMessage)
        XCTAssertEqual(store.categories.count, 1)
        XCTAssertEqual(store.items.count, 1)
    }

    @MainActor
    func testPlayEpisodeParsesSelectedEpisode() async {
        let provider = FakeShortDramaProvider(parseResult: ShortDramaParseResult(parsedUrl: "https://video.example/1.m3u8"))
        let store = ShortDramaStore(provider: provider)

        let url = await store.playURL(for: ShortDramaItem(id: "s1", name: "Short One", cover: ""), episode: 1)

        XCTAssertEqual(url?.absoluteString, "https://video.example/1.m3u8")
    }

    @MainActor
    func testPlayEpisodeBuildsSearchResultForHistory() async {
        let provider = FakeShortDramaProvider(parseResult: ShortDramaParseResult(parsedUrl: "https://video.example/1.m3u8"))
        let store = ShortDramaStore(provider: provider)

        let request = await store.playRequest(
            for: ShortDramaItem(id: "s1", name: "Short One", cover: "c.jpg", year: "2026", category: "hot"),
            episode: 3
        )

        XCTAssertEqual(request?.url.absoluteString, "https://video.example/1.m3u8")
        XCTAssertEqual(request?.result.source, "shortdrama")
        XCTAssertEqual(request?.result.sourceName, "Short Drama")
        XCTAssertEqual(request?.result.id, "s1")
        XCTAssertEqual(request?.result.title, "Short One")
        XCTAssertEqual(request?.result.poster, "c.jpg")
        XCTAssertEqual(request?.result.year, "2026")
        XCTAssertEqual(request?.result.episodes, ["https://video.example/1.m3u8"])
        XCTAssertEqual(request?.index, 0)
    }
}

private struct FakeShortDramaProvider: ShortDramaProviding {
    var categories: [ShortDramaCategory] = []
    var recommended = ShortDramaListResult(items: [], total: 0)
    var list = ShortDramaListResult(items: [], total: 0)
    var searchResult = ShortDramaListResult(items: [], total: 0)
    var detail: ShortDramaDetail?
    var parseResult: ShortDramaParseResult?

    func loadCategories() async throws -> [ShortDramaCategory] { categories }
    func loadRecommend(category: String?, size: Int) async throws -> ShortDramaListResult { recommended }
    func loadList(categoryId: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult { list }
    func search(query: String, page: Int, pageSize: Int) async throws -> ShortDramaListResult { searchResult }
    func loadDetail(id: String, name: String?) async throws -> ShortDramaDetail? { detail }
    func parse(id: String, episode: Int, name: String?) async throws -> ShortDramaParseResult? { parseResult }
}
