import Foundation

struct BangumiRating: Codable, Hashable {
    var total: Int
    var count: [String: Int]
    var score: Double
}

struct BangumiImages: Codable, Hashable {
    var large: String
    var common: String
    var medium: String
    var small: String
    var grid: String

    var bestImageUrl: String {
        [large, common, medium, small, grid].first { !$0.isEmpty } ?? ""
    }
}

struct BangumiCollection: Codable, Hashable {
    var doing: Int
    var onHold: Int
    var dropped: Int
    var wish: Int
    var collect: Int

    enum CodingKeys: String, CodingKey {
        case doing, dropped, wish, collect
        case onHold = "on_hold"
    }
}

struct BangumiWeekday: Codable, Hashable {
    var en: String
    var cn: String
    var ja: String
    var id: Int
}

struct BangumiItem: Identifiable, Codable, Hashable {
    var id: Int
    var url: String
    var type: Int
    var name: String
    var nameCn: String?
    var summary: String
    var airDate: String
    var airWeekday: Int
    var rating: BangumiRating
    var rank: Int
    var images: BangumiImages
    var collection: BangumiCollection

    enum CodingKeys: String, CodingKey {
        case id, url, type, name, summary, rating, rank, images, collection
        case nameCn = "name_cn"
        case airDate = "air_date"
        case airWeekday = "air_weekday"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(Int.self, forKey: .id)
        url = try container.decodeIfPresent(String.self, forKey: .url) ?? ""
        type = try container.decodeIfPresent(Int.self, forKey: .type) ?? 0
        name = Self.decodeHTMLEntities(try container.decodeIfPresent(String.self, forKey: .name) ?? "")
        nameCn = Self.decodeHTMLEntities(try container.decodeIfPresent(String.self, forKey: .nameCn) ?? "")
        summary = Self.decodeHTMLEntities(try container.decodeIfPresent(String.self, forKey: .summary) ?? "")
        airDate = try container.decodeIfPresent(String.self, forKey: .airDate) ?? ""
        airWeekday = try container.decodeIfPresent(Int.self, forKey: .airWeekday) ?? 0
        rating = try container.decodeIfPresent(BangumiRating.self, forKey: .rating) ?? BangumiRating(total: 0, count: [:], score: 0)
        rank = try container.decodeIfPresent(Int.self, forKey: .rank) ?? 0
        images = try container.decodeIfPresent(BangumiImages.self, forKey: .images) ?? BangumiImages(large: "", common: "", medium: "", small: "", grid: "")
        collection = try container.decodeIfPresent(BangumiCollection.self, forKey: .collection) ?? BangumiCollection(doing: 0, onHold: 0, dropped: 0, wish: 0, collect: 0)
    }

    static func decodeHTMLEntities(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&#39;", with: "'")
    }
}

struct BangumiDetails: Codable, Hashable {
    var id: Int
    var type: Int
    var name: String
    var nameCn: String?
    var summary: String
    var nsfw: Bool
    var locked: Bool
    var date: String?
    var platform: String?
    var images: BangumiImages
    var infobox: [String]
    var eps: Int
    var totalEpisodes: Int
    var rating: BangumiRating
    var collection: BangumiCollection
    var tags: [String]
    var metaTags: [String]
    var series: Bool
}

struct BangumiCalendarResponse: Codable {
    var weekday: BangumiWeekday
    var items: [BangumiItem]
}
