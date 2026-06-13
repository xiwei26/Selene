import SwiftUI

struct MainView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var provider: ServerAPIClient
    @State private var searchStore: SearchStore
    @State private var playerStore: PlayerStore

    init() {
        // Initialize with a placeholder; will be replaced in .task
        let placeholderURL = URL(string: "https://example.com")!
        _provider = State(initialValue: ServerAPIClient(baseURL: placeholderURL))
        _searchStore = State(initialValue: SearchStore(provider: ServerAPIClient(baseURL: placeholderURL)))
        _playerStore = State(initialValue: PlayerStore())
    }

    var body: some View {
        VStack(spacing: 0) {
            // Top toolbar
            HStack {
                Text("Selene")
                    .font(.headline)

                Button("退出登录", role: .destructive) {
                    sessionStore.logout()
                }
                Spacer()
            }
            .padding(.horizontal)
            .padding(.vertical, 8)

            Divider()

            // Content area
            HSplitView {
                // Left: Search + Results
                VStack(spacing: 0) {
                    searchBar
                    Divider()
                    resultsList
                }
                .frame(minWidth: 320)

                // Right: Detail + Player
                VStack(spacing: 0) {
                    if let result = searchStore.selectedResult {
                        DetailView(
                            result: result,
                            onPlay: { url in
                                playerStore.replaceItem(url: url)
                                playerStore.play()
                            }
                        )
                    } else {
                        ContentUnavailableView {
                            Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }

                    if playerStore.currentEpisodeURL != nil {
                        Divider()
                        PlayerView(playerStore: playerStore)
                            .frame(height: 200)
                    }
                }
            }

            if searchStore.isLoading {
                Divider()
                ProgressView("搜索中...")
                    .padding(.vertical, 4)
            }
        }
        .navigationTitle("Selene")
        .task {
            // Initialize with actual session URL
            if let url = sessionStore.session?.serverURL {
                let newProvider = ServerAPIClient(baseURL: url)
                provider = newProvider
                searchStore = SearchStore(provider: newProvider)
                await searchStore.loadResources()
            }
        }
    }

    private var searchBar: some View {
        HStack {
            TextField("搜索...", text: $searchStore.query)
                .textFieldStyle(.roundedBorder)
                .onSubmit {
                    Task { await searchStore.search() }
                }

            Button(searchStore.isLoading ? "搜索中..." : "搜索") {
                Task { await searchStore.search() }
            }
            .disabled(searchStore.isLoading || searchStore.query.isEmpty)
        }
        .padding()
    }

    private var resultsList: some View {
        Group {
            if searchStore.results.isEmpty && searchStore.query.isEmpty {
                ContentUnavailableView(
                    "开始搜索",
                    systemImage: "film",
                    description: Text("在服务器上搜索视频内容")
                )
            } else if searchStore.results.isEmpty {
                ContentUnavailableView(
                    "无结果",
                    systemImage: "magnifyingglass",
                    description: Text("尝试其他关键词")
                )
            } else {
                List(searchStore.results, selection: $searchStore.selectedResult) { result in
                    SearchResultRow(result: result)
                        .onTapGesture {
                            searchStore.selectResult(result)
                        }
                }
            }
        }
    }
}
