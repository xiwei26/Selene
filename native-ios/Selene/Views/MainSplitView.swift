import SwiftUI

struct MainSplitView: View {
    private enum SidebarDestination: String, CaseIterable, Identifiable, Hashable {
        case home
        case search
        case movie
        case tv
        case anime
        case show
        case live
        case shortDrama
        case bilibili
        case youtube
        case favorites
        case history
        case admin
        case settings

        var id: String { rawValue }

        var title: String {
            switch self {
            case .home: "首页"
            case .search: "搜索"
            case .movie: "电影"
            case .tv: "剧集"
            case .anime: "动漫"
            case .show: "综艺"
            case .live: "直播"
            case .shortDrama: "短剧"
            case .bilibili: "Bilibili"
            case .youtube: "YouTube"
            case .favorites: "收藏"
            case .history: "历史"
            case .admin: "管理后台"
            case .settings: "设置"
            }
        }

        var systemImage: String {
            switch self {
            case .home: "house"
            case .search: "magnifyingglass"
            case .movie: MediaCategory.movie.systemImage
            case .tv: MediaCategory.tv.systemImage
            case .anime: MediaCategory.anime.systemImage
            case .show: MediaCategory.show.systemImage
            case .live: "dot.radiowaves.left.and.right"
            case .shortDrama: "play.square.stack"
            case .bilibili: "tv.badge.wifi"
            case .youtube: "play.rectangle"
            case .favorites: "heart"
            case .history: "clock"
            case .admin: "gearshape.2"
            case .settings: "gearshape"
            }
        }

        var category: MediaCategory? {
            switch self {
            case .movie: .movie
            case .tv: .tv
            case .anime: .anime
            case .show: .show
            default: nil
            }
        }
    }

    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator
    @State private var selection: SidebarDestination? = .home
    @State private var columnVisibility: NavigationSplitViewVisibility = .all

    var body: some View {
        NavigationSplitView(columnVisibility: $columnVisibility) {
            List(selection: $selection) {
                Section {
                    sidebarRow(.home)
                    sidebarRow(.search)
                }

                Section("浏览") {
                    sidebarRow(.movie)
                    sidebarRow(.tv)
                    sidebarRow(.anime)
                    sidebarRow(.show)
                    sidebarRow(.live)
                    sidebarRow(.shortDrama)
                    sidebarRow(.bilibili)
                    sidebarRow(.youtube)
                }

                Section("片库") {
                    sidebarRow(.favorites)
                    sidebarRow(.history)
                }

                Section {
                    sidebarRow(.admin)
                    sidebarRow(.settings)
                }
            }
            .listStyle(.sidebar)
            .navigationTitle("Selene")
            .navigationSplitViewColumnWidth(min: 210, ideal: 240, max: 300)
        } detail: {
            NavigationStack {
                ZStack {
                    AppBackground()
                    content
                }
                .navigationTitle(selection?.title ?? "Selene")
                .navigationBarTitleDisplayMode(.inline)
                .navigationDestination(for: DoubanMovie.self) { movie in
                    MediaDetailView(movie: movie)
                }
                .navigationDestination(for: SearchResult.self) { result in
                    SearchDetailView(result: result)
                }
                .toolbar {
                    ToolbarItem(placement: .topBarTrailing) {
                        if let session = appModel.session {
                            Label(session.username, systemImage: "checkmark.circle.fill")
                                .font(.caption.weight(.medium))
                                .foregroundStyle(.secondary)
                        }
                    }
                }
            }
        }
        .navigationSplitViewStyle(.balanced)
        .task(id: appModel.session?.id) {
            await appModel.refreshLibrary()
        }
        .fullScreenCover(isPresented: playbackPresentation) {
            PlaybackView()
        }
    }

    private func sidebarRow(_ section: SidebarDestination) -> some View {
        Label(section.title, systemImage: section.systemImage)
            .tag(section)
            .accessibilityIdentifier("sidebar.\(section.rawValue)")
    }

    @ViewBuilder
    private var content: some View {
        switch selection ?? .home {
        case .home:
            HomeView()
        case .search:
            SearchView()
        case .movie, .tv, .anime, .show:
            if let category = selection?.category {
                CategoryView(category: category)
            }
        case .live:
            LiveView()
        case .shortDrama:
            PlatformFeatureView(feature: .shortDrama)
        case .bilibili:
            PlatformFeatureView(feature: .bilibili)
        case .youtube:
            PlatformFeatureView(feature: .youtube)
        case .favorites:
            FavoritesView()
        case .history:
            HistoryView()
        case .admin:
            AdminView()
        case .settings:
            SettingsView()
        }
    }

    private var playbackPresentation: Binding<Bool> {
        Binding {
            playerCoordinator.isPlaying
        } set: { isPresented in
            if !isPresented, playerCoordinator.isPlaying {
                playerCoordinator.stop()
            }
        }
    }
}