import Foundation

struct ShortDramaCategory: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var name: String
    var type: String?

    enum CodingKeys: String, CodingKey {
        case id, name, type
        case typeID = "type_id"
        case typeName = "type_name"
    }

    init(id: String, name: String, type: String? = nil) {
        self.id = id
        self.name = name
        self.type = type
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = (try? container.decodeFlexibleString(forKey: .id))
            ?? (try? container.decodeFlexibleString(forKey: .typeID))
            ?? ""
        name = try container.decodeIfPresent(String.self, forKey: .name)
            ?? container.decodeIfPresent(String.self, forKey: .typeName)
            ?? ""
        type = try container.decodeIfPresent(String.self, forKey: .type)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(name, forKey: .name)
        try container.encodeIfPresent(type, forKey: .type)
    }
}

struct ShortDramaItem: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var name: String
    var cover: String
    var desc: String?
    var year: String?
    var category: String?
    var episodeCount: Int?

    enum CodingKeys: String, CodingKey {
        case id, name, title, cover, poster, desc, year, category
        case episodeCount = "episode_count"
        case totalEpisodes = "total_episodes"
    }

    init(id: String, name: String, cover: String = "", desc: String? = nil, year: String? = nil, category: String? = nil, episodeCount: Int? = nil) {
        self.id = id
        self.name = name
        self.cover = cover
        self.desc = desc
        self.year = year
        self.category = category
        self.episodeCount = episodeCount
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decodeFlexibleString(forKey: .id)
        name = try container.decodeIfPresent(String.self, forKey: .name)
            ?? container.decodeIfPresent(String.self, forKey: .title)
            ?? ""
        cover = try container.decodeIfPresent(String.self, forKey: .cover)
            ?? container.decodeIfPresent(String.self, forKey: .poster)
            ?? ""
        desc = try container.decodeIfPresent(String.self, forKey: .desc)
        year = try container.decodeIfPresent(String.self, forKey: .year)
        category = try container.decodeIfPresent(String.self, forKey: .category)
        episodeCount = container.decodeFlexibleIntIfPresent(forKey: .episodeCount)
            ?? container.decodeFlexibleIntIfPresent(forKey: .totalEpisodes)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(name, forKey: .name)
        try container.encode(cover, forKey: .cover)
        try container.encodeIfPresent(desc, forKey: .desc)
        try container.encodeIfPresent(year, forKey: .year)
        try container.encodeIfPresent(category, forKey: .category)
        try container.encodeIfPresent(episodeCount, forKey: .episodeCount)
    }
}

struct ShortDramaListResult: Codable, Hashable, Sendable {
    var items: [ShortDramaItem]
    var total: Int
    var page: Int?
    var pageSize: Int?

    enum CodingKeys: String, CodingKey {
        case items, list, data, total, page
        case pageSize = "page_size"
    }

    init(items: [ShortDramaItem], total: Int = 0, page: Int? = nil, pageSize: Int? = nil) {
        self.items = items
        self.total = total
        self.page = page
        self.pageSize = pageSize
    }

    init(from decoder: Decoder) throws {
        if let rawItems = try? [ShortDramaItem](from: decoder) {
            items = rawItems
            total = rawItems.count
            page = nil
            pageSize = nil
            return
        }
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let data = try? container.decode(ShortDramaListResult.self, forKey: .data) {
            self = data
            return
        }
        items = (try? container.decode([ShortDramaItem].self, forKey: .items))
            ?? (try? container.decode([ShortDramaItem].self, forKey: .list))
            ?? []
        total = (try? container.decodeFlexibleInt(forKey: .total)) ?? items.count
        page = try? container.decodeFlexibleInt(forKey: .page)
        pageSize = try? container.decodeFlexibleInt(forKey: .pageSize)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(items, forKey: .items)
        try container.encode(total, forKey: .total)
        try container.encodeIfPresent(page, forKey: .page)
        try container.encodeIfPresent(pageSize, forKey: .pageSize)
    }
}

