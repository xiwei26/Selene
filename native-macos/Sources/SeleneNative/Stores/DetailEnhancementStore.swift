import SwiftUI

@MainActor
@Observable
final class DetailEnhancementStore {
    var backdrop: TmdbBackdropResult?
    var quickInfo: DoubanQuickInfo?
    var comments: [DoubanComment] = []
    var recommendations: [DoubanMovie] = []
    var celebrityWorks: [DoubanCelebrityWork] = []
    var trailer: TrailerRefreshResult?
    var errorMessage: String?
    var isLoading = false

    @ObservationIgnored private let provider: MetadataEnhancementProviding

    init(provider: MetadataEnhancementProviding) {
        self.provider = provider
    }

    func load(title: String, year: String, sourceType: String?, doubanId: Int?) async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        backdrop = await optional {
            try await provider.loadBackdrop(title: title, originalTitle: nil, year: year, sourceType: sourceType)
        }

        guard let doubanId else { return }
        let id = String(doubanId)
        quickInfo = await optional { try await provider.loadDoubanQuickInfo(id: id) }
        comments = await array { try await provider.loadDoubanComments(id: id, start: 0, limit: 10, sort: "new_score") }
        recommendations = await array { try await provider.loadDoubanRecommends(kind: sourceType ?? "movie", limit: 20, start: 0) }
        trailer = await optional { try await provider.refreshTrailer(id: id, force: false) }
    }

    func refreshTrailer(doubanId: Int, force: Bool) async {
        do {
            trailer = try await provider.refreshTrailer(id: String(doubanId), force: force)
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func optional<T>(_ operation: @escaping @Sendable () async throws -> T?) async -> T? {
        do {
            return try await operation()
        } catch {
            return nil
        }
    }

    private func array<T>(_ operation: @escaping @Sendable () async throws -> [T]) async -> [T] {
        do {
            return try await operation()
        } catch {
            return []
        }
    }
}
