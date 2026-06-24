import Foundation

struct SearchResult: Codable, Identifiable, Hashable, Equatable {
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
    let typeName: String?
    let doubanID: Int?
    let remarks: String?
    let resolution: String?
    let resolutionLevel: Int?
    let qualityTag: String?
    let dramaName: String?

    enum CodingKeys: String, CodingKey {
        case id, title, poster, episodes, source, year
        case episodeTitles = "episodes_titles"
        case sourceName = "source_name"
        case className = "class"
        case description = "desc"
        case typeName = "type_name"
        case doubanID = "douban_id"
        case remarks, resolution
        case resolutionLevel = "resolution_level"
        case qualityTag = "quality_tag"
        case dramaName = "drama_name"
    }

    init(
        id: String,
        title: String,
        poster: String,
        episodes: [String],
        episodeTitles: [String],
        source: String,
        sourceName: String,
        className: String? = nil,
        year: String,
        description: String? = nil,
        typeName: String? = nil,
        doubanID: Int? = nil,
        remarks: String? = nil,
        resolution: String? = nil,
        resolutionLevel: Int? = nil,
        qualityTag: String? = nil,
        dramaName: String? = nil
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
        self.typeName = typeName
        self.doubanID = doubanID
        self.remarks = remarks
        self.resolution = resolution
        self.resolutionLevel = resolutionLevel
        self.qualityTag = qualityTag
        self.dramaName = dramaName
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        title = try container.decode(String.self, forKey: .title)
        poster = try container.decodeIfPresent(String.self, forKey: .poster) ?? ""
        episodes = try container.decodeIfPresent([String].self, forKey: .episodes) ?? []
        episodeTitles = try container.decodeIfPresent([String].self, forKey: .episodeTitles) ?? []
        source = try container.decode(String.self, forKey: .source)
        sourceName = try container.decodeIfPresent(String.self, forKey: .sourceName) ?? ""
        className = try container.decodeIfPresent(String.self, forKey: .className)
        year = try container.decodeIfPresent(String.self, forKey: .year) ?? ""
        description = try container.decodeIfPresent(String.self, forKey: .description)
        typeName = try container.decodeIfPresent(String.self, forKey: .typeName)
        doubanID = try container.decodeIfPresent(Int.self, forKey: .doubanID)
        remarks = try container.decodeIfPresent(String.self, forKey: .remarks)
        resolution = try container.decodeIfPresent(String.self, forKey: .resolution)
        resolutionLevel = try container.decodeIfPresent(Int.self, forKey: .resolutionLevel)
        qualityTag = try container.decodeIfPresent(String.self, forKey: .qualityTag)
        dramaName = try container.decodeIfPresent(String.self, forKey: .dramaName)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(poster, forKey: .poster)
        try container.encode(episodes, forKey: .episodes)
        try container.encode(episodeTitles, forKey: .episodeTitles)
        try container.encode(source, forKey: .source)
        try container.encode(sourceName, forKey: .sourceName)
        try container.encodeIfPresent(className, forKey: .className)
        try container.encode(year, forKey: .year)
        try container.encodeIfPresent(description, forKey: .description)
        try container.encodeIfPresent(typeName, forKey: .typeName)
        try container.encodeIfPresent(doubanID, forKey: .doubanID)
        try container.encodeIfPresent(remarks, forKey: .remarks)
        try container.encodeIfPresent(resolution, forKey: .resolution)
        try container.encodeIfPresent(resolutionLevel, forKey: .resolutionLevel)
        try container.encodeIfPresent(qualityTag, forKey: .qualityTag)
        try container.encodeIfPresent(dramaName, forKey: .dramaName)
    }

    func episodeTitle(for index: Int) -> String {
        guard index < episodeTitles.count else {
            return "第\(index + 1)集"
        }
        let title = episodeTitles[index]
        return title.isEmpty ? "第\(index + 1)集" : title
    }
}
