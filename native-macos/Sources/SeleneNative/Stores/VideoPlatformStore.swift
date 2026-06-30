import SwiftUI

enum VideoPlatformKind: String, Sendable {
    case bilibili
    case youtube

    var title: String {
        switch self {
        case .bilibili: return "Bilibili"
        case .youtube: return "YouTube"
        }
    }
}

@MainActor
@Observable
final class VideoPlatformStore {
    var items: [VideoPlatformItem] = []
    var regions: [YouTubeRegion] = []
    var searchQuery = ""
    var selectedRegion: YouTubeRegion?
    var isLoading = false
    var errorMessage: String?
    var nextPageToken: String?

    @ObservationIgnored private let provider: VideoPlatformProviding
    @ObservationIgnored private let kind: VideoPlatformKind
    @ObservationIgnored private var page = 1
    @ObservationIgnored private let pageSize = 20

    init(provider: VideoPlatformProviding, kind: VideoPlatformKind) {
        self.provider = provider
        self.kind = kind
    }

    func loadInitial() async {
        isLoading = true
        errorMessage = nil
        page = 1
        defer { isLoading = false }
        do {
            switch kind {
            case .bilibili:
                apply(try await provider.loadBilibiliPopular(page: page, pageSize: pageSize))
            case .youtube:
                regions = try await provider.loadYouTubeRegions()
                selectedRegion = selectedRegion ?? regions.first(where: { $0.code == "US" }) ?? regions.first
                apply(try await provider.loadYouTubePopular(regionCode: selectedRegion?.code ?? "US", pageToken: nil))
            }
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
        defer { isLoading = false }
        do {
            switch kind {
            case .bilibili:
                apply(try await provider.searchBilibili(query: query))
            case .youtube:
                apply(try await provider.searchYouTube(query: query, contentType: "all", order: "relevance", maxResults: 25))
            }
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
            let result: VideoPlatformPage
            switch kind {
            case .bilibili:
                page += 1
                result = try await provider.loadBilibiliPopular(page: page, pageSize: pageSize)
            case .youtube:
                guard let nextPageToken else { return }
                result = try await provider.loadYouTubePopular(regionCode: selectedRegion?.code ?? "US", pageToken: nextPageToken)
            }
            items.append(contentsOf: result.items)
            nextPageToken = result.nextPageToken
        } catch {
            page = max(page - 1, 1)
            errorMessage = error.localizedDescription
        }
    }

    func playableURL(for item: VideoPlatformItem) -> URL? {
        for candidate in [item.playableUrl, item.proxyUrl, item.url].compactMap({ $0 }) {
            if let url = URL(string: candidate), ["http", "https"].contains(url.scheme?.lowercased()) {
                errorMessage = nil
                return url
            }
        }
        errorMessage = "Current item has no playable URL"
        return nil
    }

    private func apply(_ page: VideoPlatformPage) {
        items = page.items
        nextPageToken = page.nextPageToken
    }
}
