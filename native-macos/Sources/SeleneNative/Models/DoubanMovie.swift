import Foundation

struct DoubanRecommendItem: Identifiable, Codable, Hashable {
    var id: String
    var title: String
    var poster: String
    var rate: String?
}

struct DoubanMovieDetails: Codable, Hashable {
    var id: String
    var title: String
    var poster: String
    var rate: String?
    var year: String
    var summary: String?
    var genres: [String]
    var directors: [String]
    var screenwriters: [String]
    var actors: [String]
    var duration: String?
    var countries: [String]
    var languages: [String]
    var releaseDate: String?
    var originalTitle: String?
    var imdbId: String?
    var totalEpisodes: Int?
    var recommends: [DoubanRecommendItem]

    enum CodingKeys: String, CodingKey {
        case id, title, poster, rate, summary, genres, directors, screenwriters, actors, duration, countries, languages, recommends
        case pic, rating, pubdate
        case releaseDate = "release_date"
        case originalTitle = "original_title"
        case imdbId = "imdb_id"
        case totalEpisodes = "total_episodes"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decodeString(forKey: .id)
        title = try container.decodeString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster)
            ?? (try? container.decode(DoubanPic.self, forKey: .pic).bestPoster)
            ?? ""
        rate = try container.decodeIfPresent(String.self, forKey: .rate)
            ?? (try? container.decode(DoubanRating.self, forKey: .rating).displayValue)
        let pubdates = (try? container.decode([String].self, forKey: .pubdate)) ?? []
        year = Self.extractYear(from: pubdates.first ?? "")
        summary = try container.decodeIfPresent(String.self, forKey: .summary)
        genres = (try? container.decode([String].self, forKey: .genres)) ?? []
        directors = Self.decodePeople(from: container, key: .directors)
        screenwriters = Self.decodePeople(from: container, key: .screenwriters)
        actors = Self.decodePeople(from: container, key: .actors)
        duration = try container.decodeIfPresent(String.self, forKey: .duration)
        countries = (try? container.decode([String].self, forKey: .countries)) ?? []
        languages = (try? container.decode([String].self, forKey: .languages)) ?? []
        releaseDate = try container.decodeIfPresent(String.self, forKey: .releaseDate) ?? pubdates.first
        originalTitle = try container.decodeIfPresent(String.self, forKey: .originalTitle)
        imdbId = try container.decodeIfPresent(String.self, forKey: .imdbId)
        totalEpisodes = try container.decodeIfPresent(Int.self, forKey: .totalEpisodes)
        recommends = (try? container.decode([DoubanRecommendItem].self, forKey: .recommends)) ?? []
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(poster, forKey: .poster)
        try container.encodeIfPresent(rate, forKey: .rate)
        try container.encode(summary, forKey: .summary)
        try container.encode(genres, forKey: .genres)
        try container.encode(directors, forKey: .directors)
        try container.encode(screenwriters, forKey: .screenwriters)
        try container.encode(actors, forKey: .actors)
        try container.encodeIfPresent(duration, forKey: .duration)
        try container.encode(countries, forKey: .countries)
        try container.encode(languages, forKey: .languages)
        try container.encodeIfPresent(releaseDate, forKey: .releaseDate)
        try container.encodeIfPresent(originalTitle, forKey: .originalTitle)
        try container.encodeIfPresent(imdbId, forKey: .imdbId)
        try container.encodeIfPresent(totalEpisodes, forKey: .totalEpisodes)
        try container.encode(recommends, forKey: .recommends)
    }

    private static func decodePeople(from container: KeyedDecodingContainer<CodingKeys>, key: CodingKeys) -> [String] {
        if let names = try? container.decode([String].self, forKey: key) {
            return names
        }
        if let people = try? container.decode([DoubanPerson].self, forKey: key) {
            return people.map(\.name)
        }
        return []
    }

    static func extractYear(from text: String) -> String {
        guard let range = text.range(of: #"\d{4}"#, options: .regularExpression) else { return "" }
        return String(text[range])
    }
}

struct DoubanMovie: Identifiable, Codable, Hashable {
    var id: String
    var title: String
    var poster: String
    var rate: String?
    var year: String

    enum CodingKeys: String, CodingKey {
        case id, title, poster, rate, pic, rating
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
        id = try container.decodeString(forKey: .id)
        title = try container.decodeString(forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster)
            ?? (try? container.decode(DoubanPic.self, forKey: .pic).bestPoster)
            ?? ""
        rate = try container.decodeIfPresent(String.self, forKey: .rate)
            ?? (try? container.decode(DoubanRating.self, forKey: .rating).displayValue)
        year = DoubanMovieDetails.extractYear(from: (try? container.decode(String.self, forKey: .cardSubtitle)) ?? "")
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(poster, forKey: .poster)
        try container.encodeIfPresent(rate, forKey: .rate)
        try container.encode(year, forKey: .cardSubtitle)
    }
}

struct DoubanResponse: Codable {
    var items: [DoubanMovie]
}

private struct DoubanPic: Codable {
    var normal: String?
    var large: String?
    var poster: String?

    var bestPoster: String {
        [normal, large, poster].compactMap { $0 }.first { !$0.isEmpty } ?? ""
    }
}

private struct DoubanRating: Codable {
    var value: String?
    var average: String?

    var displayValue: String? {
        value ?? average
    }
}

private struct DoubanPerson: Codable {
    var name: String
}

private extension KeyedDecodingContainer {
    func decodeString(forKey key: Key) throws -> String {
        if let string = try? decode(String.self, forKey: key) {
            return string
        }
        if let int = try? decode(Int.self, forKey: key) {
            return String(int)
        }
        throw DecodingError.valueNotFound(
            String.self,
            DecodingError.Context(codingPath: codingPath + [key], debugDescription: "Expected string-compatible value")
        )
    }
}
