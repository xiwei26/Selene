import Foundation

struct FavoriteItem: Identifiable, Codable, Hashable {
    let id: String
    let source: String
    var title: String
    var sourceName: String
    var year: String
    var cover: String
    var totalEpisodes: Int
    var saveTime: Int64
    var searchTitle: String
    var origin: String
    var type: String?
    var releaseDate: String?
    var remarks: String?

    enum CodingKeys: String, CodingKey {
        case id, source, title, year, cover
        case sourceName = "source_name"
        case totalEpisodes = "total_episodes"
        case saveTime = "save_time"
        case searchTitle = "search_title"
        case origin, type, releaseDate, remarks
    }

    static func fromJson(key: String, data: [String: Any]) -> FavoriteItem {
        let parts = splitKey(key)
        return FavoriteItem(
            id: key,
            source: parts.source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? data["sourceName"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? data["poster"] as? String ?? "",
            totalEpisodes: intValue(data["total_episodes"] ?? data["totalEpisodes"]) ?? 0,
            saveTime: int64Value(data["save_time"] ?? data["saveTime"]) ?? Int64(Date().timeIntervalSince1970 * 1000),
            searchTitle: data["search_title"] as? String ?? data["searchTitle"] as? String ?? data["title"] as? String ?? "",
            origin: data["origin"] as? String ?? "vod",
            type: data["type"] as? String,
            releaseDate: data["releaseDate"] as? String ?? data["release_date"] as? String,
            remarks: data["remarks"] as? String
        )
    }

    func toJson() -> [String: Any] {
        var json: [String: Any] = [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "total_episodes": totalEpisodes,
            "save_time": saveTime,
            "search_title": searchTitle,
            "origin": origin
        ]
        json["type"] = type
        json["releaseDate"] = releaseDate
        json["remarks"] = remarks
        return json
    }
}

func splitKey(_ key: String) -> (source: String, itemId: String) {
    guard let separator = key.firstIndex(of: "+") else {
        return ("", key)
    }
    return (String(key[..<separator]), String(key[key.index(after: separator)...]))
}

func intValue(_ value: Any?) -> Int? {
    switch value {
    case let value as Int:
        return value
    case let value as Int64:
        return Int(value)
    case let value as Double:
        return Int(value)
    case let value as String:
        return Int(value)
    default:
        return nil
    }
}

func int64Value(_ value: Any?) -> Int64? {
    switch value {
    case let value as Int64:
        return value
    case let value as Int:
        return Int64(value)
    case let value as Double:
        return Int64(value)
    case let value as String:
        return Int64(value)
    default:
        return nil
    }
}