struct ShortDramaDetail: Codable, Hashable, Sendable {
    var id: String
    var name: String
    var cover: String
    var desc: String?
    var episodes: [ShortDramaEpisode]

    init(id: String, name: String, cover: String = "", desc: String? = nil, episodes: [ShortDramaEpisode] = []) {
        self.id = id
        self.name = name
        self.cover = cover
        self.desc = desc
        self.episodes = episodes
    }
}

struct ShortDramaEpisode: Identifiable, Codable, Hashable, Sendable {
    var id: String { "\(episode)-\(title ?? "")" }
    var episode: Int
    var title: String?
    var url: String?
}

struct ShortDramaParseResult: Codable, Hashable, Sendable {
    var parsedUrl: String?
    var proxyUrl: String?
    var url: String?

    enum CodingKeys: String, CodingKey {
        case url
        case parsedUrl = "parsedUrl"
        case parsedURL = "parsed_url"
        case proxyUrl = "proxyUrl"
        case proxyURL = "proxy_url"
    }

    init(parsedUrl: String? = nil, proxyUrl: String? = nil, url: String? = nil) {
        self.parsedUrl = parsedUrl
        self.proxyUrl = proxyUrl
        self.url = url
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        parsedUrl = try container.decodeIfPresent(String.self, forKey: .parsedUrl)
            ?? container.decodeIfPresent(String.self, forKey: .parsedURL)
        proxyUrl = try container.decodeIfPresent(String.self, forKey: .proxyUrl)
            ?? container.decodeIfPresent(String.self, forKey: .proxyURL)
        url = try container.decodeIfPresent(String.self, forKey: .url)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(parsedUrl, forKey: .parsedUrl)
        try container.encodeIfPresent(proxyUrl, forKey: .proxyUrl)
        try container.encodeIfPresent(url, forKey: .url)
    }
}

