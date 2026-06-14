import SwiftUI

struct FavoritesView: View {
    let favoritesStore: FavoritesStore
    let provider: ContentProvider

    var body: some View {
        Group {
            if favoritesStore.favorites.isEmpty {
                ContentUnavailableView("暂无收藏", systemImage: "heart", description: Text("在详情页收藏后会显示在这里"))
            } else {
                List(favoritesStore.favorites) { item in
                    VideoCardView(
                        title: item.title,
                        poster: item.cover,
                        sourceName: item.sourceName,
                        year: item.year,
                        subtitle: "共\(item.totalEpisodes)集"
                    )
                }
            }
        }
        .task {
            await favoritesStore.loadFavorites(provider: provider)
        }
        .toolbar {
            Button {
                Task { await favoritesStore.loadFavorites(provider: provider) }
            } label: {
                Label("刷新", systemImage: "arrow.clockwise")
            }
        }
    }
}
