import Foundation

struct SearchResource: Codable, Identifiable, Hashable {
    let id: String
    let key: String
    let name: String
    let api: String
    let detail: String
    let from: String
    let disabled: Bool

    init(key: String, name: String, api: String, detail: String = "", from: String = "", disabled: Bool = false) {
        id = key
        self.key = key
        self.name = name
        self.api = api
        self.detail = detail
        self.from = from
        self.disabled = disabled
    }

    enum CodingKeys: String, CodingKey {
        case key, name, api, detail, from, disabled
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        key = try container.decode(String.self, forKey: .key)
        id = key
        name = try container.decode(String.self, forKey: .name)
        api = try container.decodeIfPresent(String.self, forKey: .api) ?? ""
        detail = try container.decodeIfPresent(String.self, forKey: .detail) ?? ""
        from = try container.decodeIfPresent(String.self, forKey: .from) ?? ""
        disabled = try container.decodeIfPresent(Bool.self, forKey: .disabled) ?? false
    }
}

struct SearchSuggestion: Codable, Hashable, Identifiable {
    var id: String { text }
    let text: String
    let type: String
    let score: Double
}

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

    static func fromJSON(key: String, data: [String: Any]) -> FavoriteItem {
        let parts = splitMediaKey(key)
        return FavoriteItem(
            id: key,
            source: parts.source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? data["sourceName"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? data["poster"] as? String ?? "",
            totalEpisodes: flexibleInt(data["total_episodes"] ?? data["totalEpisodes"]) ?? 0,
            saveTime: flexibleInt64(data["save_time"] ?? data["saveTime"]) ?? Int64(Date().timeIntervalSince1970 * 1_000),
            searchTitle: data["search_title"] as? String ?? data["title"] as? String ?? "",
            origin: data["origin"] as? String ?? "vod",
            type: data["type"] as? String,
            releaseDate: data["releaseDate"] as? String ?? data["release_date"] as? String,
            remarks: data["remarks"] as? String
        )
    }

    func jsonValue() -> [String: Any] {
        [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "total_episodes": totalEpisodes,
            "save_time": saveTime,
            "search_title": searchTitle,
            "origin": origin,
            "type": type as Any,
            "releaseDate": releaseDate as Any,
            "remarks": remarks as Any
        ]
    }
}

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
    var remarks: String? = nil
    var doubanID: Int? = nil
    var type: String? = nil

    var itemID: String { splitMediaKey(id).itemID }
    var episodeIndex: Int { max(index - 1, 0) }
    var episodeNumber: Int { max(index, 1) }
    var progress: Double {
        guard totalTime > 0 else { return 0 }
        return min(max(Double(playTime) / Double(totalTime), 0), 1)
    }
    var formattedTime: String {
        "\(Self.format(playTime)) / \(Self.format(totalTime))"
    }

    static func fromJSON(key: String, data: [String: Any]) -> PlayRecord {
        let parts = splitMediaKey(key)
        return PlayRecord(
            id: key,
            source: parts.source,
            title: data["title"] as? String ?? "",
            sourceName: data["source_name"] as? String ?? data["sourceName"] as? String ?? "",
            year: data["year"] as? String ?? "",
            cover: data["cover"] as? String ?? data["poster"] as? String ?? "",
            index: flexibleInt(data["index"]) ?? 1,
            totalEpisodes: flexibleInt(data["total_episodes"] ?? data["totalEpisodes"]) ?? 0,
            playTime: flexibleInt(data["play_time"] ?? data["playTime"]) ?? 0,
            totalTime: flexibleInt(data["total_time"] ?? data["totalTime"]) ?? 0,
            saveTime: flexibleInt64(data["save_time"] ?? data["saveTime"]) ?? Int64(Date().timeIntervalSince1970 * 1_000),
            searchTitle: data["search_title"] as? String ?? data["title"] as? String ?? "",
            remarks: data["remarks"] as? String,
            doubanID: flexibleInt(data["douban_id"]),
            type: data["type"] as? String
        )
    }

    func jsonValue() -> [String: Any] {
        [
            "title": title,
            "source_name": sourceName,
            "year": year,
            "cover": cover,
            "index": index,
            "total_episodes": totalEpisodes,
            "play_time": playTime,
            "total_time": totalTime,
            "save_time": saveTime,
            "search_title": searchTitle,
            "remarks": remarks as Any,
            "douban_id": doubanID as Any,
            "type": type as Any
        ]
    }

    private static func format(_ seconds: Int) -> String {
        let seconds = max(seconds, 0)
        let hours = seconds / 3_600
        let minutes = (seconds % 3_600) / 60
        let remainder = seconds % 60
        return hours > 0
            ? String(format: "%02d:%02d:%02d", hours, minutes, remainder)
            : String(format: "%02d:%02d", minutes, remainder)
    }
}

