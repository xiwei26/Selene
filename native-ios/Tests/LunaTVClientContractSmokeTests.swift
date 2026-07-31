import Foundation

/// Boxes a mutable value behind an `NSLock` so the single `MockURLProtocol`
/// handler can be swapped between tests under Swift 6 concurrency.
private final class LockedBox<V> {
    private let lock = NSLock()
    private var _wrapped: V
    init(_ wrapped: V) { self._wrapped = wrapped }
    var wrapped: V {
        _read { lock.lock(); defer { lock.unlock() }; yield _wrapped }
        _modify { lock.lock(); defer { lock.unlock() }; yield &_wrapped }
    }
}

private final class MockURLProtocol: URLProtocol {
    nonisolated(unsafe) static var handler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        do {
            guard let handler = Self.handler else { fatalError("Missing request handler") }
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

@main
private struct LunaTVClientContractSmokeTests {
    static func main() async throws {
        try await runClientContracts()
        try runCookieParsing()
        try runPlayURLParsing()
        try runKeychainRoundTrip()
        try runURLNormalizer()
        try runM3UParsing()
        print("LunaTVClient contract smoke tests passed")
    }

    private static func runClientContracts() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: configuration)
        let client = LunaTVClient(
            baseURL: URL(string: "https://luna.example.com")!,
            cookie: "session=test",
            urlSession: session
        )

        var observedRequests: [URLRequest] = []
        MockURLProtocol.handler = { request in
            observedRequests.append(request)
            let path = request.url?.path ?? ""
            let payload: String
            switch path {
            case "/api/login":
                let response = HTTPURLResponse(
                    url: request.url!,
                    statusCode: 200,
                    httpVersion: nil,
                    headerFields: [
                        "Content-Type": "application/json",
                        "Set-Cookie": "session=abc123; Path=/; HttpOnly"
                    ]
                )!
                return (response, Data(#"{"ok":true}"#.utf8))
            case "/api/douban/categories":
                payload = #"{"list":[{"id":"1","title":"动画","poster":"","rate":"9.0","year":"2026"}]}"#
            case "/api/douban/details":
                payload = #"{"data":{"id":"1","title":"动画","poster":"","rating":{"value":"9.0"},"pubdate":["2026-07-31"],"summary":"简介","genres":["动画"],"directors":[{"name":"导演"}],"actors":[{"name":"演员"}],"recommends":[]}}"#
            case "/api/live/sources":
                payload = #"{"success":true,"data":[{"key":"main","name":"主线路","url":"https://live.example.com/list.m3u","ua":"","epg":"","from":"","disabled":false}]}"#
            case "/api/favorites":
                payload = #"{"favorites":{"demo+1":{"title":"收藏影片","source_name":"演示源","year":"2026","cover":"","total_episodes":12,"save_time":100,"search_title":"收藏影片","origin":"vod"}}}"#
            case "/api/playrecords":
                payload = #"{"records":{"demo+1":{"title":"续播影片","source_name":"演示源","year":"2026","cover":"","index":3,"total_episodes":12,"play_time":120,"total_time":1200,"save_time":200,"search_title":"续播影片"}}}"#
            case "/provide/vod":
                payload = #"{"data":{"list":[{"vod_id":"88","vod_name":"本地影片","vod_pic":"https://cms.example.com/p.jpg","vod_year":"2026","vod_remarks":"全 2 集","vod_class":"动画","vod_play_url":"m3u8$$$第01集$https://cms.example.com/1.m3u8#第02集$https://cms.example.com/2.m3u8"}]}}"#
            case "/provide/fail":
                throw URLError(.cannotConnectToHost)
            default:
                payload = #"{"results":[]}"#
            }
            let response = HTTPURLResponse(
                url: request.url!,
                statusCode: 200,
                httpVersion: nil,
                headerFields: ["Content-Type": "application/json"]
            )!
            return (response, Data(payload.utf8))
        }

        let loggedIn = try await client.login(username: "demo", password: "secret")
        precondition(loggedIn.cookie == "session=abc123", "Unexpected cookie: \(loggedIn.cookie)")
        precondition(loggedIn.username == "demo")

        let anime = try await client.category(.anime)
        precondition(anime.first?.title == "动画")
        let categoryRequest = observedRequests.last!
        let query = URLComponents(url: categoryRequest.url!, resolvingAgainstBaseURL: false)?.queryItems ?? []
        precondition(query.contains(URLQueryItem(name: "kind", value: "tv")))
        precondition(query.contains(URLQueryItem(name: "category", value: "tv")))
        precondition(query.contains(URLQueryItem(name: "type", value: "tv_animation")))
        precondition(categoryRequest.value(forHTTPHeaderField: "Cookie") == "session=test")

        let details = try await client.doubanDetails(id: "1")
        precondition(details.year == "2026", "Unexpected Douban year: \(details.year)")
        precondition(details.directors == ["导演"])
        let detailQuery = URLComponents(
            url: observedRequests.last!.url!,
            resolvingAgainstBaseURL: false
        )?.queryItems ?? []
        precondition(detailQuery.contains(URLQueryItem(name: "id", value: "1")))

        let liveSources = try await client.liveSources()
        precondition(liveSources.first?.key == "main")

        let favorites = try await client.favorites()
        precondition(favorites.first?.source == "demo")
        precondition(favorites.first?.totalEpisodes == 12)

        let records = try await client.playRecords()
        precondition(records.first?.episodeNumber == 3)
        precondition(records.first?.playTime == 120)

        let localOutcome = await client.localSearch(
            query: "本地影片",
            resources: [
                SearchResource(key: "cms", name: "演示 CMS", api: "https://cms.example.com/provide/vod"),
                SearchResource(key: "down", name: "故障源", api: "https://cms.example.com/provide/fail")
            ]
        )
        let localRequest = observedRequests.first {
            $0.url?.path == "/provide/vod"
        }!
        let localQuery = URLComponents(
            url: localRequest.url!,
            resolvingAgainstBaseURL: false
        )?.queryItems ?? []
        precondition(localQuery.contains(URLQueryItem(name: "ac", value: "videolist")))
        precondition(localQuery.contains(URLQueryItem(name: "wd", value: "本地影片")))
        precondition(localRequest.value(forHTTPHeaderField: "Cookie") == nil)

        // `{"data":{"list":[...]}}` must unwrap, and the resource tag must survive.
        precondition(localOutcome.results.count == 1, "Unexpected local row count")
        let localRow = localOutcome.results[0]
        precondition(localRow.id == "88")
        precondition(localRow.title == "本地影片")
        precondition(localRow.source == "cms")
        precondition(localRow.sourceName == "演示 CMS")
        precondition(localRow.year == "2026")
        precondition(localRow.remarks == "全 2 集")
        precondition(localRow.isLocal)
        precondition(localRow.localAPI == "https://cms.example.com/provide/vod")
        // First play-from group has no http URLs, so the second one wins, in order.
        precondition(localRow.episodes == [
            "https://cms.example.com/1.m3u8",
            "https://cms.example.com/2.m3u8"
        ], "Episode order or group selection is wrong")
        precondition(localRow.episodeTitles == ["第01集", "第02集"])
        // One failing resource degrades to a warning, not a failed search.
        precondition(localOutcome.totalSources == 2)
        precondition(localOutcome.completedSources == 2)
        precondition(localOutcome.warnings.count == 1, "Expected one source warning")
        precondition(localOutcome.warnings[0].hasPrefix("故障源: "))
    }

    private static func runCookieParsing() throws {
        let url = URL(string: "https://luna.example.com/api/login")!
        let response = HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: nil,
            headerFields: [
                "Set-Cookie": "session=xyz; Path=/; HttpOnly; Secure"
            ]
        )!
        let header = LunaTVClient.cookieHeader(from: response, fallbackURL: url)
        precondition(header == "session=xyz", "Unexpected cookie header: \(header)")

        let bare = HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Set-Cookie": "token=plain"]
        )!
        let bareHeader = LunaTVClient.cookieHeader(from: bare, fallbackURL: url)
        precondition(bareHeader.contains("token=plain"), "Bare cookie parse failed: \(bareHeader)")
    }

