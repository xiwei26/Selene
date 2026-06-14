import Foundation

enum URLNormalizer {
    /// Normalize a user-provided server URL string.
    /// - Adds "https://" if no scheme is present
    /// - Falls back to http:// for localhost-style URLs
    static func normalize(_ input: String) -> URL? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        if trimmed.hasPrefix("http://") || trimmed.hasPrefix("https://") {
            let url = URL(string: trimmed)
            return url?.host == nil ? nil : url
        }

        guard !trimmed.contains("://") else { return nil }

        let scheme = isLocalhost(trimmed) ? "http" : "https"
        let url = URL(string: "\(scheme)://\(trimmed)")
        return url?.host == nil ? nil : url
    }

    private static func isLocalhost(_ input: String) -> Bool {
        guard let hostPort = input.split(whereSeparator: { "/?#".contains($0) }).first else {
            return false
        }

        return hostPort == "localhost" || hostPort.hasPrefix("localhost:")
    }
}
