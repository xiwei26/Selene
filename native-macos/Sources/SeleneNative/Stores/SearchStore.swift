import SwiftUI
import Combine

final class SearchStore: ObservableObject {
    @Published var query: String = ""
    @Published var results: [SearchResult] = []
    @Published var isLoading: Bool = false
    @Published var selectedResult: SearchResult?
    @Published var errorMessage: String?
    @Published var resources: [SearchResource] = []

    private let provider: ContentProvider

    init(provider: ContentProvider) {
        self.provider = provider
    }

    func search() async {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }

        await MainActor.run {
            isLoading = true
            errorMessage = nil
            results = []
        }

        do {
            let searchResults = try await provider.search(query: query)
            await MainActor.run {
                results = searchResults
                isLoading = false
            }
        } catch {
            await MainActor.run {
                errorMessage = error.localizedDescription
                isLoading = false
            }
        }
    }

    func loadResources() async {
        do {
            let res = try await provider.searchResources()
            await MainActor.run {
                resources = res.filter { !$0.disabled }
            }
        } catch {
            // Non-critical
        }
    }

    func selectResult(_ result: SearchResult) {
        selectedResult = result
    }

    func clearSelection() {
        selectedResult = nil
    }

    func clearError() {
        errorMessage = nil
    }
}