struct VideoPlatformItem: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var title: String
    var cover: String
    var author: String?
    var desc: String?
    var duration: String?
    var views: String?
    var publishedAt: String?
    var playableUrl: String?
    var proxyUrl: String?
    var url: String?

    enum CodingKeys: String, CodingKey {
        case id, bvid, aid, title, cover, thumbnail, pic, image, author, duration, views, play, url, description, desc, snippet
        case publishedAt = "publishedAt"
        case publishedAtSnake = "published_at"
        case pubdate
        case playableUrl = "playableUrl"
        case playableURL = "playable_url"
        case proxyUrl = "proxyUrl"
        case proxyURL = "proxy_url"
    }

    private enum YouTubeIDKeys: String, CodingKey {
        case videoId, channelId, playlistId, kind
    }

    private enum YouTubeSnippetKeys: String, CodingKey {
        case title, description, channelTitle, publishedAt, thumbnails
    }

    private enum YouTubeThumbnailSizeKeys: String, CodingKey {
        case maxres, standard, high, medium
        case defaultSize = "default"
    }

    private enum YouTubeThumbnailKeys: String, CodingKey {
        case url
    }

    init(id: String, title: String, cover: String = "", author: String? = nil, desc: String? = nil, duration: String? = nil, views: String? = nil, publishedAt: String? = nil, playableUrl: String? = nil, proxyUrl: String? = nil, url: String? = nil) {
        self.id = id
        self.title = title
        self.cover = cover
        self.author = author
        self.desc = desc
        self.duration = duration
        self.views = views
        self.publishedAt = publishedAt
        self.playableUrl = playableUrl
        self.proxyUrl = proxyUrl
        self.url = url
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let snippet = try? container.nestedContainer(keyedBy: YouTubeSnippetKeys.self, forKey: .snippet)

        id = (try? container.decodeFlexibleString(forKey: .bvid))
            ?? Self.decodeYouTubeID(from: container)
            ?? (try? container.decodeFlexibleString(forKey: .id))
            ?? (try? container.decodeFlexibleString(forKey: .aid))
            ?? ""
        title = try container.decodeIfPresent(String.self, forKey: .title)
            ?? snippet?.decodeIfPresent(String.self, forKey: .title)
            ?? ""
        cover = try container.decodeIfPresent(String.self, forKey: .cover)
            ?? container.decodeIfPresent(String.self, forKey: .thumbnail)
            ?? container.decodeIfPresent(String.self, forKey: .pic)
            ?? container.decodeIfPresent(String.self, forKey: .image)
            ?? Self.decodeYouTubeThumbnail(from: snippet)
            ?? ""
        author = try container.decodeIfPresent(String.self, forKey: .author)
            ?? snippet?.decodeIfPresent(String.self, forKey: .channelTitle)
        desc = try container.decodeIfPresent(String.self, forKey: .desc)
            ?? container.decodeIfPresent(String.self, forKey: .description)
            ?? snippet?.decodeIfPresent(String.self, forKey: .description)
        duration = try container.decodeIfPresent(String.self, forKey: .duration)
        views = (try? container.decodeFlexibleString(forKey: .views))
            ?? (try? container.decodeFlexibleString(forKey: .play))
        publishedAt = try container.decodeIfPresent(String.self, forKey: .publishedAt)
            ?? container.decodeIfPresent(String.self, forKey: .publishedAtSnake)
            ?? (try? container.decodeFlexibleString(forKey: .pubdate))
            ?? snippet?.decodeIfPresent(String.self, forKey: .publishedAt)
        playableUrl = try container.decodeIfPresent(String.self, forKey: .playableUrl)
            ?? container.decodeIfPresent(String.self, forKey: .playableURL)
        proxyUrl = try container.decodeIfPresent(String.self, forKey: .proxyUrl)
            ?? container.decodeIfPresent(String.self, forKey: .proxyURL)
        url = try container.decodeIfPresent(String.self, forKey: .url)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(cover, forKey: .cover)
        try container.encodeIfPresent(author, forKey: .author)
        try container.encodeIfPresent(desc, forKey: .desc)
        try container.encodeIfPresent(duration, forKey: .duration)
        try container.encodeIfPresent(views, forKey: .views)
        try container.encodeIfPresent(publishedAt, forKey: .publishedAt)
        try container.encodeIfPresent(playableUrl, forKey: .playableUrl)
        try container.encodeIfPresent(proxyUrl, forKey: .proxyUrl)
        try container.encodeIfPresent(url, forKey: .url)
    }

    private static func decodeYouTubeID(from container: KeyedDecodingContainer<CodingKeys>) -> String? {
        guard let idContainer = try? container.nestedContainer(keyedBy: YouTubeIDKeys.self, forKey: .id) else {
            return nil
        }
        return (try? idContainer.decodeIfPresent(String.self, forKey: .videoId))
            ?? (try? idContainer.decodeIfPresent(String.self, forKey: .channelId))
            ?? (try? idContainer.decodeIfPresent(String.self, forKey: .playlistId))
            ?? (try? idContainer.decodeIfPresent(String.self, forKey: .kind))
    }

    private static func decodeYouTubeThumbnail(from snippet: KeyedDecodingContainer<YouTubeSnippetKeys>?) -> String? {
        guard let snippet,
              let thumbnails = try? snippet.nestedContainer(keyedBy: YouTubeThumbnailSizeKeys.self, forKey: .thumbnails) else {
            return nil
        }

        let sizes: [YouTubeThumbnailSizeKeys] = [.maxres, .standard, .high, .medium, .defaultSize]
        for size in sizes {
            if let thumbnail = try? thumbnails.nestedContainer(keyedBy: YouTubeThumbnailKeys.self, forKey: size),
               let url = try? thumbnail.decodeIfPresent(String.self, forKey: .url),
               !url.isEmpty {
                return url
            }
        }
        return nil
    }
}

struct VideoPlatformPage: Codable, Hashable, Sendable {
    var items: [VideoPlatformItem]
    var nextPageToken: String?
    var total: Int?

