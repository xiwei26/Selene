import SwiftUI

struct MainView: View {
    private enum NavigationSection: String, CaseIterable, Identifiable {
        case home, search, movie, tv, anime, show, shortDrama, bilibili, youtube, live, favorites, history, settings

        var id: String { rawValue }

        var title: String {
            switch self {
            case .shortDrama: return "Short Drama"
            case .bilibili: return "Bilibili"
            case .youtube: return "YouTube"
            case .home: return "首页"
            case .search: return "搜索"
            case .movie: return "电影"
            case .tv: return "电视剧"
            case .anime: return "动漫"
            case .show: return "综艺"
            case .live: return "直播"
            case .favorites: return "收藏"
            case .history: return "历史"
            case .settings: return "设置"
            }
        }

        var icon: String {
            switch self {
            case .shortDrama: return "play.rectangle.on.rectangle"
            case .bilibili: return "tv.and.mediabox"
            case .youtube: return "play.tv"
            case .home: return "house"
            case .search: return "magnifyingglass"
            case .movie: return "film"
            case .tv: return "tv"
            case .anime: return "sparkles.tv"
            case .show: return "theatermasks"
            case .live: return "dot.radiowaves.left.and.right"
            case .favorites: return "heart"
            case .history: return "clock"
            case .settings: return "gearshape"
            }
        }
    }

    @Environment(SessionStore.self) private var sessionStore
    @Environment(FavoritesStore.self) private var favoritesStore
    @Environment(HistoryStore.self) private var historyStore
    @Environment(ThemeStore.self) private var themeStore
    @State private var selection: NavigationSection? = .home
    @State private var provider: ServerAPIClient
    @State private var searchStore: SearchStore
    @State private var playerStore = PlayerStore()
    @State private var doubanProvider = DoubanAPIClient()
    @State private var bangumiProvider = BangumiAPIClient()
    @State private var shortDramaStore: ShortDramaStore
    @State private var bilibiliStore: VideoPlatformStore
    @State private var youtubeStore: VideoPlatformStore
    @State private var metadataProvider: MetadataEnhancementAPIClient?
    @State private var liveStore = LiveStore()
    @State private var liveProvider = LiveServiceClient()
    @State private var isPlaying = false

    init() {
        let placeholderURL = URL(string: "https://example.com")!
        let placeholderProvider = ServerAPIClient(baseURL: placeholderURL)
        let shortDramaProvider = ShortDramaAPIClient(serverURL: placeholderURL)
        let videoProvider = VideoPlatformAPIClient(serverURL: placeholderURL)
        _provider = State(initialValue: placeholderProvider)
        _searchStore = State(initialValue: SearchStore(provider: placeholderProvider))
        _shortDramaStore = State(initialValue: ShortDramaStore(provider: shortDramaProvider))
        _bilibiliStore = State(initialValue: VideoPlatformStore(provider: videoProvider, kind: .bilibili))
        _youtubeStore = State(initialValue: VideoPlatformStore(provider: videoProvider, kind: .youtube))
    }

    @MainActor
    private func playRecord(_ record: PlayRecord) {
        guard sessionStore.session != nil else { return }
        playerStore.currentSourceResults = []
        playerStore.stop()
        isPlaying = true
        Task {
            await playerStore.loadDetailAndPlay(record: record, provider: provider)
        }
    }

    @MainActor
    private func closePlayer() {
        saveCurrentRecord()
        playerStore.stop()
        isPlaying = false
    }

    @MainActor
    private func saveCurrentRecord() {
        guard let record = playerStore.makePlayRecord() else { return }
        Task {
            await historyStore.saveRecord(record, provider: provider)
        }
    }

