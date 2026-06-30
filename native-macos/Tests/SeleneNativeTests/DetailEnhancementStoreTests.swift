import XCTest
@testable import SeleneNative

final class DetailEnhancementStoreTests: XCTestCase {
    @MainActor
    func testLoadWithDoubanIdLoadsOptionalEnhancements() async {
        let provider = FakeMetadataEnhancementProvider(
            backdrop: TmdbBackdropResult(backdropUrl: "https://img.example/backdrop.jpg"),
            comments: [DoubanComment(username: "u", content: "good")],
            recommendations: [DoubanMovie(id: "r1", title: "Related", poster: "", rate: nil, year: "2026")]
        )
        let store = DetailEnhancementStore(provider: provider)

        await store.load(title: "Title", year: "2026", sourceType: "movie", doubanId: 1292052)

        XCTAssertEqual(store.backdrop?.backdropUrl, "https://img.example/backdrop.jpg")
        XCTAssertEqual(store.comments.count, 1)
        XCTAssertEqual(store.recommendations.count, 1)
    }
}

private struct FakeMetadataEnhancementProvider: MetadataEnhancementProviding {
    var backdrop: TmdbBackdropResult?
    var actor: TmdbActorResult?
    var comments: [DoubanComment] = []
    var recommendations: [DoubanMovie] = []
    var quickInfo: DoubanQuickInfo?
    var suggestions: [DoubanSuggestItem] = []
    var celebrityWorks: [DoubanCelebrityWork] = []
    var trailer: TrailerRefreshResult?

    func loadBackdrop(title: String, originalTitle: String?, year: String?, sourceType: String?) async throws -> TmdbBackdropResult? { backdrop }
    func loadActor(actor: String, type: String, limit: Int) async throws -> TmdbActorResult? { actor }
    func loadDoubanComments(id: String, start: Int, limit: Int, sort: String) async throws -> [DoubanComment] { comments }
    func loadDoubanRecommends(kind: String, limit: Int, start: Int) async throws -> [DoubanMovie] { recommendations }
    func loadDoubanQuickInfo(id: String) async throws -> DoubanQuickInfo? { quickInfo }
    func suggestDouban(query: String) async throws -> [DoubanSuggestItem] { suggestions }
    func loadCelebrityWorks(name: String, limit: Int, mode: String) async throws -> [DoubanCelebrityWork] { celebrityWorks }
    func refreshTrailer(id: String, force: Bool) async throws -> TrailerRefreshResult? { trailer }
}
