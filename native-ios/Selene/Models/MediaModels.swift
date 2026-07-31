import Foundation

enum MediaCategory: String, CaseIterable, Identifiable, Hashable {
    case movie
    case tv
    case anime
    case show

    var id: String { rawValue }

    var title: String {
        switch self {
        case .movie: "电影"
        case .tv: "剧集"
        case .anime: "动漫"
        case .show: "综艺"
        }
    }

    var subtitle: String {
        switch self {
        case .movie: "热门电影"
        case .tv: "热门剧集"
        case .anime: "电视动画"
        case .show: "热门综艺"
        }
    }

    var systemImage: String {
        switch self {
        case .movie: "film.stack"
        case .tv: "tv"
        case .anime: "sparkles.tv"
        case .show: "theatermasks"
        }
    }

    var lunaQuery: [URLQueryItem] {
        switch self {
        case .movie:
            [
                URLQueryItem(name: "kind", value: "movie"),
                URLQueryItem(name: "category", value: "热门"),
                URLQueryItem(name: "type", value: "全部")
            ]
        case .tv:
            [
                URLQueryItem(name: "kind", value: "tv"),
                URLQueryItem(name: "category", value: "tv"),
                URLQueryItem(name: "type", value: "tv")
            ]
        case .show:
            [
                URLQueryItem(name: "kind", value: "tv"),
                URLQueryItem(name: "category", value: "show"),
                URLQueryItem(name: "type", value: "show")
            ]
        case .anime:
            [
                URLQueryItem(name: "kind", value: "tv"),
                URLQueryItem(name: "category", value: "tv"),
                URLQueryItem(name: "type", value: "tv_animation")
            ]
        }
    }
}

struct DoubanMovie: Decodable, Identifiable, Hashable {
    let id: String
    let title: String
    let poster: String
    let rate: String?
    let year: String

    enum CodingKeys: String, CodingKey {
        case id, title, poster, rate, year, pic, rating
        case cardSubtitle = "card_subtitle"
    }

    init(id: String, title: String, poster: String, rate: String?, year: String) {
        self.id = id
        self.title = title
        self.poster = poster
        self.rate = rate
        self.year = year
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = container.lossyString(forKey: .id)
        title = container.lossyString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster)
            ?? (try? container.decode(DoubanPicture.self, forKey: .pic).bestURL)
            ?? ""
        rate = try container.decodeIfPresent(String.self, forKey: .rate)
            ?? (try? container.decode(DoubanRating.self, forKey: .rating).displayValue)
        let explicitYear = container.lossyOptionalString(forKey: .year)
        let subtitle = try container.decodeIfPresent(String.self, forKey: .cardSubtitle) ?? ""
        year = explicitYear ?? subtitle.firstMatch(of: #/\d{4}/#).map { String($0.output) } ?? ""
    }
}

struct DoubanRecommendItem: Decodable, Identifiable, Hashable {
    let id: String
    let title: String
    let poster: String
    let rate: String?

    enum CodingKeys: String, CodingKey {
        case id, title, poster, rate, pic, rating
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = container.lossyString(forKey: .id)
        title = container.lossyString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster)
            ?? (try? container.decode(DoubanPicture.self, forKey: .pic).bestURL)
            ?? ""
        rate = try container.decodeIfPresent(String.self, forKey: .rate)
            ?? (try? container.decode(DoubanRating.self, forKey: .rating).displayValue)
    }

    init(id: String, title: String, poster: String, rate: String?) {
        self.id = id
        self.title = title
        self.poster = poster
        self.rate = rate
    }
}

struct DoubanMovieDetails: Decodable, Hashable {
    let id: String
    let title: String
    let poster: String
    let rate: String?
    let year: String
    let summary: String?
    let genres: [String]
    let directors: [String]
    let screenwriters: [String]
    let actors: [String]
    let duration: String?
    let countries: [String]
    let languages: [String]
    let releaseDate: String?
    let originalTitle: String?
    let imdbID: String?
    let totalEpisodes: Int?
    let recommends: [DoubanRecommendItem]

