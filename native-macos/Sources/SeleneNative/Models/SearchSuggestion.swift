import Foundation

struct SearchSuggestion: Codable, Hashable {
    var text: String
    var type: String
    var score: Double
}
