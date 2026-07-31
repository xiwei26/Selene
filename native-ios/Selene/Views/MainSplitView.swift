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

        /// Server-backed destinations are hidden in Base58 local subscription mode.
        var requiresServer: Bool {
            switch self {
            case .home, .movie, .tv, .anime, .show, .shortDrama, .bilibili, .youtube,
                    .favorites, .history, .admin:
                true
            case .search, .live, .settings:
                false
            }
        }
    }

    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator
    @State private var selection: SidebarDestination? = .home
    @State private var columnVisibility: NavigationSplitViewVisibility = .all
    @State private var path = NavigationPath()

    var body: some View {
        NavigationSplitView(columnVisibility: $columnVisibility) {
            List(selection: $selection) {
                Section {
                    if shows(.home) { sidebarRow(.home) }
                    sidebarRow(.search)
                }

                Section("浏览") {
                    if shows(.movie) { sidebarRow(.movie) }
                    if shows(.tv) { sidebarRow(.tv) }
                    if shows(.anime) { sidebarRow(.anime) }
                    if shows(.show) { sidebarRow(.show) }
                    sidebarRow(.live)
                    if shows(.shortDrama) { sidebarRow(.shortDrama) }
                    if shows(.bilibili) { sidebarRow(.bilibili) }
                    if shows(.youtube) { sidebarRow(.youtube) }
                }

                if !appModel.isLocalMode {
                    Section("片库") {
                        sidebarRow(.favorites)
                        sidebarRow(.history)
                    }
                }

                Section {
                    if shows(.admin) {
                        sidebarRow(.admin)
                    }
                    sidebarRow(.settings)
                }
            }
            .listStyle(.sidebar)
            .navigationTitle("Selene")
            .navigationSplitViewColumnWidth(min: 210, ideal: 240, max: 300)
        } detail: {
            NavigationStack(path: $path) {
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
        .onChange(of: selection) { _, _ in
            // NavigationStack keeps pushed views when only the root content
            // changes, which would leave a stale detail on screen.
            path = NavigationPath()
        }
        .onChange(of: appModel.isLocalMode) { _, isLocal in
            // Drop a server-only selection after switching into local mode.
            if isLocal, let selection, selection.requiresServer {
                self.selection = .search
            }
        }
        .task(id: appModel.session?.id) {
            if appModel.isLocalMode, selection?.requiresServer == true {
                selection = .search
            }
            await appModel.refreshLibrary()
        }
        .fullScreenCover(isPresented: playbackPresentation) {
            PlaybackView()
        }
    }

    private func shows(_ destination: SidebarDestination) -> Bool {
        !appModel.isLocalMode || !destination.requiresServer
    }

    private func sidebarRow(_ section: SidebarDestination) -> some View {
        Label(section.title, systemImage: section.systemImage)
            .tag(section)
            .accessibilityIdentifier("sidebar.\(section.rawValue)")
    }

    @ViewBuilder
    private var content: some View {
        switch selection ?? (appModel.isLocalMode ? .search : .home) {
        case .home:
            HomeView()
        case .search:
            SearchView()
        case .movie, .tv, .anime, .show:
            if let category = selection?.category {
                // A multi-pattern case is one ViewBuilder branch, so all four
                // categories share a structural slot. Without an explicit id the
                // CategoryStore @State survives a switch and shows stale posters.
                CategoryView(category: category)
                    .id(category)
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

    /// Single owner of playback teardown and VOD progress persistence.
    private var playbackPresentation: Binding<Bool> {
        Binding {
            playerCoordinator.isPlaying
        } set: { isPresented in
            guard !isPresented, playerCoordinator.isPlaying else { return }
            let record = playerCoordinator.stopAndMakeRecord()
            if let record, !appModel.isLocalMode {
                Task { await appModel.savePlayRecord(record) }
            }
        }
    }
}