import Foundation
import CryptoKit

enum Base58 {
    private static let alphabet = Array("123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz")

    static func encode(_ bytes: [UInt8]) -> String {
        guard !bytes.isEmpty else { return "" }
        var digits = [UInt8](repeating: 0, count: 1)

        for byte in bytes {
            var carry = Int(byte)
            for index in digits.indices {
                carry += Int(digits[index]) << 8
                digits[index] = UInt8(carry % 58)
                carry /= 58
            }
            while carry > 0 {
                digits.append(UInt8(carry % 58))
                carry /= 58
            }
        }

        var result = String(bytes.prefix { $0 == 0 }.map { _ in Character("1") })
        for digit in digits.reversed() {
            result.append(alphabet[Int(digit)])
        }
        return result
    }

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

        for character in value.prefix(while: { $0 == "1" }) {
            if character == "1" {
                bytes.append(0)
            }
        }

        return bytes.reversed()
    }
}

final class SubscriptionService {
    struct SubscriptionContent {
        var searchResources: [SearchResource]?
        var liveSources: [LiveSource]?
    }

    static func parseSubscriptionContent(_ content: String) -> SubscriptionContent? {
        guard let decoded = Base58.decode(content),
              let json = String(bytes: decoded, encoding: .utf8),
              let data = json.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }

        var resources: [SearchResource] = []
        if let apiSite = object["api_site"] as? [String: [String: Any]] {
            resources = apiSite.map { key, value in
                SearchResource(
                    key: key,
                    name: value["name"] as? String ?? key,
                    api: value["api"] as? String ?? "",
                    detail: value["detail"] as? String ?? "",
                    from: value["from"] as? String ?? "",
                    disabled: value["disabled"] as? Bool ?? false
                )
            }
            .sorted { $0.name < $1.name }
        }

        var liveSources: [LiveSource] = []
        if let lives = object["lives"] as? [[String: Any]] {
            liveSources = lives.map { value in
                LiveSource(
                    key: value["key"] as? String
                        ?? value["name"] as? String
                        ?? value["url"] as? String
                        ?? "live-\(indexHash(value))",
                    name: value["name"] as? String ?? "",
                    url: value["url"] as? String ?? "",
                    ua: value["ua"] as? String ?? "",
                    epg: value["epg"] as? String ?? "",
                    from: value["from"] as? String ?? "",
                    disabled: value["disabled"] as? Bool ?? false
                )
            }
        }

        return SubscriptionContent(
            searchResources: resources.isEmpty ? nil : resources,
            liveSources: liveSources.isEmpty ? nil : liveSources
        )
    }

    private static func indexHash(_ value: [String: Any]) -> String {
        let description = value.keys.sorted().map { key in
            "\(key)=\(value[key] ?? "")"
        }.joined(separator: "|")
        let digest = SHA256.hash(data: Data(description.utf8))
        return digest.map { String(format: "%02x", $0) }.joined()
    }
}
