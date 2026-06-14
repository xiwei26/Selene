import Foundation

final class CacheService: @unchecked Sendable {
    static let shared = CacheService()

    private let directory: URL
    private let fileManager: FileManager

    init(namespace: String = "CacheService", fileManager: FileManager = .default) {
        self.fileManager = fileManager
        let base = fileManager.urls(for: .cachesDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        directory = base
            .appendingPathComponent("com.selene.native", isDirectory: true)
            .appendingPathComponent(namespace, isDirectory: true)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    func save<T: Codable>(key: String, data: T, maxAge: TimeInterval) throws {
        let payload = try JSONEncoder().encode(data)
        try payload.write(to: dataURL(for: key), options: .atomic)
        let metadata = CacheMetadata(saveTime: Date().timeIntervalSince1970, maxAge: maxAge)
        try JSONEncoder().encode(metadata).write(to: metadataURL(for: key), options: .atomic)
    }

    func load<T: Codable>(key: String, maxAge: TimeInterval) -> T? {
        let metadataURL = metadataURL(for: key)
        let dataURL = dataURL(for: key)
        guard let metadataData = try? Data(contentsOf: metadataURL),
              let metadata = try? JSONDecoder().decode(CacheMetadata.self, from: metadataData),
              let payload = try? Data(contentsOf: dataURL) else {
            return nil
        }

        let effectiveMaxAge = min(maxAge, metadata.maxAge)
        guard Date().timeIntervalSince1970 - metadata.saveTime <= effectiveMaxAge else {
            remove(key: key)
            return nil
        }

        return try? JSONDecoder().decode(T.self, from: payload)
    }

    func remove(key: String) {
        try? fileManager.removeItem(at: dataURL(for: key))
        try? fileManager.removeItem(at: metadataURL(for: key))
    }

    func clearExpired() {
        guard let files = try? fileManager.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) else { return }
        for file in files where file.pathExtension == "meta" {
            guard let data = try? Data(contentsOf: file),
                  let metadata = try? JSONDecoder().decode(CacheMetadata.self, from: data),
                  Date().timeIntervalSince1970 - metadata.saveTime > metadata.maxAge else {
                continue
            }
            let key = file.deletingPathExtension().lastPathComponent
            remove(key: key)
        }
    }

    func clearAll() {
        try? fileManager.removeItem(at: directory)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    private func dataURL(for key: String) -> URL {
        directory.appendingPathComponent(sanitized(key)).appendingPathExtension("json")
    }

    private func metadataURL(for key: String) -> URL {
        directory.appendingPathComponent(sanitized(key)).appendingPathExtension("meta")
    }

    private func sanitized(_ key: String) -> String {
        key.map { character in
            character.isLetter || character.isNumber || character == "-" || character == "_" ? character : "_"
        }
        .map(String.init)
        .joined()
    }
}

private struct CacheMetadata: Codable {
    var saveTime: TimeInterval
    var maxAge: TimeInterval
}