struct LiveSource: Identifiable, Codable, Hashable {
    var id: String { key }
    let key: String
    let name: String
    let url: String
    let ua: String
    let epg: String
    let from: String
    let disabled: Bool

    init(key: String, name: String, url: String, ua: String = "", epg: String = "", from: String = "", disabled: Bool = false) {
        self.key = key
        self.name = name
        self.url = url
        self.ua = ua
        self.epg = epg
        self.from = from
        self.disabled = disabled
    }
}

struct LiveChannel: Identifiable, Codable, Hashable {
    let id: String
    let tvgID: String
    let name: String
    let logo: String
    let group: String
    let url: String
    let isFavorite: Bool

    enum CodingKeys: String, CodingKey {
        case id, name, logo, group, url
        case tvgID = "tvg_id"
        case isFavorite = "is_favorite"
    }

    init(id: String, tvgID: String, name: String, logo: String, group: String, url: String, isFavorite: Bool = false) {
        self.id = id
        self.tvgID = tvgID
        self.name = name
        self.logo = logo
        self.group = group
        self.url = url
        self.isFavorite = isFavorite
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        name = try container.decode(String.self, forKey: .name)
        url = try container.decode(String.self, forKey: .url)
        id = try container.decodeIfPresent(String.self, forKey: .id) ?? "\(name)-\(url)"
        tvgID = try container.decodeIfPresent(String.self, forKey: .tvgID) ?? ""
        logo = try container.decodeIfPresent(String.self, forKey: .logo) ?? ""
        group = try container.decodeIfPresent(String.self, forKey: .group) ?? "未分组"
        isFavorite = try container.decodeIfPresent(Bool.self, forKey: .isFavorite) ?? false
    }
}

struct EPGProgram: Identifiable, Codable, Hashable {
    var id: String { "\(channelID)-\(startTime.timeIntervalSince1970)" }
    let channelID: String
    let title: String
    let startTime: Date
    let endTime: Date
    let description: String?

    var isLive: Bool { startTime <= .now && .now < endTime }
    var timeRange: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return "\(formatter.string(from: startTime))–\(formatter.string(from: endTime))"
    }

    enum CodingKeys: String, CodingKey {
        case title, description, start, end
        case channelID = "channel_id"
        case startTime = "start_time"
        case endTime = "end_time"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        title = try container.decode(String.self, forKey: .title)
        description = try container.decodeIfPresent(String.self, forKey: .description)
        channelID = try container.decodeIfPresent(String.self, forKey: .channelID) ?? ""
        startTime = Self.date(container, primary: .startTime, fallback: .start)
        endTime = Self.date(container, primary: .endTime, fallback: .end)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(channelID, forKey: .channelID)
        try container.encode(title, forKey: .title)
        try container.encode(startTime, forKey: .startTime)
        try container.encode(endTime, forKey: .endTime)
        try container.encodeIfPresent(description, forKey: .description)
    }

    private static func date(
        _ container: KeyedDecodingContainer<CodingKeys>,
        primary: CodingKeys,
        fallback: CodingKeys
    ) -> Date {
        if let value = try? container.decode(Date.self, forKey: primary) { return value }
        let text = (try? container.decodeIfPresent(String.self, forKey: primary))
            ?? (try? container.decodeIfPresent(String.self, forKey: fallback))
            ?? nil
        guard let text else { return .distantPast }
        if let value = ISO8601DateFormatter().date(from: text) { return value }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        for format in ["yyyyMMddHHmmss Z", "yyyy-MM-dd HH:mm:ss Z", "yyyy-MM-dd'T'HH:mm:ssZ"] {
            formatter.dateFormat = format
            if let value = formatter.date(from: text) { return value }
        }
        return .distantPast
    }
}

