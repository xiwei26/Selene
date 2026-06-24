import XCTest
@testable import SeleneNative

final class FeatureParityP2Tests: XCTestCase {
    func testDoubanMovieDecodesCardSubtitleYearAndPosterFallback() throws {
        let data = """
        {
          "id": "1292052",
          "title": "测试电影",
          "pic": {"large": "https://example.com/large.jpg"},
          "rating": {"value": "9.6"},
          "card_subtitle": "1994 / 美国 / 剧情"
        }
        """.data(using: .utf8)!

        let movie = try JSONDecoder().decode(DoubanMovie.self, from: data)

        XCTAssertEqual(movie.id, "1292052")
        XCTAssertEqual(movie.poster, "https://example.com/large.jpg")
        XCTAssertEqual(movie.rate, "9.6")
        XCTAssertEqual(movie.year, "1994")
    }

    func testDoubanMovieDetailsUsesNestedRatingAndPubdateYear() throws {
        let data = """
        {
          "id": "1292052",
          "title": "测试详情",
          "pic": {"normal": "https://example.com/normal.jpg"},
          "rating": {"average": "9.7"},
          "pubdate": ["1994-09-10"],
          "summary": "简介",
          "genres": ["剧情"],
          "directors": [{"name": "导演"}],
          "actors": [{"name": "演员"}],
          "recommends": [{"id": "1", "title": "推荐", "poster": "p", "rate": "8.0"}]
        }
        """.data(using: .utf8)!

        let details = try JSONDecoder().decode(DoubanMovieDetails.self, from: data)

        XCTAssertEqual(details.poster, "https://example.com/normal.jpg")
        XCTAssertEqual(details.rate, "9.7")
        XCTAssertEqual(details.year, "1994")
        XCTAssertEqual(details.directors, ["导演"])
        XCTAssertEqual(details.actors, ["演员"])
        XCTAssertEqual(details.recommends.count, 1)
    }

    func testBangumiItemDecodesHTMLEntitiesAndBestImage() throws {
        let data = """
        {
          "id": 1,
          "url": "https://bgm.tv/subject/1",
          "type": 2,
          "name": "A &amp; B",
          "name_cn": "甲 &lt;乙&gt;",
          "summary": "Line &quot;one&quot;",
          "air_date": "2024-01-01",
          "air_weekday": 1,
          "rating": {"total": 1, "count": {"10": 1}, "score": 8.5},
          "rank": 100,
          "images": {"large": "", "common": "common.jpg", "medium": "", "small": "", "grid": ""},
          "collection": {"doing": 1, "on_hold": 2, "dropped": 3, "wish": 4, "collect": 5}
        }
        """.data(using: .utf8)!

        let item = try JSONDecoder().decode(BangumiItem.self, from: data)

        XCTAssertEqual(item.name, "A & B")
        XCTAssertEqual(item.nameCn, "甲 <乙>")
        XCTAssertEqual(item.summary, "Line \"one\"")
        XCTAssertEqual(item.images.bestImageUrl, "common.jpg")
    }

    func testBangumiItemToleratesNullNestedFieldsFromAPI() throws {
        let data = """
        {
          "id": 2,
          "name": "Null Fields",
          "images": {"large": null, "common": "common.jpg", "medium": null},
          "rating": {"total": null, "count": null, "score": null},
          "collection": {"doing": null, "wish": 3}
        }
        """.data(using: .utf8)!

        let item = try JSONDecoder().decode(BangumiItem.self, from: data)

        XCTAssertEqual(item.images.bestImageUrl, "common.jpg")
        XCTAssertEqual(item.rating.total, 0)
        XCTAssertEqual(item.collection.wish, 3)
        XCTAssertEqual(item.collection.doing, 0)
    }

    func testAPIErrorDescriptionWorksThroughErrorProtocol() {
        let error: Error = APIError.parseError
        XCTAssertEqual(error.localizedDescription, "数据解析失败")
    }

    func testCacheServiceExpiresEntries() throws {
        let cache = CacheService(namespace: "FeatureParityP2Tests-\(UUID().uuidString)")
        try cache.save(key: "fresh", data: ["value"], maxAge: 60)
        try cache.save(key: "expired", data: ["old"], maxAge: -1)

        let fresh: [String]? = cache.load(key: "fresh", maxAge: 60)
        let expired: [String]? = cache.load(key: "expired", maxAge: 60)

        XCTAssertEqual(fresh, ["value"])
        XCTAssertNil(expired)
        cache.clearAll()
    }
}
