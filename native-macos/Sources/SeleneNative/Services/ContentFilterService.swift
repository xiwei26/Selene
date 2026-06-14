import Foundation

struct ContentFilterService: Sendable {
    var blockedKeywords: [String]

    init(blockedKeywords: [String] = []) {
        self.blockedKeywords = blockedKeywords
    }

    func shouldHide(_ result: SearchResult) -> Bool {
        let haystack = [
            result.title,
            result.description ?? "",
            result.sourceName,
            result.typeName ?? ""
        ]
        .joined(separator: " ")
        .lowercased()

        return blockedKeywords
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
            .filter { !$0.isEmpty }
            .contains { haystack.contains($0) }
    }

    func filter(_ results: [SearchResult]) -> [SearchResult] {
        results.filter { !shouldHide($0) }
    }
}
