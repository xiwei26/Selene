import Foundation

struct AggregatedSearchResult: Identifiable, Hashable {
    var id: String { key }
    let key: String
    let title: String
    let year: String
    let type: String
    let cover: String
    var episodeCounts: [String: Int]
    var doubanIds: [String: Int]
    var sourceNames: [String]
    var originalResults: [SearchResult]
    let addedTimestamp: Int64

    var mostCommonEpisodeCount: Int {
        let counts = episodeCounts.values
        return counts.min() ?? 0
    }

    var mostCommonDoubanId: String? {
        doubanIds.max { left, right in
            if left.value == right.value {
                return left.key > right.key
            }
            return left.value < right.value
        }?.key
    }

    static func fromSearchResult(_ result: SearchResult) -> AggregatedSearchResult {
        let type = result.typeName?.isEmpty == false ? result.typeName! : "unknown"
        let sourceName = result.sourceName.isEmpty ? result.source : result.sourceName
        var doubanIds: [String: Int] = [:]
        if let doubanID = result.doubanID {
            doubanIds[String(doubanID)] = 1
        }

        return AggregatedSearchResult(
            key: "\(result.title)|\(result.year)|\(type)",
            title: result.title,
            year: result.year,
            type: type,
            cover: result.poster,
            episodeCounts: [sourceName: result.episodes.count],
            doubanIds: doubanIds,
            sourceNames: [sourceName],
            originalResults: [result],
            addedTimestamp: Int64(Date().timeIntervalSince1970 * 1000)
        )
    }

    mutating func addResult(_ result: SearchResult) {
        let sourceName = result.sourceName.isEmpty ? result.source : result.sourceName
        episodeCounts[sourceName] = result.episodes.count
        if !sourceNames.contains(sourceName) {
            sourceNames.append(sourceName)
        }
        if let doubanID = result.doubanID {
            let key = String(doubanID)
            doubanIds[key, default: 0] += 1
        }
        originalResults.append(result)
    }
}