    private static func runPlayURLParsing() throws {
        let parsed = LunaTVClient.parsePlayURL(
            "m3u8$$$第01集$https://a.example/1.m3u8#第02集$https://a.example/2.m3u8"
        )
        precondition(parsed.urls.count == 2)
        precondition(parsed.titles == ["第01集", "第02集"])
        precondition(LunaTVClient.parsePlayURL("").urls.isEmpty)
    }

    private static func runKeychainRoundTrip() throws {
        let account = "selene.smoke.\(UUID().uuidString)"
        defer { KeychainStore.delete(account: account) }

        let payload = Data("session-blob".utf8)
        try KeychainStore.save(data: payload, account: account)
        let loaded = try KeychainStore.load(account: account)
        precondition(loaded == payload, "Keychain round-trip failed")

        let updated = Data("session-blob-v2".utf8)
        try KeychainStore.save(data: updated, account: account)
        let reloaded = try KeychainStore.load(account: account)
        precondition(reloaded == updated)

        KeychainStore.delete(account: account)
        let deleted = try KeychainStore.load(account: account)
        precondition(deleted == nil)
    }

    private static func runURLNormalizer() throws {
        precondition(URLNormalizer.normalize("luna.example.com")?.absoluteString == "https://luna.example.com")
        precondition(URLNormalizer.normalize("https://luna.example.com/app/")?.path == "/app")
        precondition(URLNormalizer.normalize("ftp://bad.example") == nil)
        precondition(URLNormalizer.normalize("   ") == nil)
    }

    private static func runM3UParsing() throws {
        let text = """
        #EXTM3U
        #EXTINF:-1 tvg-id="cctv1" tvg-logo="https://logo/1.png" group-title="央视",CCTV-1
        https://live.example.com/cctv1.m3u8
        #EXTINF:-1 group-title="卫视",湖南卫视
        https://live.example.com/hnws.m3u8
        """
        let channels = LocalLiveService.parseM3U(text)
        precondition(channels.count == 2)
        precondition(channels[0].name == "CCTV-1")
        precondition(channels[0].group == "央视")
        precondition(channels[0].tvgID == "cctv1")
        precondition(channels[1].group == "卫视")
    }
}