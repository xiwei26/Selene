import Foundation

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
    static let defaultCategories = ["Film & Animation", "Music", "Gaming", "News & Politics", "Entertainment"]

    var enabled: Bool = false
    var apiKey: String = ""
    var enableDemo: Bool = true
    var maxResults: Int = 25
    var enabledRegions: [String] = Self.defaultRegions
    var enabledCategories: [String] = Self.defaultCategories
}

struct BilibiliAdminConfig: Codable, Hashable {
    var enabled: Bool = false
    var loginStatus: String?
    var userInfo: BilibiliAdminUserInfo?
}

struct BilibiliAdminUserInfo: Codable, Hashable {
    var mid: Int64 = 0
    var username: String = ""
    var face: String = ""
    var isVip: Bool = false
}

struct ShortDramaAdminConfig: Codable, Hashable {
    var primaryApiUrl: String = ""
    var alternativeApiUrl: String = ""
    var enableAlternative: Bool = false
}

struct AdminSiteConfig: Codable, Hashable {
    var siteName: String = ""
    var announcement: String = ""

    enum CodingKeys: String, CodingKey {
        case siteName = "SiteName"
        case announcement = "Announcement"
    }
}
