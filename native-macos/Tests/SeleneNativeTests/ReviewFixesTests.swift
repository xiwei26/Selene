import XCTest
@testable import SeleneNative

@MainActor
final class ReviewFixesTests: XCTestCase {
    func testServerAPIClientSendsCookieOnSearchRequests() async throws {
        RequestCaptureURLProtocol.handler = { request in
            XCTAssertEqual(request.value(forHTTPHeaderField: "Cookie"), "session=abc")
            let data = #"{"results":[]}"#.data(using: .utf8)!
            let response = HTTPURLResponse(
                url: request.url!,
                statusCode: 200,
                httpVersion: nil,
                headerFields: ["Content-Type": "application/json"]
            )!
            return (response, data)
        }

        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [RequestCaptureURLProtocol.self]
        let session = URLSession(configuration: configuration)
        let client = ServerAPIClient(
            baseURL: URL(string: "https://example.com")!,
            cookie: "session=abc",
            session: session
        )

        _ = try await client.search(query: "test")
    }

    func testSSEProgressClearsStaleErrorsAfterSuccessfulEvent() {
        let client = SSESearchClient()
        var progress = SSESearchClient.SearchProgress(totalSources: 2)

        progress = client.handle(
            event: (type: "sourceError", data: ["sourceName": "A", "error": "failed"]),
            currentProgress: progress
        )
        XCTAssertEqual(progress.error, "failed")

        progress = client.handle(
            event: (type: "sourceResult", data: ["sourceName": "B", "results": []]),
            currentProgress: progress
        )
        XCTAssertNil(progress.error)

        progress = client.handle(event: (type: "complete", data: [:]), currentProgress: progress)
        XCTAssertNil(progress.error)
    }

    func testCacheServiceDoesNotCollideSimilarKeys() throws {
        let cache = CacheService(namespace: "ReviewFixesTests-\(UUID().uuidString)")
        defer { cache.clearAll() }

        try cache.save(key: "a+b", data: ["plus"], maxAge: 60)
        try cache.save(key: "a_b", data: ["underscore"], maxAge: 60)

        let plus: [String]? = cache.load(key: "a+b", maxAge: 60)
        let underscore: [String]? = cache.load(key: "a_b", maxAge: 60)

        XCTAssertEqual(plus, ["plus"])
        XCTAssertEqual(underscore, ["underscore"])
    }

    func testAggregatedFilteringRemovesBlockedOriginalResults() {
        let store = SearchStore(provider: EmptyContentProvider())
        let blocked = SearchResult(
            id: "1",
            title: "Shared",
            poster: "",
            episodes: ["blocked"],
            episodeTitles: [],
            source: "blocked",
            sourceName: "Blocked",
            year: "2024",
            description: "广告"
        )
        let clean = SearchResult(
            id: "2",
            title: "Shared",
            poster: "",
            episodes: ["clean"],
            episodeTitles: [],
            source: "clean",
            sourceName: "Clean",
            year: "2024"
        )

        store.results = [blocked, clean]
        store.blockedKeywordsText = "广告"

        XCTAssertEqual(store.filteredAggregatedResults.count, 1)
        XCTAssertEqual(store.filteredAggregatedResults[0].originalResults.map(\.source), ["clean"])
    }

    func testM3UChannelIDsIncludeTvgIdWhenPresent() throws {
        let m3u = """
        #EXTM3U
        #EXTINF:-1 tvg-id="a" group-title="G",Shared
        https://example.com/shared.m3u8
        #EXTINF:-1 tvg-id="b" group-title="G",Shared
        https://example.com/shared.m3u8
        """

        let channels = try LiveServiceClient.parseM3U(m3u)

        XCTAssertEqual(Set(channels.map(\.id)).count, 2)
    }

    func testSubscriptionFallbackKeyIsStableWhenNameIsMissing() {
        let json = """
        {"lives":[{"url":"https://example.com/live.m3u","disabled":false}]}
        """
        let encoded = Base58.encode([UInt8](json.utf8))

        let first = SubscriptionService.parseSubscriptionContent(encoded)?.liveSources?.first?.key
        let second = SubscriptionService.parseSubscriptionContent(encoded)?.liveSources?.first?.key

        XCTAssertEqual(first, second)
        XCTAssertEqual(first, "https://example.com/live.m3u")
    }

