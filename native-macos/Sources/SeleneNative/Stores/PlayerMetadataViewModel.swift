import Foundation

@MainActor
@Observable
final class PlayerMetadataViewModel {
    var result: SearchResult?
    var tmdbBackdrop: TMDBBackdrop?
    var quickInfo: DoubanQuickInfo?
    var comments: [DoubanComment] = []
    var recommendations: [DoubanRecommendation] = []
    var overview: String?
    var isLoading = false

    @ObservationIgnored private var loadGeneration = 0

    func load(result: SearchResult, provider: ContentProvider?) async {
        loadGeneration += 1
        let generation = loadGeneration

        self.result = result
        tmdbBackdrop = nil
        quickInfo = nil
        comments = []
        recommendations = []
        overview = result.description
        isLoading = true

        defer {
            if generation == loadGeneration {
                isLoading = false
            }
        }

        guard let provider else { return }

        async let backdrop = optional {
            try await provider.getTMDBBackdrop(
                title: result.title,
                year: result.year.isEmpty ? nil : result.year,
                type: Self.inferTMDBType(from: result)
            )
        }
        async let quick = optional {
            try await provider.getDoubanQuickInfo(title: result.title)
        }

        let doubanId = result.doubanID.map(String.init)
        async let loadedComments: [DoubanComment] = optionalList {
            guard let doubanId else { return [] }
            return try await provider.getDoubanComments(doubanId: doubanId)
        }
        async let loadedRecommendations: [DoubanRecommendation] = optionalList {
            guard let doubanId else { return [] }
            return try await provider.getDoubanRecommendations(doubanId: doubanId)
        }

        let loadedBackdrop = await backdrop
        let loadedQuickInfo = await quick
        let loadedCommentsValue = await loadedComments
        let loadedRecommendationsValue = await loadedRecommendations

        guard generation == loadGeneration, !Task.isCancelled else { return }

        tmdbBackdrop = loadedBackdrop
        quickInfo = loadedQuickInfo
        comments = loadedCommentsValue
        recommendations = loadedRecommendationsValue
        overview = firstNonEmpty(
            tmdbBackdrop?.overview,
            quickInfo?.summary,
            result.description
        )
    }

    private static func inferTMDBType(from result: SearchResult) -> String {
        let text = [
            result.source,
            result.typeName,
            result.className
        ]
            .compactMap { $0 }
            .joined(separator: " ")
            .lowercased()

        return text.contains("movie") || text.contains("电影") ? "movie" : "tv"
    }

    private func firstNonEmpty(_ values: String?...) -> String? {
        values.first { value in
            guard let value else { return false }
            return !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        } ?? nil
    }

    private func optional<T>(_ operation: @Sendable () async throws -> T?) async -> T? {
        do {
            return try await operation()
        } catch {
            return nil
        }
    }

    private func optionalList<T>(_ operation: @Sendable () async throws -> [T]) async -> [T] {
        do {
            return try await operation()
        } catch {
            return []
        }
    }
}