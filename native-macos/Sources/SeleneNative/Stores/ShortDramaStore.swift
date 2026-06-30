import SwiftUI

@MainActor
@Observable
final class ShortDramaStore {
    var categories: [ShortDramaCategory] = []
    var items: [ShortDramaItem] = []
    var searchQuery = ""
    var selectedCategory: ShortDramaCategory?
    var isLoading = false
    var errorMessage: String?

    @ObservationIgnored private let provider: ShortDramaProviding
    @ObservationIgnored private var page = 1
    @ObservationIgnored private let pageSize = 24

    init(provider: ShortDramaProviding) {
        self.provider = provider
    }

    func loadInitial() async {
        isLoading = true
        errorMessage = nil
        page = 1
        defer { isLoading = false }
        do {
            async let categories = provider.loadCategories()
            async let recommended = provider.loadRecommend(category: nil, size: pageSize)
            self.categories = try await categories
            self.items = (try await recommended).items
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func search() async {
        let query = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else {
            await loadInitial()
            return
        }
        isLoading = true
        errorMessage = nil
        page = 1
        defer { isLoading = false }
        do {
            items = try await provider.search(query: query, page: page, pageSize: pageSize).items
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func load(category: ShortDramaCategory) async {
        selectedCategory = category
        isLoading = true
        errorMessage = nil
        page = 1
        defer { isLoading = false }
        do {
            items = try await provider.loadList(categoryId: category.id, page: page, pageSize: pageSize).items
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func loadMore() async {
        guard !isLoading else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            page += 1
            let query = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
            let result: ShortDramaListResult
            if !query.isEmpty {
                result = try await provider.search(query: query, page: page, pageSize: pageSize)
            } else if let selectedCategory {
                result = try await provider.loadList(categoryId: selectedCategory.id, page: page, pageSize: pageSize)
            } else {
                result = try await provider.loadRecommend(category: nil, size: pageSize)
            }
            items.append(contentsOf: result.items)
        } catch {
            page = max(page - 1, 1)
            errorMessage = error.localizedDescription
        }
    }

    func playURL(for item: ShortDramaItem, episode: Int) async -> URL? {
        guard let request = await playRequest(for: item, episode: episode) else { return nil }
        return request.url
    }

    func playRequest(for item: ShortDramaItem, episode: Int) async -> (url: URL, result: SearchResult, index: Int)? {
        do {
            guard let result = try await provider.parse(id: item.id, episode: episode, name: item.name) else {
                errorMessage = "Short drama playback URL is unavailable"
                return nil
            }
            for candidate in [result.parsedUrl, result.proxyUrl, result.url].compactMap({ $0 }) {
                if let url = URL(string: candidate), ["http", "https"].contains(url.scheme?.lowercased()) {
                    errorMessage = nil
                    let searchResult = item.searchResult(episodeURL: url.absoluteString, episode: episode)
                    return (url, searchResult, 0)
                }
            }
            errorMessage = "Short drama playback URL is unavailable"
            return nil
        } catch {
            errorMessage = error.localizedDescription
            return nil
        }
    }
}

extension ShortDramaItem {
    func searchResult(episodeURL: String, episode: Int) -> SearchResult {
        SearchResult(
            id: id,
            title: name,
            poster: cover,
            episodes: [episodeURL],
            episodeTitles: ["Episode \(episode)"],
            source: "shortdrama",
            sourceName: "Short Drama",
            className: category,
            year: year ?? "",
            description: desc,
            typeName: category
        )
    }
}
