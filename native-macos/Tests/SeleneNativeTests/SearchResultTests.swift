import XCTest
@testable import SeleneNative

final class SearchResultTests: XCTestCase {
    func testDecodeFromJSON() throws {
        let json = """
        {
            "id": "123",
            "title": "测试视频",
            "poster": "https://example.com/poster.jpg",
            "episodes": ["https://example.com/ep1.m3u8"],
            "episodes_titles": ["第1集"],
            "source": "source1",
            "source_name": "源1",
            "class": "电影",
            "year": "2024",
            "desc": "描述内容",
            "type_name": "电影",
            "douban_id": 12345
        }
        """.data(using: .utf8)!

        let result = try JSONDecoder().decode(SearchResult.self, from: json)
        XCTAssertEqual(result.id, "123")
        XCTAssertEqual(result.title, "测试视频")
        XCTAssertEqual(result.episodes.count, 1)
        XCTAssertEqual(result.episodeTitles.count, 1)
        XCTAssertEqual(result.episodeTitles[0], "第1集")
        XCTAssertEqual(result.source, "source1")
        XCTAssertEqual(result.sourceName, "源1")
        XCTAssertEqual(result.className, "电影")
        XCTAssertEqual(result.year, "2024")
        XCTAssertEqual(result.description, "描述内容")
        XCTAssertEqual(result.typeName, "电影")
        XCTAssertEqual(result.doubanID, 12345)
    }

    func testDecodeWithMissingOptionalFields() throws {
        let json = """
        {
            "id": "123",
            "title": "测试",
            "poster": "",
            "episodes": [],
            "episodes_titles": [],
            "source": "s",
            "source_name": "",
            "year": ""
        }
        """.data(using: .utf8)!

        let result = try JSONDecoder().decode(SearchResult.self, from: json)
        XCTAssertEqual(result.id, "123")
        XCTAssertEqual(result.description, "")
        XCTAssertEqual(result.typeName, "")
        XCTAssertNil(result.doubanID)
    }

    func testEpisodeTitleFallback() {
        let result = SearchResult(
            id: "1", title: "测试", poster: "",
            episodes: ["url1", "url2"],
            episodeTitles: [],
            source: "s", sourceName: "", year: "2024"
        )
        XCTAssertEqual(result.episodeTitle(for: 0), "第1集")
        XCTAssertEqual(result.episodeTitle(for: 1), "第2集")
    }

    func testEpisodeTitleWithTitleList() {
        let result = SearchResult(
            id: "1", title: "测试", poster: "",
            episodes: ["url1"],
            episodeTitles: ["第一集"],
            source: "s", sourceName: "", year: "2024"
        )
        XCTAssertEqual(result.episodeTitle(for: 0), "第一集")
    }
}