    enum CodingKeys: String, CodingKey {
        case items, list, videos, data, total
        case nextPageToken = "nextPageToken"
        case nextPageTokenSnake = "next_page_token"
    }

    init(items: [VideoPlatformItem], nextPageToken: String? = nil, total: Int? = nil) {
        self.items = items
        self.nextPageToken = nextPageToken
        self.total = total
    }

    init(from decoder: Decoder) throws {
        if let rawItems = try? [VideoPlatformItem](from: decoder) {
            items = rawItems
            nextPageToken = nil
            total = rawItems.count
            return
        }
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let data = try? container.decode(VideoPlatformPage.self, forKey: .data) {
            self = data
            return
        }
        items = (try? container.decode([VideoPlatformItem].self, forKey: .items))
            ?? (try? container.decode([VideoPlatformItem].self, forKey: .list))
            ?? (try? container.decode([VideoPlatformItem].self, forKey: .videos))
            ?? []
        nextPageToken = try container.decodeIfPresent(String.self, forKey: .nextPageToken)
            ?? container.decodeIfPresent(String.self, forKey: .nextPageTokenSnake)
        total = try? container.decodeFlexibleInt(forKey: .total)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(items, forKey: .items)
        try container.encodeIfPresent(nextPageToken, forKey: .nextPageToken)
        try container.encodeIfPresent(total, forKey: .total)
    }
}

struct YouTubeRegion: Identifiable, Codable, Hashable, Sendable {
    var code: String
    var name: String
    var id: String { code }
}

struct TmdbBackdropResult: Codable, Hashable, Sendable {
    var backdropUrl: String?
    var logoUrl: String?
    var posterUrl: String?

    enum CodingKeys: String, CodingKey {
        case backdrop, logo, poster
        case backdropUrl = "backdropUrl"
        case backdropURL = "backdrop_url"
        case logoUrl = "logoUrl"
        case logoURL = "logo_url"
        case posterUrl = "posterUrl"
        case posterURL = "poster_url"
    }

    init(backdropUrl: String? = nil, logoUrl: String? = nil, posterUrl: String? = nil) {
        self.backdropUrl = backdropUrl
        self.logoUrl = logoUrl
        self.posterUrl = posterUrl
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        backdropUrl = try container.decodeIfPresent(String.self, forKey: .backdropUrl)
            ?? container.decodeIfPresent(String.self, forKey: .backdropURL)
            ?? container.decodeIfPresent(String.self, forKey: .backdrop)
        logoUrl = try container.decodeIfPresent(String.self, forKey: .logoUrl)
            ?? container.decodeIfPresent(String.self, forKey: .logoURL)
            ?? container.decodeIfPresent(String.self, forKey: .logo)
        posterUrl = try container.decodeIfPresent(String.self, forKey: .posterUrl)
            ?? container.decodeIfPresent(String.self, forKey: .posterURL)
            ?? container.decodeIfPresent(String.self, forKey: .poster)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(backdropUrl, forKey: .backdropUrl)
        try container.encodeIfPresent(logoUrl, forKey: .logoUrl)
        try container.encodeIfPresent(posterUrl, forKey: .posterUrl)
    }
}

struct TmdbActorResult: Codable, Hashable, Sendable {
    var name: String?
    var profileUrl: String?
    var works: [DoubanMovie]?
}

