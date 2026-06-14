import SwiftUI

@Observable
final class FavoritesStore {
    var favorites: [FavoriteItem] = []
    var isLoading: Bool = false
    var errorMessage: String?

    func loadFavorites(provider: ContentProvider) async {
        isLoading = true
        defer { isLoading = false }
        do {
            favorites = try await provider.getFavorites()
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func toggleFavorite(source: String, id: String, data: [String: Any], provider: ContentProvider) async {
        if isFavorited(source: source, id: id) {
            do {
                try await provider.removeFavorite(source: source, id: id)
                favorites.removeAll { $0.id == "\(source)+\(id)" }
            } catch {
                errorMessage = error.localizedDescription
            }
        } else {
            do {
                try await provider.addFavorite(source: source, id: id, data: data)
                await loadFavorites(provider: provider)
            } catch {
                errorMessage = error.localizedDescription
            }
        }
    }

    func isFavorited(source: String, id: String) -> Bool {
        favorites.contains { $0.id == "\(source)+\(id)" }
    }
}