    func testPlayerStoreLoadsHistoryRecordWithOriginalItemID() async {
        let provider = CapturingDetailProvider()
        let record = PlayRecord(
            id: "source-a+video-1",
            source: "source-a",
            title: "Video",
            sourceName: "Source A",
            year: "2024",
            cover: "",
            index: 1,
            totalEpisodes: 2,
            playTime: 30,
            totalTime: 120,
            saveTime: 1,
            searchTitle: "Video"
        )
        let store = PlayerStore()

        await store.loadDetailAndPlay(record: record, provider: provider)

        XCTAssertEqual(provider.requestedSource, "source-a")
        XCTAssertEqual(provider.requestedID, "video-1")
        XCTAssertEqual(store.currentEpisodeIndex, 0)
        XCTAssertEqual(store.currentEpisodeURL?.absoluteString, "https://example.com/1.mp4")
    }

    func testPlayerStoreSavesPlayRecordWithOneBasedEpisodeNumber() {
        let store = PlayerStore()
        let result = SearchResult(
            id: "video-1",
            title: "Video",
            poster: "",
            episodes: ["https://example.com/1.mp4"],
            episodeTitles: [],
            source: "source-a",
            sourceName: "Source A",
            year: "2024"
        )

        store.loadEpisode(url: URL(string: "https://example.com/1.mp4")!, result: result, index: 0)

        XCTAssertEqual(store.makePlayRecord()?.index, 1)
    }
}

final class RequestCaptureURLProtocol: URLProtocol {
    static var handler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = Self.handler else {
            client?.urlProtocol(self, didFailWithError: APIError.unknown)
            return
        }
        do {
            let (response, data) = try handler(request)
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}
}

private struct EmptyContentProvider: ContentProvider {
    func login(username: String, password: String) async throws -> LoginSession { throw APIError.unknown }
    func search(query: String) async throws -> [SearchResult] { [] }
    func detail(source: String, id: String) async throws -> SearchResult? { nil }
    func searchResources() async throws -> [SearchResource] { [] }
    func getFavorites() async throws -> [FavoriteItem] { [] }
    func addFavorite(source: String, id: String, data: [String: Any]) async throws {}
    func removeFavorite(source: String, id: String) async throws {}
    func savePlayRecord(_ record: PlayRecord) async throws {}
    func deletePlayRecord(source: String, id: String) async throws {}
    func clearPlayRecords() async throws {}
    func getPlayRecords() async throws -> [PlayRecord] { [] }
    func getSearchHistory() async throws -> [String] { [] }
    func addSearchHistory(query: String) async throws {}
    func deleteSearchHistory(query: String) async throws {}
    func clearSearchHistory() async throws {}
    func searchSuggestions(query: String) async throws -> [SearchSuggestion] { [] }
    func getLiveSources() async throws -> [LiveSource] { [] }
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel] { [] }
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData? { nil }
    func sseSearchURL(query: String) -> URL? { nil }
}

private final class CapturingDetailProvider: ContentProvider, @unchecked Sendable {
    var requestedSource: String?
    var requestedID: String?

    func login(username: String, password: String) async throws -> LoginSession { throw APIError.unknown }
    func search(query: String) async throws -> [SearchResult] { [] }
    func detail(source: String, id: String) async throws -> SearchResult? {
        requestedSource = source
        requestedID = id
        return SearchResult(
            id: id,
            title: "Video",
            poster: "",
            episodes: ["https://example.com/1.mp4", "https://example.com/2.mp4"],
            episodeTitles: [],
            source: source,
            sourceName: "Source A",
            year: "2024"
        )
    }
    func searchResources() async throws -> [SearchResource] { [] }
    func getFavorites() async throws -> [FavoriteItem] { [] }
    func addFavorite(source: String, id: String, data: [String: Any]) async throws {}
    func removeFavorite(source: String, id: String) async throws {}
    func savePlayRecord(_ record: PlayRecord) async throws {}
    func deletePlayRecord(source: String, id: String) async throws {}
    func clearPlayRecords() async throws {}
    func getPlayRecords() async throws -> [PlayRecord] { [] }
    func getSearchHistory() async throws -> [String] { [] }
    func addSearchHistory(query: String) async throws {}
    func deleteSearchHistory(query: String) async throws {}
    func clearSearchHistory() async throws {}
    func searchSuggestions(query: String) async throws -> [SearchSuggestion] { [] }
    func getLiveSources() async throws -> [LiveSource] { [] }
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel] { [] }
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData? { nil }
    func sseSearchURL(query: String) -> URL? { nil }
}