struct DoubanComment: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var username: String
    var content: String
    var rating: String?
    var createdAt: String?

    enum CodingKeys: String, CodingKey {
        case id, username, user, content, rating
        case createdAt = "createdAt"
        case createdAtSnake = "created_at"
    }

    init(id: String = UUID().uuidString, username: String, content: String, rating: String? = nil, createdAt: String? = nil) {
        self.id = id
        self.username = username
        self.content = content
        self.rating = rating
        self.createdAt = createdAt
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = (try? container.decodeFlexibleString(forKey: .id)) ?? UUID().uuidString
        username = try container.decodeIfPresent(String.self, forKey: .username)
            ?? container.decodeIfPresent(String.self, forKey: .user)
            ?? ""
        content = try container.decodeIfPresent(String.self, forKey: .content) ?? ""
        rating = try container.decodeIfPresent(String.self, forKey: .rating)
        createdAt = try container.decodeIfPresent(String.self, forKey: .createdAt)
            ?? container.decodeIfPresent(String.self, forKey: .createdAtSnake)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(username, forKey: .username)
        try container.encode(content, forKey: .content)
        try container.encodeIfPresent(rating, forKey: .rating)
        try container.encodeIfPresent(createdAt, forKey: .createdAt)
    }
}

struct DoubanQuickInfo: Codable, Hashable, Sendable {
    var id: String?
    var title: String?
    var summary: String?
    var rating: String?
    var year: String?

    enum CodingKeys: String, CodingKey {
        case id, title, summary, rating, year, rate
        case plotSummary = "plot_summary"
    }

    init(id: String? = nil, title: String? = nil, summary: String? = nil, rating: String? = nil, year: String? = nil) {
        self.id = id
        self.title = title
        self.summary = summary
        self.rating = rating
        self.year = year
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try? container.decodeFlexibleString(forKey: .id)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        summary = try container.decodeIfPresent(String.self, forKey: .summary)
            ?? container.decodeIfPresent(String.self, forKey: .plotSummary)
        rating = try container.decodeIfPresent(String.self, forKey: .rating)
            ?? container.decodeIfPresent(String.self, forKey: .rate)
        year = try container.decodeIfPresent(String.self, forKey: .year)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(id, forKey: .id)
        try container.encodeIfPresent(title, forKey: .title)
        try container.encodeIfPresent(summary, forKey: .summary)
        try container.encodeIfPresent(rating, forKey: .rating)
        try container.encodeIfPresent(year, forKey: .year)
    }
}

struct DoubanSuggestItem: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var title: String
    var type: String?
    var year: String?
}

struct DoubanCelebrityWork: Identifiable, Codable, Hashable, Sendable {
    var id: String
    var title: String
    var poster: String?
    var year: String?
}

struct TrailerRefreshResult: Codable, Hashable, Sendable {
    var trailerUrl: String?
    var message: String?

    enum CodingKeys: String, CodingKey {
        case message
        case trailerUrl = "trailerUrl"
        case trailerURL = "trailer_url"
    }

    init(trailerUrl: String? = nil, message: String? = nil) {
        self.trailerUrl = trailerUrl
        self.message = message
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        trailerUrl = try container.decodeIfPresent(String.self, forKey: .trailerUrl)
            ?? container.decodeIfPresent(String.self, forKey: .trailerURL)
        message = try container.decodeIfPresent(String.self, forKey: .message)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(trailerUrl, forKey: .trailerUrl)
        try container.encodeIfPresent(message, forKey: .message)
    }
}

private extension KeyedDecodingContainer {
    func decodeFlexibleString(forKey key: Key) throws -> String {
        if let value = try? decode(String.self, forKey: key) { return value }
        if let value = try? decode(Int.self, forKey: key) { return String(value) }
        throw DecodingError.valueNotFound(
            String.self,
            DecodingError.Context(codingPath: codingPath + [key], debugDescription: "Expected string-compatible value")
        )
    }

    func decodeFlexibleInt(forKey key: Key) throws -> Int {
        if let value = try? decode(Int.self, forKey: key) { return value }
        if let value = try? decode(String.self, forKey: key), let intValue = Int(value) { return intValue }
        throw DecodingError.valueNotFound(
            Int.self,
            DecodingError.Context(codingPath: codingPath + [key], debugDescription: "Expected int-compatible value")
        )
    }

    func decodeFlexibleIntIfPresent(forKey key: Key) -> Int? {
        try? decodeFlexibleInt(forKey: key)
    }
}
