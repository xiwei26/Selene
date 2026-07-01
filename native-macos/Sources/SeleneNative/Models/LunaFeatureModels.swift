import Foundation

struct MediaPlatformItem: Codable, Identifiable, Hashable {
    let id: String
    let title: String
    let cover: String
    let author: String
    let description: String
    let duration: String
    let source: String
    let url: String

    init(
        id: String = "",
        title: String = "",
        cover: String = "",
        author: String = "",
        description: String = "",
        duration: String = "",
        source: String = "",
        url: String = ""
    ) {
        self.id = id
        self.title = title
        self.cover = cover
        self.author = author
        self.description = description
        self.duration = duration
        self.source = source
        self.url = url
    }
}

struct TMDBBackdrop: Codable, Hashable {
    let backdrop: String?
    let poster: String?
    let logo: String?
    let title: String?
    let overview: String?
    let rating: Double?
    let year: String?
    let numberOfSeasons: Int?
}

struct DoubanQuickInfo: Hashable {
    let title: String
    let year: String?
    let rating: String?
    let summary: String?
    let genres: [String]
    let directors: [String]
    let cast: [String]
}

struct DoubanComment: Hashable, Identifiable {
    var id: String { "\(author)-\(content)" }
    let author: String
    let content: String
    let rating: String
}

struct DoubanRecommendation: Hashable, Identifiable {
    let id: String
    let title: String
    let cover: String
    let rating: String
}
