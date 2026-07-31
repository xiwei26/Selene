import Foundation
import Observation

struct SearchAggregate: Identifiable {
    let key: String
    let representative: SearchResult
    let sourceNames: [String]

    var id: String { key }
}

enum LoadPhase: Equatable {
    case idle
    case loading
    case loaded
    case failed(String)

    var isLoading: Bool {
        self == .loading
    }
}

@MainActor
@Observable
final class HomeStore {
    var movies: [DoubanMovie] = []
    var tvShows: [DoubanMovie] = []
    var anime: [DoubanMovie] = []
    var shows: [DoubanMovie] = []
    var bangumi: [BangumiItem] = []
    var phase: LoadPhase = .idle

    func load(using client: LunaTVClient, force: Bool = false) async {
        if !force {
            switch phase {
            case .idle, .failed:
                break
            case .loading, .loaded:
                return
            }
        }

        phase = .loading
        async let movieRequest = client.category(.movie)
        async let tvRequest = client.category(.tv)
        async let animeRequest = client.category(.anime)
        async let showRequest = client.category(.show)
        async let bangumiRequest = client.bangumiCalendar()

        let movieValue = try? await movieRequest
        let tvValue = try? await tvRequest
        let animeValue = try? await animeRequest
        let showValue = try? await showRequest
        let bangumiValue = try? await bangumiRequest

        movies = movieValue ?? []
        tvShows = tvValue ?? []
        anime = animeValue ?? []
        shows = showValue ?? []
        bangumi = bangumiValue ?? []

        if [movies, tvShows, anime, shows].allSatisfy(\.isEmpty) {
            phase = .failed("暂时没有加载到内容，请检查服务器的 Douban 接口。")
        } else {
            phase = .loaded
        }
    }
}

@MainActor
@Observable
final class CategoryStore {
    var items: [DoubanMovie] = []
    var phase: LoadPhase = .idle

    func load(category: MediaCategory, using client: LunaTVClient, force: Bool = false) async {
        guard force || phase == .idle else { return }
        phase = .loading

        do {
            items = try await client.category(category)
            phase = .loaded
        } catch is CancellationError {
            phase = .idle
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class MediaDetailStore {
    var details: DoubanMovieDetails?
    var phase: LoadPhase = .idle

    func load(id: String, using client: LunaTVClient) async {
        guard phase == .idle else { return }
        phase = .loading
        do {
            details = try await client.doubanDetails(id: id)
            phase = .loaded
        } catch is CancellationError {
            phase = .idle
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class SearchStore {
    var query = ""
    var submittedQuery = ""
    var results: [SearchResult] = []
    var suggestions: [SearchSuggestion] = []
    var completedSources = 0
    var totalSources = 0
    var currentSource: String?
    var sourceFilter: String?
    var yearFilter: String?
    var blockedKeywordsText = ""
    var isAggregating = false
    var sortNewestFirst = true
    var phase: LoadPhase = .idle

    var availableSources: [String] {
        Array(Set(results.map { $0.sourceName.isEmpty ? $0.source : $0.sourceName })).sorted()
    }

    var availableYears: [String] {
        Array(Set(results.map(\.year).filter { !$0.isEmpty })).sorted(by: >)
    }

    var filteredResults: [SearchResult] {
        results
            .filter(matchesFilters)
            .sorted { sortNewestFirst ? $0.year > $1.year : $0.year < $1.year }
    }

    var filteredAggregates: [SearchAggregate] {
        let grouped = Dictionary(grouping: filteredResults) {
            "\($0.title)|\($0.year)|\($0.typeName ?? "unknown")"
        }
        return grouped.map { key, values in
            SearchAggregate(
                key: key,
                representative: values[0],
                sourceNames: Array(
                    Set(values.map { $0.sourceName.isEmpty ? $0.source : $0.sourceName })
                ).sorted()
            )
        }
        .sorted {
            sortNewestFirst
                ? $0.representative.year > $1.representative.year
                : $0.representative.year < $1.representative.year
        }
    }

    func submit() {
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalized.isEmpty else {
            submittedQuery = ""
            results = []
            phase = .idle
            return
        }
        submittedQuery = normalized
    }

    func search(using client: LunaTVClient, useStreaming: Bool) async {
        guard !submittedQuery.isEmpty else { return }
        results = []
        completedSources = 0
        totalSources = 0
        currentSource = nil
        phase = .loading

        do {
            if useStreaming {
                let updates = await client.streamingSearch(query: submittedQuery)
                for try await update in updates {
                    appendUnique(update.results)
                    completedSources = update.completedSources
                    totalSources = update.totalSources
                    currentSource = update.currentSource
                }
            } else {
                results = try await client.search(query: submittedQuery)
            }
            try? await client.addSearchHistory(submittedQuery)
            try Task.checkCancellation()
            phase = .loaded
        } catch is CancellationError {
            return
        } catch {
            do {
                if results.isEmpty {
                    results = try await client.search(query: submittedQuery)
                }
                try? await client.addSearchHistory(submittedQuery)
                phase = .loaded
            } catch {
                phase = .failed(error.localizedDescription)
            }
        }
    }

    func loadSuggestions(using client: LunaTVClient) async {
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard normalized.count >= 2 else {
            suggestions = []
            return
        }
        try? await Task.sleep(for: .milliseconds(350))
        guard !Task.isCancelled else { return }
        suggestions = (try? await client.suggestions(query: normalized)) ?? []
    }

    private func appendUnique(_ batch: [SearchResult]) {
        var existing = Set(results.map { "\($0.source)|\($0.id)" })
        for result in batch where existing.insert("\(result.source)|\(result.id)").inserted {
            results.append(result)
        }
    }

    func clearFilters() {
        sourceFilter = nil
        yearFilter = nil
        blockedKeywordsText = ""
    }

    private func matchesFilters(_ result: SearchResult) -> Bool {
        if let sourceFilter {
            let sourceName = result.sourceName.isEmpty ? result.source : result.sourceName
            guard sourceName == sourceFilter || result.source == sourceFilter else { return false }
        }
        if let yearFilter, result.year != yearFilter {
            return false
        }
        let keywords = blockedKeywordsText
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
            .filter { !$0.isEmpty }
        let searchableText = [
            result.title,
            result.description ?? "",
            result.sourceName,
            result.typeName ?? ""
        ]
        .joined(separator: " ")
        .lowercased()
        return !keywords.contains { searchableText.contains($0) }
    }
}

@MainActor
@Observable
final class PlayableResourceStore {
    var resources: [SearchResult] = []
    var phase: LoadPhase = .idle

    func find(title: String, using client: LunaTVClient) async {
        phase = .loading
        do {
            resources = try await client.search(query: title)
            phase = .loaded
        } catch is CancellationError {
            phase = .idle
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }
}

@MainActor
@Observable
final class SearchDetailStore {
    var detail: SearchResult?
    var alternatives: [SearchResult] = []
    var phase: LoadPhase = .idle

    func load(result: SearchResult, using client: LunaTVClient) async {
        guard phase == .idle else { return }
        phase = .loading

        do {
            async let detailRequest = client.detail(source: result.source, id: result.id)
            async let alternativesRequest = client.search(query: result.title)
            detail = try await detailRequest
            alternatives = (try? await alternativesRequest) ?? [result]
            phase = .loaded
        } catch is CancellationError {
            phase = .idle
        } catch {
            detail = result
            phase = .failed(error.localizedDescription)
        }
    }
}