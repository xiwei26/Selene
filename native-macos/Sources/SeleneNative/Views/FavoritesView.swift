import SwiftUI

struct FavoritesView: View {
    let favoritesStore: FavoritesStore
    let provider: ContentProvider
    let onPlayRecord: ((PlayRecord) -> Void)?

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
                    .onTapGesture {
                        let record = PlayRecord(
                            id: item.id,
                            source: item.source,
                            title: item.title,
                            sourceName: item.sourceName,
                            year: item.year,
                            cover: item.cover,
                            index: 0,
                            totalEpisodes: item.totalEpisodes,
                            playTime: 0,
                            totalTime: 0,
                            saveTime: item.saveTime,
                            searchTitle: item.title
                        )
                        onPlayRecord?(record)
                    }
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
