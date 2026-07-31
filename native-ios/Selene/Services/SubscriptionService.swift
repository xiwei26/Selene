import CryptoKit
import Foundation

enum Base58 {
    private static let alphabet = Array("123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz")

    static func decode(_ value: String) -> [UInt8]? {
        guard !value.isEmpty else { return [] }
        var bytes = [UInt8](repeating: 0, count: 1)

        for character in value {
            guard let digit = alphabet.firstIndex(of: character) else { return nil }
            var carry = digit
            for index in bytes.indices {
                carry += Int(bytes[index]) * 58
                bytes[index] = UInt8(carry & 0xff)
                carry >>= 8
            }
            while carry > 0 {
                bytes.append(UInt8(carry & 0xff))
                carry >>= 8
            }
        }

        bytes.append(contentsOf: value.prefix(while: { $0 == "1" }).map { _ in 0 })
        return bytes.reversed()
    }
}

enum SubscriptionService {
    struct Content {
        let searchResources: [SearchResource]
        let liveSources: [LiveSource]
    }

    static func parse(_ content: String) -> Content? {
        guard let decoded = Base58.decode(content),
              let object = try? JSONSerialization.jsonObject(with: Data(decoded)) as? [String: Any] else {
            return nil
        }

        let resources: [SearchResource]
        if let sites = object["api_site"] as? [String: [String: Any]] {
            resources = sites.map { key, value in
                SearchResource(
                    key: key,
                    name: value["name"] as? String ?? key,
                    api: value["api"] as? String ?? "",
                    detail: value["detail"] as? String ?? "",
                    from: value["from"] as? String ?? "",
                    disabled: value["disabled"] as? Bool ?? false
                )
            }
            .sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
        } else {
            resources = []
        }

        let liveSources = (object["lives"] as? [[String: Any]] ?? []).enumerated().map { index, value in
            LiveSource(
                key: value["key"] as? String ?? value["name"] as? String ?? "live-\(index)",
                name: value["name"] as? String ?? "直播源 \(index + 1)",
                url: value["url"] as? String ?? "",
                ua: value["ua"] as? String ?? "",
                epg: value["epg"] as? String ?? "",
                from: value["from"] as? String ?? "",
                disabled: value["disabled"] as? Bool ?? false
            )
        }

        guard !resources.isEmpty || !liveSources.isEmpty else { return nil }
        return Content(searchResources: resources, liveSources: liveSources)
    }
}