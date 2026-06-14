import Foundation

enum M3U8Service {
    static func resolutionRank(from url: String) -> Int {
        let lowered = url.lowercased()
        if lowered.contains("2160") || lowered.contains("4k") { return 4 }
        if lowered.contains("1080") { return 3 }
        if lowered.contains("720") { return 2 }
        if lowered.contains("480") { return 1 }
        return 0
    }

    static func sortedByLikelyQuality(_ urls: [String]) -> [String] {
        urls.sorted { resolutionRank(from: $0) > resolutionRank(from: $1) }
    }
}
