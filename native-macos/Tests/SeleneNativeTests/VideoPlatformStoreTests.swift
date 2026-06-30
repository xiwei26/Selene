import XCTest
@testable import SeleneNative

final class VideoPlatformStoreTests: XCTestCase {
    @MainActor
    func testLoadInitialLoadsBilibiliPopular() async {
        let provider = FakeVideoPlatformProvider(
            bilibiliPopular: VideoPlatformPage(items: [VideoPlatformItem(id: "BV1", title: "Popular")])
        )
        let store = VideoPlatformStore(provider: provider, kind: .bilibili)

        await store.loadInitial()

        XCTAssertEqual(store.items.first?.title, "Popular")
    }

    @MainActor
    func testLoadInitialLoadsYouTubeRegionsAndPopular() async {
        let provider = FakeVideoPlatformProvider(
            regions: [YouTubeRegion(code: "US", name: "United States")],
            youtubePopular: VideoPlatformPage(items: [VideoPlatformItem(id: "yt1", title: "Trending")])
        )
        let store = VideoPlatformStore(provider: provider, kind: .youtube)

        await store.loadInitial()

        XCTAssertEqual(store.regions.count, 1)
        XCTAssertEqual(store.items.count, 1)
    }

    @MainActor
    func testDirectPlayRequestBuildsSearchResultForHistory() {
        let store = VideoPlatformStore(provider: FakeVideoPlatformProvider(), kind: .bilibili)
        let item = VideoPlatformItem(
            id: "BV1",
            title: "Playable",
            cover: "c.jpg",
            publishedAt: "2026",
            playableUrl: "https://video.example/bv1.m3u8",
            url: "https://www.bilibili.com/video/BV1"
        )

        let request = store.directPlayRequest(for: item)

        XCTAssertEqual(request?.url.absoluteString, "https://video.example/bv1.m3u8")
        XCTAssertEqual(request?.result.source, "bilibili")
        XCTAssertEqual(request?.result.sourceName, "Bilibili")
        XCTAssertEqual(request?.result.episodes, ["https://video.example/bv1.m3u8"])
        XCTAssertEqual(request?.result.poster, "c.jpg")
        XCTAssertEqual(request?.result.year, "2026")
        XCTAssertEqual(request?.index, 0)
    }

    @MainActor
    func testOrdinaryWebpageUrlIsNotPlayable() {
        let store = VideoPlatformStore(provider: FakeVideoPlatformProvider(), kind: .youtube)
        let item = VideoPlatformItem(
            id: "yt1",
            title: "Web Only",
            url: "https://www.youtube.com/watch?v=yt1"
        )

        XCTAssertNil(store.directPlayRequest(for: item))
        XCTAssertEqual(store.errorMessage, "Current item has no playable URL")
    }
}

private struct FakeVideoPlatformProvider: VideoPlatformProviding {
    var bilibiliPopular = VideoPlatformPage(items: [])
    var bilibiliSearch = VideoPlatformPage(items: [])
    var youtubePopular = VideoPlatformPage(items: [])
    var youtubeSearch = VideoPlatformPage(items: [])
    var regions: [YouTubeRegion] = []

    func loadBilibiliPopular(page: Int, pageSize: Int) async throws -> VideoPlatformPage { bilibiliPopular }
    func searchBilibili(query: String) async throws -> VideoPlatformPage { bilibiliSearch }
    func loadYouTubePopular(regionCode: String, pageToken: String?) async throws -> VideoPlatformPage { youtubePopular }
    func searchYouTube(query: String, contentType: String, order: String, maxResults: Int) async throws -> VideoPlatformPage { youtubeSearch }
    func loadYouTubeRegions() async throws -> [YouTubeRegion] { regions }
}