    enum CodingKeys: String, CodingKey {
        case id, title, poster, rate, year, summary, genres, directors, screenwriters, actors
        case duration, countries, languages, recommends, pic, rating, pubdate
        case releaseDate = "release_date"
        case originalTitle = "original_title"
        case imdbID = "imdb_id"
        case totalEpisodes = "total_episodes"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = container.lossyString(forKey: .id)
        title = container.lossyString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster)
            ?? (try? container.decode(DoubanPicture.self, forKey: .pic).bestURL)
            ?? ""
        rate = try container.decodeIfPresent(String.self, forKey: .rate)
            ?? (try? container.decode(DoubanRating.self, forKey: .rating).displayValue)
        let pubdates = (try? container.decode([String].self, forKey: .pubdate)) ?? []
        year = container.lossyOptionalString(forKey: .year)
            ?? Self.extractYear(pubdates.first ?? "")
        summary = try container.decodeIfPresent(String.self, forKey: .summary)
        genres = (try? container.decode([String].self, forKey: .genres)) ?? []
        directors = Self.people(container, key: .directors)
        screenwriters = Self.people(container, key: .screenwriters)
        actors = Self.people(container, key: .actors)
        duration = try container.decodeIfPresent(String.self, forKey: .duration)
        countries = (try? container.decode([String].self, forKey: .countries)) ?? []
        languages = (try? container.decode([String].self, forKey: .languages)) ?? []
        releaseDate = try container.decodeIfPresent(String.self, forKey: .releaseDate) ?? pubdates.first
        originalTitle = try container.decodeIfPresent(String.self, forKey: .originalTitle)
        imdbID = try container.decodeIfPresent(String.self, forKey: .imdbID)
        totalEpisodes = try container.decodeIfPresent(Int.self, forKey: .totalEpisodes)
        recommends = (try? container.decode([DoubanRecommendItem].self, forKey: .recommends)) ?? []
    }

    private static func people(
        _ container: KeyedDecodingContainer<CodingKeys>,
        key: CodingKeys
    ) -> [String] {
        if let names = try? container.decode([String].self, forKey: key) {
            return names
        }
        if let people = try? container.decode([DoubanPerson].self, forKey: key) {
            return people.map(\.name)
        }
        return []
    }

    private static func extractYear(_ text: String) -> String {
        text.firstMatch(of: #/\d{4}/#).map { String($0.output) } ?? ""
    }
}

private struct DoubanPicture: Codable {
    let normal: String?
    let large: String?
    let poster: String?

    var bestURL: String {
        [normal, large, poster].compactMap { $0 }.first(where: { !$0.isEmpty }) ?? ""
    }
}

private struct DoubanPerson: Codable {
    let name: String
}

private struct DoubanRating: Codable {
    let value: FlexibleString?
    let average: FlexibleString?

    var displayValue: String? {
        value?.value ?? average?.value
    }
}

struct SearchResult: Codable, Identifiable, Hashable, Sendable {
    let id: String
    let title: String
    let poster: String
    let episodes: [String]
    let episodeTitles: [String]
    let source: String
    let sourceName: String
    let className: String?
    let year: String
    let description: String?
    let remarks: String?
    let resolution: String?
    let doubanID: Int?
    let typeName: String?
    let qualityTag: String?
    let dramaName: String?
    /// Set when the row came from a local Base58 subscription resource rather
    /// than a LunaTV server, so detail loading can stay off `/api/detail`.
    let isLocal: Bool
    /// The originating MacCMS `api` endpoint for a local row. Carried on the
    /// result so detail loading does not need the subscription list again.
    let localAPI: String?

    enum CodingKeys: String, CodingKey {
        case id, title, poster, episodes, source, year, remarks, resolution
        case episodeTitles = "episodes_titles"
        case sourceName = "source_name"
        case className = "class"
        case description = "desc"
        case doubanID = "douban_id"
        case typeName = "type_name"
        case qualityTag = "quality_tag"
        case dramaName = "drama_name"
        case isLocal = "is_local"
        case localAPI = "local_api"
    }