struct EPGData: Codable, Hashable {
    let tvgID: String
    let source: String
    let epgURL: String
    let programs: [EPGProgram]

    enum CodingKeys: String, CodingKey {
        case source, programs
        case tvgID = "tvg_id"
        case epgURL = "epg_url"
    }

    enum AlternateCodingKeys: String, CodingKey {
        case tvgID = "tvgId"
        case epgURL = "epgUrl"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let alternate = try decoder.container(keyedBy: AlternateCodingKeys.self)
        tvgID = try container.decodeIfPresent(String.self, forKey: .tvgID)
            ?? alternate.decodeIfPresent(String.self, forKey: .tvgID)
            ?? ""
        source = try container.decodeIfPresent(String.self, forKey: .source) ?? ""
        epgURL = try container.decodeIfPresent(String.self, forKey: .epgURL)
            ?? alternate.decodeIfPresent(String.self, forKey: .epgURL)
            ?? ""
        programs = try container.decodeIfPresent([EPGProgram].self, forKey: .programs) ?? []
    }
}

struct MediaPlatformItem: Codable, Identifiable, Hashable {
    let id: String
    let title: String
    let cover: String
    let author: String
    let description: String
    let duration: String
    let source: String
    let url: String
}

struct AdminConfig: Codable, Hashable {
    var youTubeConfig: YouTubeAdminConfig?
    var bilibiliConfig: BilibiliAdminConfig?
    var shortDramaConfig: ShortDramaAdminConfig?
    var siteConfig: AdminSiteConfig?
    var role: String?

    enum CodingKeys: String, CodingKey {
        case youTubeConfig = "YouTubeConfig"
        case bilibiliConfig = "BilibiliConfig"
        case shortDramaConfig = "ShortDramaConfig"
        case siteConfig = "SiteConfig"
        case role = "Role"
    }
}

struct YouTubeAdminConfig: Codable, Hashable {
    static let defaultRegions = ["US", "CN", "JP", "KR", "GB", "DE", "FR"]
    var enabled = false
    var apiKey = ""
    var enableDemo = true
    var maxResults = 25
    var enabledRegions = Self.defaultRegions
    var enabledCategories = ["Film & Animation", "Music", "Gaming", "News & Politics", "Entertainment"]
}

struct BilibiliAdminConfig: Codable, Hashable {
    var enabled = false
    var loginStatus: String?
}

struct ShortDramaAdminConfig: Codable, Hashable {
    var primaryApiUrl = ""
    var alternativeApiUrl = ""
    var enableAlternative = false
}

struct AdminSiteConfig: Codable, Hashable {
    var siteName = ""
    var announcement = ""

    enum CodingKeys: String, CodingKey {
        case siteName = "SiteName"
        case announcement = "Announcement"
    }
}

struct BangumiItem: Identifiable, Codable, Hashable {
    let id: Int
    let name: String
    let nameCN: String?
    let airDate: String?
    let images: BangumiImages?
    let rating: BangumiRating?

    enum CodingKeys: String, CodingKey {
        case id, name, images, rating
        case nameCN = "name_cn"
        case airDate = "air_date"
    }

    var displayName: String { nameCN?.isEmpty == false ? nameCN! : name }
    var poster: String { images?.large ?? images?.common ?? images?.medium ?? "" }
}

struct BangumiImages: Codable, Hashable {
    let large: String?
    let common: String?
    let medium: String?
}

struct BangumiRating: Codable, Hashable {
    let score: Double?
}

func splitMediaKey(_ key: String) -> (source: String, itemID: String) {
    guard let separator = key.firstIndex(of: "+") else { return ("", key) }
    return (String(key[..<separator]), String(key[key.index(after: separator)...]))
}

func flexibleInt(_ value: Any?) -> Int? {
    if let value = value as? Int { return value }
    if let value = value as? NSNumber { return value.intValue }
    if let value = value as? String { return Int(value) }
    return nil
}

func flexibleInt64(_ value: Any?) -> Int64? {
    if let value = value as? Int64 { return value }
    if let value = value as? NSNumber { return value.int64Value }
    if let value = value as? String { return Int64(value) }
    return nil
}