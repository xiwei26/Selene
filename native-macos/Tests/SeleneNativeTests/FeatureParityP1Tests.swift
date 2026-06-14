import XCTest
@testable import SeleneNative

final class FeatureParityP1Tests: XCTestCase {
    func testAggregatedSearchResultGroupsEquivalentResults() {
        var aggregate = AggregatedSearchResult.fromSearchResult(
            SearchResult(
                id: "one",
                title: "Example",
                poster: "poster-a",
                episodes: ["1", "2"],
                episodeTitles: [],
                source: "alpha",
                sourceName: "Alpha",
                year: "2024",
                typeName: "movie",
                doubanID: 42
            )
        )

        aggregate.addResult(
            SearchResult(
                id: "two",
                title: "Example",
                poster: "poster-b",
                episodes: ["1", "2", "3"],
                episodeTitles: [],
                source: "beta",
                sourceName: "Beta",
                year: "2024",
                typeName: "movie",
                doubanID: 42
            )
        )

        XCTAssertEqual(aggregate.key, "Example|2024|movie")
        XCTAssertEqual(aggregate.sourceNames, ["Alpha", "Beta"])
        XCTAssertEqual(aggregate.episodeCounts["Alpha"], 2)
        XCTAssertEqual(aggregate.episodeCounts["Beta"], 3)
        XCTAssertEqual(aggregate.mostCommonEpisodeCount, 2)
        XCTAssertEqual(aggregate.mostCommonDoubanId, "42")
        XCTAssertEqual(aggregate.originalResults.count, 2)
    }

    func testFavoriteItemRoundTripsServerMapValue() {
        let item = FavoriteItem.fromJson(
            key: "source-a+video-1",
            data: [
                "title": "Video",
                "source_name": "Source A",
                "year": "2024",
                "cover": "https://example.com/poster.jpg",
                "total_episodes": 12,
                "save_time": 1_717_000_000_000
            ]
        )

        XCTAssertEqual(item.id, "source-a+video-1")
        XCTAssertEqual(item.source, "source-a")
        XCTAssertEqual(item.title, "Video")
        XCTAssertEqual(item.totalEpisodes, 12)

        let json = item.toJson()
        XCTAssertNil(json["id"])
        XCTAssertNil(json["source"])
        XCTAssertEqual(json["title"] as? String, "Video")
        XCTAssertEqual(json["total_episodes"] as? Int, 12)
    }

    func testPlayRecordFormatsProgressAndRoundTripsServerMapValue() {
        let record = PlayRecord.fromJson(
            key: "source-a+video-1",
            data: [
                "title": "Video",
                "source_name": "Source A",
                "year": "2024",
                "cover": "cover",
                "index": 3,
                "total_episodes": 12,
                "play_time": 90,
                "total_time": 300,
                "save_time": 1_717_000_000_000,
                "search_title": "Video Search"
            ]
        )

        XCTAssertEqual(record.source, "source-a")
        XCTAssertEqual(record.progressPercentage, 0.3, accuracy: 0.0001)
        XCTAssertEqual(record.formattedPlayTime, "01:30")
        XCTAssertEqual(record.formattedTotalTime, "05:00")
        XCTAssertEqual(record.toJson()["search_title"] as? String, "Video Search")
    }

    func testSearchSuggestionDecodesFromServerPayload() throws {
        let data = """
        {"text":"example","type":"history","score":0.75}
        """.data(using: .utf8)!

        let suggestion = try JSONDecoder().decode(SearchSuggestion.self, from: data)

        XCTAssertEqual(suggestion.text, "example")
        XCTAssertEqual(suggestion.type, "history")
        XCTAssertEqual(suggestion.score, 0.75)
    }
}