    init(
        id: String,
        title: String,
        poster: String = "",
        episodes: [String] = [],
        episodeTitles: [String] = [],
        source: String,
        sourceName: String = "",
        className: String? = nil,
        year: String = "",
        description: String? = nil,
        remarks: String? = nil,
        resolution: String? = nil,
        doubanID: Int? = nil,
        typeName: String? = nil,
        qualityTag: String? = nil,
        dramaName: String? = nil,
        isLocal: Bool = false,
        localAPI: String? = nil
    ) {
        self.id = id
        self.title = title
        self.poster = poster
        self.episodes = episodes
        self.episodeTitles = episodeTitles
        self.source = source
        self.sourceName = sourceName
        self.className = className
        self.year = year
        self.description = description
        self.remarks = remarks
        self.resolution = resolution
        self.doubanID = doubanID
        self.typeName = typeName
        self.qualityTag = qualityTag
        self.dramaName = dramaName
        self.isLocal = isLocal
        self.localAPI = localAPI
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = container.lossyString(forKey: .id)
        title = container.lossyString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster) ?? ""
        episodes = try container.decodeIfPresent([String].self, forKey: .episodes) ?? []
        episodeTitles = try container.decodeIfPresent([String].self, forKey: .episodeTitles) ?? []
        source = container.lossyString(forKey: .source)
        sourceName = try container.decodeIfPresent(String.self, forKey: .sourceName) ?? source
        className = try container.decodeIfPresent(String.self, forKey: .className)
        year = container.lossyOptionalString(forKey: .year) ?? ""
        description = try container.decodeIfPresent(String.self, forKey: .description)
        remarks = try container.decodeIfPresent(String.self, forKey: .remarks)
        resolution = try container.decodeIfPresent(String.self, forKey: .resolution)
        doubanID = try container.decodeIfPresent(Int.self, forKey: .doubanID)
        typeName = try container.decodeIfPresent(String.self, forKey: .typeName)
        qualityTag = try container.decodeIfPresent(String.self, forKey: .qualityTag)
        dramaName = try container.decodeIfPresent(String.self, forKey: .dramaName)
        isLocal = try container.decodeIfPresent(Bool.self, forKey: .isLocal) ?? false
        localAPI = try container.decodeIfPresent(String.self, forKey: .localAPI)
    }

    var metadata: String {
        [year, sourceName, resolution, remarks]
            .compactMap { $0 }
            .filter { !$0.isEmpty }
            .joined(separator: " · ")
    }

    /// `id` is only unique within one source, so aggregated multi-source results
    /// collide in SwiftUI collections. Key rows on this instead.
    var uniqueKey: String { "\(source)|\(id)" }

    func episodeTitle(at index: Int) -> String {
        guard episodeTitles.indices.contains(index), !episodeTitles[index].isEmpty else {
            return "第 \(index + 1) 集"
        }
        return episodeTitles[index]
    }
}

private struct FlexibleString: Codable {
    let value: String

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let string = try? container.decode(String.self) {
            value = string
        } else if let number = try? container.decode(Double.self) {
            value = number.formatted(.number.precision(.fractionLength(0...1)))
        } else {
            throw DecodingError.typeMismatch(
                String.self,
                .init(codingPath: decoder.codingPath, debugDescription: "Expected a string or number")
            )
        }
    }
}

private extension KeyedDecodingContainer {
    func lossyString(forKey key: Key) -> String {
        lossyOptionalString(forKey: key) ?? ""
    }

    func lossyOptionalString(forKey key: Key) -> String? {
        if let value = try? decode(String.self, forKey: key) {
            return value
        }
        if let value = try? decode(Int.self, forKey: key) {
            return String(value)
        }
        if let value = try? decode(Double.self, forKey: key) {
            return String(value)
        }
        return nil
    }
}