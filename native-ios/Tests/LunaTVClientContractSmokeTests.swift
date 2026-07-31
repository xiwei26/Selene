import Foundation

private final class MockURLProtocol: URLProtocol {
    static var handler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

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

        print("LunaTVClient contract smoke tests passed")
    }
}