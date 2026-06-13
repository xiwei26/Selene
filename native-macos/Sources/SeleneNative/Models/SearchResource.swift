import Foundation

struct SearchResource: Codable, Identifiable {
    let id: String
    let key: String
    let name: String
    let api: String
    let detail: String
    let from: String
    let disabled: Bool

    enum CodingKeys: String, CodingKey {
        case id, key, name, api, detail, from, disabled
    }

    init(key: String, name: String, api: String, detail: String, from: String, disabled: Bool) {
        self.id = key
        self.key = key
        self.name = name
        self.api = api
        self.detail = detail
        self.from = from
        self.disabled = disabled
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        key = try container.decode(String.self, forKey: .key)
        name = try container.decode(String.self, forKey: .name)
        api = try container.decode(String.self, forKey: .api)
        detail = try container.decodeIfPresent(String.self, forKey: .detail) ?? ""
        from = try container.decodeIfPresent(String.self, forKey: .from) ?? ""
        disabled = try container.decodeIfPresent(Bool.self, forKey: .disabled) ?? false
        id = key
    }
}
