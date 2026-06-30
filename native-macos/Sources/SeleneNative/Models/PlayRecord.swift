import Foundation

struct PlayRecord: Identifiable, Codable, Hashable {
    let id: String
    let source: String
    var title: String
    var sourceName: String
    var year: String
    var cover: String
    var index: Int
    var totalEpisodes: Int
    var playTime: Int
    var totalTime: Int
    var saveTime: Int64
    var searchTitle: String
    var originalEpisodes: Int? = nil
    var remarks: String? = nil
    var doubanID: Int? = nil
    var type: String? = nil

    enum CodingKeys: String, CodingKey {
        case id, source, title, year, cover, index
        case sourceName = "source_name"
        case totalEpisodes = "total_episodes"
        case playTime = "play_time"
        case totalTime = "total_time"
        case saveTime = "save_time"
        case searchTitle = "search_title"
        case originalEpisodes = "original_episodes"
        case remarks, type
        case doubanID = "douban_id"
    }

    var itemId: String {
        splitKey(id).itemId
    }

    var fullId: String {
        id
    }

    var episodeIndex: Int {
        max(index - 1, 0)
    }

    var episodeNumber: Int {
        max(index, 1)
    }

    var progressPercentage: Double {
        guard totalTime > 0 else { return 0 }
        return min(max(Double(playTime) / Double(totalTime), 0), 1)
    }

    var formattedPlayTime: String {
        Self.formatSeconds(playTime)
    }

    var formattedTotalTime: String {
        Self.formatSeconds(totalTime)
    }

    static func fromJson(key: String, data: [String: Any]) -> PlayRecord {
        let parts = splitKey(key)
        return PlayRecord(
            id: key,
            source: parts.source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? data["sourceName"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? data["poster"] as? String ?? "",
            index: intValue(data["index"]) ?? 0,
            totalEpisodes: intValue(data["total_episodes"] ?? data["totalEpisodes"]) ?? 0,
            playTime: intValue(data["play_time"] ?? data["playTime"]) ?? 0,
            totalTime: intValue(data["total_time"] ?? data["totalTime"]) ?? 0,
            saveTime: int64Value(data["save_time"] ?? data["saveTime"]) ?? Int64(Date().timeIntervalSince1970 * 1000),
            searchTitle: data["search_title"] as? String ?? data["searchTitle"] as? String ?? "",
            originalEpisodes: intValue(data["original_episodes"] ?? data["originalEpisodes"]),
            remarks: data["remarks"] as? String,
            doubanID: intValue(data["douban_id"] ?? data["doubanID"]),
            type: data["type"] as? String
        )
    }

    func toJson() -> [String: Any] {
        var json: [String: Any] = [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "index": index,
            "total_episodes": totalEpisodes,
            "play_time": playTime,
            "total_time": totalTime,
            "save_time": saveTime,
            "search_title": searchTitle
        ]
        json["original_episodes"] = originalEpisodes ?? totalEpisodes
        json["remarks"] = remarks
        json["douban_id"] = doubanID
        json["type"] = type
        return json
    }

    private static func formatSeconds(_ seconds: Int) -> String {
        let clamped = max(seconds, 0)
        let hours = clamped / 3600
        let minutes = (clamped % 3600) / 60
        let seconds = clamped % 60
        if hours > 0 {
            return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
        }
        return String(format: "%02d:%02d", minutes, seconds)
    }
}
