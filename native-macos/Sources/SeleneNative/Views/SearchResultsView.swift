import SwiftUI

struct SearchResultsView: View {
    @Bindable var searchStore: SearchStore
    let playerStore: PlayerStore
    let provider: ContentProvider
    let favoritesStore: FavoritesStore
    let historyStore: HistoryStore
    let session: LoginSession?

    var body: some View {
        HSplitView {
            VStack(spacing: 0) {
                searchHeader
                Divider()
                resultsList
            }
            .frame(minWidth: 360)

            VStack(spacing: 0) {
                if let result = searchStore.selectedResult {
                    DetailView(
                        result: result,
                        isFavorited: favoritesStore.isFavorited(source: result.source, id: result.id),
                        onToggleFavorite: { result in
                            Task {
                                await favoritesStore.toggleFavorite(
                                    source: result.source,
                                    id: result.id,
                                    data: favoriteData(for: result),
                                    provider: provider
                                )
                            }
                        },
                        onPlay: { selectedResult, index, url in
                            playerStore.currentSourceResults = sourceResults(for: selectedResult)
                            playerStore.replaceItem(url: url, result: selectedResult, index: index)
                            playerStore.play()
                            saveCurrentRecord()
                        }
                    )
                    .frame(minWidth: 360)
                } else {
                    ContentUnavailableView {
                        Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }

                if playerStore.currentEpisodeURL != nil {
                    Divider()
                    PlayerView(playerStore: playerStore)
                        .frame(minHeight: 300)
                }
            }
        }
        .task {
            await searchStore.loadResources()
            await searchStore.loadHistory()
        }
        .onChange(of: playerStore.playTime) {
            guard playerStore.playTime > 0 else { return }
            saveCurrentRecord()
        }
    }

    private var searchHeader: some View {
        VStack(alignment: .leading, spacing: 10) {
            ZStack(alignment: .topLeading) {
                HStack {
                    TextField("搜索电影、剧集、综艺...", text: $searchStore.query)
                        .textFieldStyle(.roundedBorder)
                        .onSubmit {
                            Task { await searchStore.searchWithSSE(session: session) }
                        }
                        .onChange(of: searchStore.query) {
                            Task { await searchStore.loadSuggestions() }
                        }

                    Button {
                        Task { await searchStore.searchWithSSE(session: session) }
                    } label: {
                        Label(searchStore.isLoading ? "搜索中" : "搜索", systemImage: "magnifyingglass")
                    }
                    .disabled(searchStore.isLoading || searchStore.query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }

                SearchSuggestionOverlay(suggestions: searchStore.suggestions) { suggestion in
                    searchStore.query = suggestion.text
                    searchStore.suggestions = []
                    Task { await searchStore.searchWithSSE(session: session) }
                }
                .padding(.top, 34)
                .frame(maxWidth: 360)
                .zIndex(2)
            }

            if searchStore.isLoading {
                ProgressView(value: searchStore.sseProgress.progressPercentage)
            }

            filterBar
            TextField("过滤关键词，用逗号分隔", text: $searchStore.blockedKeywordsText)
                .textFieldStyle(.roundedBorder)
                .font(.caption)
            historyBar
        }
        .padding()
    }

    private var filterBar: some View {
        HStack(spacing: 8) {
            Toggle("聚合", isOn: $searchStore.isAggregating)
                .toggleStyle(.switch)

            Menu(searchStore.sourceFilter ?? "来源") {
                Button("全部") { searchStore.sourceFilter = nil }
                ForEach(searchStore.availableSources, id: \.self) { source in
                    Button(source) { searchStore.sourceFilter = source }
                }
            }

            Menu(searchStore.yearFilter ?? "年份") {
                Button("全部") { searchStore.yearFilter = nil }
                ForEach(searchStore.availableYears, id: \.self) { year in
                    Button(year) { searchStore.yearFilter = year }
                }
            }

            Button {
                searchStore.sortNewestFirst.toggle()
            } label: {
                Label(searchStore.sortNewestFirst ? "新到旧" : "旧到新", systemImage: "arrow.up.arrow.down")
            }
            .labelStyle(.iconOnly)
            .help(searchStore.sortNewestFirst ? "年份新到旧" : "年份旧到新")

            Button {
                searchStore.clearFilters()
            } label: {
                Label("清除筛选", systemImage: "xmark.circle")
            }
            .labelStyle(.iconOnly)
            .help("清除筛选")
        }
        .font(.caption)
    }

    private var historyBar: some View {
        ScrollView(.horizontal) {
            HStack(spacing: 8) {
                ForEach(searchStore.searchHistory.prefix(8), id: \.self) { query in
                    Button {
                        searchStore.query = query
                        Task { await searchStore.searchWithSSE(session: session) }
                    } label: {
                        Label(query, systemImage: "clock")
                            .font(.caption)
                    }
                    .buttonStyle(.borderless)
                }
            }
        }
    }

    private var resultsList: some View {
        Group {
            if searchStore.isLoading && searchStore.results.isEmpty {
                ProgressView("搜索中...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let error = searchStore.errorMessage {
                VStack(spacing: 12) {
                    Text("出错了")
                        .font(.headline)
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Button("重试") {
                        Task { await searchStore.searchWithSSE(session: session) }
                    }
                }
                .padding()
            } else if searchStore.results.isEmpty && !searchStore.query.isEmpty {
                ContentUnavailableView("无结果", systemImage: "magnifyingglass", description: Text("尝试其他关键词"))
            } else if searchStore.results.isEmpty {
                ContentUnavailableView("开始搜索", systemImage: "film", description: Text("在服务器上搜索视频内容"))
            } else if searchStore.isAggregating {
                List(searchStore.filteredAggregatedResults) { aggregate in
                    VideoCardView(
                        title: aggregate.title,
                        poster: aggregate.cover,
                        sourceName: aggregate.sourceNames.joined(separator: " / "),
                        year: aggregate.year,
                        subtitle: "\(aggregate.sourceNames.count) 个来源"
                    )
                    .contentShape(Rectangle())
                    .onTapGesture { searchStore.selectAggregate(aggregate) }
                }
            } else {
                List(searchStore.filteredResults, selection: $searchStore.selectedResult) { result in
                    VideoCardView(
                        title: result.title,
                        poster: result.poster,
                        sourceName: result.sourceName.isEmpty ? result.source : result.sourceName,
                        year: result.year,
                        subtitle: result.episodes.isEmpty ? nil : "共\(result.episodes.count)集"
                    )
                    .contentShape(Rectangle())
                    .onTapGesture { searchStore.selectResult(result) }
                }
            }
        }
    }

    private func sourceResults(for result: SearchResult) -> [SearchResult] {
        searchStore.results.filter { candidate in
            candidate.title == result.title && candidate.year == result.year
        }
    }

    private func favoriteData(for result: SearchResult) -> [String: Any] {
        [
            "title": result.title,
            "source_name": result.sourceName,
            "year": result.year,
            "cover": result.poster,
            "total_episodes": result.episodes.count,
            "save_time": Int64(Date().timeIntervalSince1970 * 1000)
        ]
    }

    private func saveCurrentRecord() {
        guard let record = playerStore.makePlayRecord() else { return }
        Task {
            await historyStore.saveRecord(record, provider: provider)
        }
    }
}
