import Foundation

struct LiveSource: Identifiable, Codable, Hashable {
    var id: String { key }
    var key: String
    var name: String
    var url: String
    var ua: String
    var epg: String
    var from: String
    var disabled: Bool
}

struct LiveChannel: Identifiable, Codable, Hashable {
    var id: String
    var tvgId: String
    var name: String
    var logo: String
    var group: String
    var url: String
    var isFavorite: Bool

    enum CodingKeys: String, CodingKey {
        case id, name, logo, group, url
        case tvgId = "tvg_id"
        case isFavorite = "is_favorite"
    }
}

struct LiveChannelGroup: Identifiable, Hashable {
    var id: String { name }
    let name: String
    let channels: [LiveChannel]
}

struct EpgProgram: Identifiable, Codable, Hashable {
    var id: String { "\(channelId)-\(startTime.timeIntervalSince1970)" }
    var channelId: String
    var title: String
    var startTime: Date
    var endTime: Date
    var description: String?

    enum CodingKeys: String, CodingKey {
        case title, description
        case channelId = "channel_id"
        case startTime = "start_time"
        case endTime = "end_time"
    }

    var isLive: Bool {
        let now = Date()
        return startTime <= now && now < endTime
    }

    var progress: Double {
        let total = endTime.timeIntervalSince(startTime)
        guard total > 0 else { return 0 }
        return min(max(Date().timeIntervalSince(startTime) / total, 0), 1)
    }

    var timeRange: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return "\(formatter.string(from: startTime)) - \(formatter.string(from: endTime))"
    }
}

struct EpgData: Codable, Hashable {
    var tvgId: String
    var source: String
    var epgUrl: String
    var programs: [EpgProgram]

    enum CodingKeys: String, CodingKey {
        case source, programs
        case tvgId = "tvg_id"
        case epgUrl = "epg_url"
    }
}
