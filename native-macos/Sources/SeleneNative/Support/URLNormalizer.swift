import Foundation

enum URLNormalizer {
    /// Normalize a user-provided server URL string.
    /// - Adds "https://" if no scheme is present
    /// - Falls back to http:// for localhost-style URLs
    static func normalize(_ input: String) -> URL? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        // Try with https:// prefix
        if !trimmed.hasPrefix("http://") && !trimmed.hasPrefix("https://") {
            let httpsURL = URL(string: "https://\(trimmed)")
            if let httpsURL, httpsURL.host != nil {
                return httpsURL
            }
            // Try http for localhost-like inputs
            let httpURL = URL(string: "http://\(trimmed)")
            if let httpURL, httpURL.host != nil {
                return httpURL
            }
            return nil
        }

        // Already has a scheme
        return URL(string: trimmed)
    }
}
