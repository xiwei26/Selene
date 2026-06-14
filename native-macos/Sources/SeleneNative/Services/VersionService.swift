import Foundation

struct VersionInfo: Codable, Hashable {
    var version: String
    var downloadURL: String?
    var releaseNotes: String?

    enum CodingKeys: String, CodingKey {
        case version
        case downloadURL = "download_url"
        case releaseNotes = "release_notes"
    }
}

final class VersionService: @unchecked Sendable {
    private let endpoint: URL?
    private let session: URLSession
    private let userDefaults: UserDefaults

    init(endpoint: URL? = nil, session: URLSession = .shared, userDefaults: UserDefaults = .standard) {
        self.endpoint = endpoint
        self.session = session
        self.userDefaults = userDefaults
    }

    func checkForUpdate(currentVersion: String) async throws -> VersionInfo? {
        guard let endpoint else { return nil }
        let (data, response) = try await session.data(from: endpoint)
        guard let httpResponse = response as? HTTPURLResponse,
              (200...299).contains(httpResponse.statusCode) else {
            throw APIError.responseError(statusCode: (response as? HTTPURLResponse)?.statusCode ?? -1)
        }
        let info = try JSONDecoder().decode(VersionInfo.self, from: data)
        guard Self.isRemoteVersion(info.version, newerThan: currentVersion),
              userDefaults.string(forKey: "selene_dismissed_version") != info.version else {
            return nil
        }
        userDefaults.set(Date().timeIntervalSince1970, forKey: "selene_last_version_check")
        return info
    }

    func dismiss(version: String) {
        userDefaults.set(version, forKey: "selene_dismissed_version")
    }

    static func isRemoteVersion(_ remote: String, newerThan current: String) -> Bool {
        compare(remote, current) == .orderedDescending
    }

    static func compare(_ lhs: String, _ rhs: String) -> ComparisonResult {
        let left = components(lhs)
        let right = components(rhs)
        let count = max(left.count, right.count)
        for index in 0..<count {
            let leftValue = index < left.count ? left[index] : 0
            let rightValue = index < right.count ? right[index] : 0
            if leftValue > rightValue { return .orderedDescending }
            if leftValue < rightValue { return .orderedAscending }
        }
        return .orderedSame
    }

    private static func components(_ version: String) -> [Int] {
        version
            .split(separator: ".")
            .map { part in
                Int(part.prefix { $0.isNumber }) ?? 0
            }
    }
}