    var body: some View {
        NavigationSplitView {
            List(selection: $selection) {
                sidebarButton(.home)
                sidebarButton(.search)

                Section("浏览") {
                    sidebarButton(.movie)
                    sidebarButton(.tv)
                    sidebarButton(.anime)
                    sidebarButton(.show)
                    sidebarButton(.shortDrama)
                    sidebarButton(.bilibili)
                    sidebarButton(.youtube)
                    sidebarButton(.live)
                }

                Section("个人") {
                    sidebarButton(.favorites)
                    sidebarButton(.history)
                    sidebarButton(.settings)
                }
            }
            .navigationTitle("Selene")
            .frame(minWidth: 190)
        } detail: {
            contentView
                .navigationTitle(selection?.title ?? "Selene")
        }
        .task(id: sessionStore.session?.id) {
            guard let url = sessionStore.session?.serverURL else { return }
            let newProvider = ServerAPIClient(baseURL: url, cookie: sessionStore.session?.cookie ?? "")
            provider = newProvider
            searchStore = SearchStore(provider: newProvider)
            doubanProvider = DoubanAPIClient(
                backendBaseURL: url,
                backendCookie: sessionStore.session?.cookie ?? ""
            )
            if sessionStore.session?.isLocalMode == true {
                metadataProvider = nil
            } else {
                let cookie = sessionStore.session?.cookie ?? ""
                shortDramaStore = ShortDramaStore(provider: ShortDramaAPIClient(serverURL: url, cookie: cookie))
                let videoProvider = VideoPlatformAPIClient(serverURL: url, cookie: cookie)
                bilibiliStore = VideoPlatformStore(provider: videoProvider, kind: .bilibili)
                youtubeStore = VideoPlatformStore(provider: videoProvider, kind: .youtube)
                metadataProvider = MetadataEnhancementAPIClient(serverURL: url, cookie: cookie)
            }
            if sessionStore.session?.isLocalMode == true {
                liveProvider = LiveServiceClient(localSources: sessionStore.session?.localLiveSources ?? [])
            } else {
                liveProvider = LiveServiceClient(provider: newProvider)
            }
            liveStore = LiveStore()
            await searchStore.loadResources()
            await searchStore.loadHistory()
            await favoritesStore.loadFavorites(provider: newProvider)
            await historyStore.loadRecords(provider: newProvider)
        }
    }

    private func sidebarButton(_ section: NavigationSection) -> some View {
        Label(section.title, systemImage: section.icon)
            .tag(section)
    }

    @ViewBuilder
    private var contentView: some View {
        if isPlaying {
            PlayerScreen(
                playerStore: playerStore,
                onClose: closePlayer
            )
        } else {
            switch selection ?? .search {
            case .home:
                HomeView(
                    historyStore: historyStore,
                    doubanProvider: doubanProvider,
                    bangumiProvider: bangumiProvider,
                    onPlayRecord: playRecord
                )
            case .search:
                SearchResultsView(
                    searchStore: searchStore,
                    playerStore: playerStore,
                    provider: provider,
                    metadataProvider: metadataProvider,
                    favoritesStore: favoritesStore,
                    historyStore: historyStore,
                    session: sessionStore.session
                )
            case .movie:
                CategoryView(category: .movie, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
            case .tv:
                CategoryView(category: .tv, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
            case .anime:
                CategoryView(category: .anime, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
            case .show:
                CategoryView(category: .show, doubanProvider: doubanProvider, bangumiProvider: bangumiProvider)
            case .shortDrama:
                serverFeatureView {
                    ShortDramaView(store: shortDramaStore, onPlayURL: playDirectURL)
                }
            case .bilibili:
                serverFeatureView {
                    VideoPlatformView(store: bilibiliStore, kind: .bilibili, onPlayURL: playDirectURL)
                }
            case .youtube:
                serverFeatureView {
                    VideoPlatformView(store: youtubeStore, kind: .youtube, onPlayURL: playDirectURL)
                }
            case .live:
                LiveScreenView(liveStore: liveStore, provider: liveProvider)
            case .favorites:
                FavoritesView(
                    favoritesStore: favoritesStore,
                    provider: provider,
                    onPlayRecord: playRecord
                )
            case .history:
                HistoryView(
                    historyStore: historyStore,
                    provider: provider,
                    onPlayRecord: playRecord
                )
            case .settings:
                SettingsView(
                    sessionStore: sessionStore,
                    themeStore: themeStore,
                    versionService: VersionService()
                )
            }
        }
    }

    private func placeholder(_ text: String, icon: String) -> some View {
        ContentUnavailableView("即将可用", systemImage: icon, description: Text(text))
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    @MainActor
    private func playDirectURL(_ url: URL) {
        playerStore.currentSourceResults = []
        playerStore.replaceItem(url: url)
        playerStore.play()
        isPlaying = true
    }

    @ViewBuilder
    private func serverFeatureView<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        if sessionStore.session?.isLocalMode == true || sessionStore.session == nil {
            ContentUnavailableView(
                "Server session required",
                systemImage: "person.crop.circle.badge.exclamationmark",
                description: Text("Connect to a LunaTV server to use this source.")
            )
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            content()
        }
    }

}
