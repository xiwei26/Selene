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

    enum CodingKeys: String, CodingKey {
        case key, name, url, ua, epg, from, disabled
    }

    init(key: String, name: String, url: String, ua: String = "", epg: String = "", from: String = "", disabled: Bool = false) {
        self.key = key
        self.name = name
        self.url = url
        self.ua = ua
        self.epg = epg
        self.from = from
        self.disabled = disabled
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        key = try container.decode(String.self, forKey: .key)
        name = try container.decode(String.self, forKey: .name)
        url = try container.decode(String.self, forKey: .url)
        ua = try container.decodeIfPresent(String.self, forKey: .ua) ?? ""
        epg = try container.decodeIfPresent(String.self, forKey: .epg) ?? ""
        from = try container.decodeIfPresent(String.self, forKey: .from) ?? ""
        disabled = try container.decodeIfPresent(Bool.self, forKey: .disabled) ?? false
    }
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

    enum AlternateCodingKeys: String, CodingKey {
        case tvgId, isFavorite
    }

    init(id: String, tvgId: String, name: String, logo: String, group: String, url: String, isFavorite: Bool) {
        self.id = id
        self.tvgId = tvgId
        self.name = name
        self.logo = logo
        self.group = group
        self.url = url
        self.isFavorite = isFavorite
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let alternate = try decoder.container(keyedBy: AlternateCodingKeys.self)
        id = try container.decodeIfPresent(String.self, forKey: .id)
            ?? "\(try container.decode(String.self, forKey: .name))-\(try container.decode(String.self, forKey: .url))"
        tvgId = try container.decodeIfPresent(String.self, forKey: .tvgId)
            ?? (try alternate.decodeIfPresent(String.self, forKey: .tvgId))
            ?? ""
        name = try container.decode(String.self, forKey: .name)
        logo = try container.decodeIfPresent(String.self, forKey: .logo) ?? ""
        group = try container.decodeIfPresent(String.self, forKey: .group) ?? "未分组"
        url = try container.decode(String.self, forKey: .url)
        isFavorite = try container.decodeIfPresent(Bool.self, forKey: .isFavorite)
            ?? (try alternate.decodeIfPresent(Bool.self, forKey: .isFavorite))
            ?? false
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
        case title, description, start, end
        case channelId = "channel_id"
        case startTime = "start_time"
        case endTime = "end_time"
    }

    init(channelId: String, title: String, startTime: Date, endTime: Date, description: String? = nil) {
        self.channelId = channelId
        self.title = title
        self.startTime = startTime
        self.endTime = endTime
        self.description = description
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        title = try container.decode(String.self, forKey: .title)
        description = try container.decodeIfPresent(String.self, forKey: .description)
        channelId = try container.decodeIfPresent(String.self, forKey: .channelId) ?? ""
        startTime = try Self.decodeDate(from: container, preferred: .startTime, fallback: .start)
        endTime = try Self.decodeDate(from: container, preferred: .endTime, fallback: .end)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(channelId, forKey: .channelId)
        try container.encode(title, forKey: .title)
        try container.encode(startTime, forKey: .startTime)
        try container.encode(endTime, forKey: .endTime)
        try container.encodeIfPresent(description, forKey: .description)
    }

    private static func decodeDate(
        from container: KeyedDecodingContainer<CodingKeys>,
        preferred: CodingKeys,
        fallback: CodingKeys
    ) throws -> Date {
        if let date = try? container.decode(Date.self, forKey: preferred) {
            return date
        }
        if let value = try container.decodeIfPresent(String.self, forKey: preferred) ?? container.decodeIfPresent(String.self, forKey: fallback) {
            return parseDate(value) ?? Date(timeIntervalSince1970: 0)
        }
        return Date(timeIntervalSince1970: 0)
    }

    private static func parseDate(_ value: String) -> Date? {
        let iso = ISO8601DateFormatter()
        if let date = iso.date(from: value) {
            return date
        }

        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        for format in ["yyyyMMddHHmmss Z", "yyyy-MM-dd HH:mm:ss Z", "yyyy-MM-dd'T'HH:mm:ssZ"] {
            formatter.dateFormat = format
            if let date = formatter.date(from: value) {
                return date
            }
        }
        return nil
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

    enum AlternateCodingKeys: String, CodingKey {
        case tvgId, epgUrl
    }

    init(tvgId: String, source: String, epgUrl: String, programs: [EpgProgram]) {
        self.tvgId = tvgId
        self.source = source
        self.epgUrl = epgUrl
        self.programs = programs
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let alternate = try decoder.container(keyedBy: AlternateCodingKeys.self)
        tvgId = try container.decodeIfPresent(String.self, forKey: .tvgId)
            ?? (try alternate.decodeIfPresent(String.self, forKey: .tvgId))
            ?? ""
        source = try container.decodeIfPresent(String.self, forKey: .source) ?? ""
        epgUrl = try container.decodeIfPresent(String.self, forKey: .epgUrl)
            ?? (try alternate.decodeIfPresent(String.self, forKey: .epgUrl))
            ?? ""
        programs = try container.decodeIfPresent([EpgProgram].self, forKey: .programs) ?? []
    }
}
