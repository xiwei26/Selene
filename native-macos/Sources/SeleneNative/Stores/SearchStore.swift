import SwiftUI

@Observable
@MainActor
final class SearchStore {
    var query: String = ""
    var results: [SearchResult] = []
    var aggregatedResults: [AggregatedSearchResult] = []
    var isLoading: Bool = false
    var selectedResult: SearchResult?
    var errorMessage: String?
    var resources: [SearchResource] = []
    var searchHistory: [String] = []
    var suggestions: [SearchSuggestion] = []
    var isAggregating: Bool = false
    var sourceFilter: String?
    var yearFilter: String?
    var titleFilter: String = ""
    var blockedKeywordsText: String = ""
    var sortNewestFirst: Bool = true
    var sseProgress = SSESearchClient.SearchProgress()

    @ObservationIgnored private let provider: ContentProvider
    @ObservationIgnored private let sseClient = SSESearchClient()
    @ObservationIgnored private var sseTasks: [Task<Void, Never>] = []

    init(provider: ContentProvider) {
        self.provider = provider
    }

    var filteredResults: [SearchResult] {
        var values = results
        if let sourceFilter {
            values = values.filter { $0.sourceName == sourceFilter || $0.source == sourceFilter }
        }
        if let yearFilter {
            values = values.filter { $0.year == yearFilter }
        }
        let normalizedTitle = titleFilter.trimmingCharacters(in: .whitespacesAndNewlines)
        if !normalizedTitle.isEmpty {
            values = values.filter { $0.title.localizedCaseInsensitiveContains(normalizedTitle) }
        }
        values = contentFilter.filter(values)
        return values.sorted { left, right in
            sortNewestFirst ? left.year > right.year : left.year < right.year
        }
    }

    var filteredAggregatedResults: [AggregatedSearchResult] {
        var values = aggregatedResults
        if let sourceFilter {
            values = values.filter { $0.sourceNames.contains(sourceFilter) }
        }
        if let yearFilter {
            values = values.filter { $0.year == yearFilter }
        }
        let normalizedTitle = titleFilter.trimmingCharacters(in: .whitespacesAndNewlines)
        if !normalizedTitle.isEmpty {
            values = values.filter { $0.title.localizedCaseInsensitiveContains(normalizedTitle) }
        }
        let visibleKeys = Set(contentFilter.filter(results).map { AggregatedSearchResult.fromSearchResult($0).key })
        values = values.filter { visibleKeys.contains($0.key) }
        return values.sorted { left, right in
            sortNewestFirst ? left.year > right.year : left.year < right.year
        }
    }

    private var contentFilter: ContentFilterService {
        ContentFilterService(
            blockedKeywords: blockedKeywordsText
                .split(separator: ",")
                .map(String.init)
        )
    }

    var availableSources: [String] {
        Array(Set(results.map { $0.sourceName.isEmpty ? $0.source : $0.sourceName })).sorted()
    }

    var availableYears: [String] {
        Array(Set(results.map(\.year).filter { !$0.isEmpty })).sorted(by: >)
    }

    func search() async {
        let trimmedQuery = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedQuery.isEmpty else { return }

        isLoading = true
        errorMessage = nil
        results = []
        aggregatedResults = []

        do {
            let searchResults = try await provider.search(query: trimmedQuery)
            results = searchResults
            rebuildAggregates()
            try? await provider.addSearchHistory(query: trimmedQuery)
            await loadHistory()
        } catch {
            errorMessage = error.localizedDescription
        }

        isLoading = false
    }

    func searchWithSSE(session: LoginSession?) async {
        guard let session,
              let _ = provider.sseSearchURL(query: query) else {
            await search()
            return
        }

        let trimmedQuery = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedQuery.isEmpty else { return }

        stopStreamingSearch()
        isLoading = true
        errorMessage = nil
        results = []
        aggregatedResults = []
        sseProgress = SSESearchClient.SearchProgress()

        sseTasks = [
            Task { [weak self] in
                guard let self else { return }
                for await batch in self.sseClient.incrementalResults {
                    await MainActor.run {
                        self.results.append(contentsOf: batch)
                        self.rebuildAggregates()
                    }
                }
            },
            Task { [weak self] in
                guard let self else { return }
                for await progress in self.sseClient.progress {
                    await MainActor.run {
                        self.sseProgress = progress
                        self.isLoading = !progress.isComplete
                    }
                }
            },
            Task { [weak self] in
                guard let self else { return }
                for await error in self.sseClient.errors {
                    await MainActor.run {
                        self.errorMessage = error
                    }
                }
            }
        ]

        await sseClient.startSearch(query: trimmedQuery, serverURL: session.serverURL, cookie: session.cookie)
        try? await provider.addSearchHistory(query: trimmedQuery)
        await loadHistory()
    }

    func stopStreamingSearch() {
        sseClient.stopSearch()
        sseTasks.forEach { $0.cancel() }
        sseTasks.removeAll()
        isLoading = false
    }

    func loadResources() async {
        do {
            resources = try await provider.searchResources().filter { !$0.disabled }
        } catch {
            // Resource loading is non-critical for search.
        }
    }

    func loadHistory() async {
        do {
            searchHistory = try await provider.getSearchHistory()
        } catch {
            searchHistory = []
        }
    }

    func loadSuggestions() async {
        let trimmedQuery = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedQuery.isEmpty else {
            suggestions = []
            return
        }
        do {
            suggestions = try await provider.searchSuggestions(query: trimmedQuery)
        } catch {
            suggestions = []
        }
    }

    func deleteHistory(_ query: String) async {
        try? await provider.deleteSearchHistory(query: query)
        await loadHistory()
    }

    func clearHistory() async {
        try? await provider.clearSearchHistory()
        searchHistory = []
    }

    func selectResult(_ result: SearchResult) {
        selectedResult = result
    }

    func selectAggregate(_ aggregate: AggregatedSearchResult) {
        selectedResult = aggregate.originalResults.first
    }

    func clearSelection() {
        selectedResult = nil
    }

    func clearError() {
        errorMessage = nil
    }

    func clearFilters() {
        sourceFilter = nil
        yearFilter = nil
        titleFilter = ""
    }

    private func rebuildAggregates() {
        var grouped: [String: AggregatedSearchResult] = [:]
        for result in results {
            let aggregate = AggregatedSearchResult.fromSearchResult(result)
            if var existing = grouped[aggregate.key] {
                existing.addResult(result)
                grouped[aggregate.key] = existing
            } else {
                grouped[aggregate.key] = aggregate
            }
        }
        aggregatedResults = grouped.values.sorted { $0.addedTimestamp < $1.addedTimestamp }
    }
}
