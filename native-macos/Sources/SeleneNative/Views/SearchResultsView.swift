import SwiftUI

struct SearchResultsView: View {
    @State private var searchStore: SearchStore
    @State private var playerStore: PlayerStore
    private let provider: ContentProvider

    init(provider: ContentProvider) {
        self.provider = provider
        _searchStore = State(initialValue: SearchStore(provider: provider))
        _playerStore = State(initialValue: PlayerStore())
    }

    var body: some View {
        HSplitView {
            // Left panel: Search + Results list
            VStack(spacing: 0) {
                searchBar
                resultsList
            }
            .frame(minWidth: 320)

            // Right panel: Detail
            if let result = searchStore.selectedResult {
                DetailView(
                    result: result,
                    onPlay: { url in
                        playerStore.replaceItem(url: url)
                        playerStore.play()
                    }
                )
                .frame(minWidth: 300)
            } else {
                emptyDetailPlaceholder
                    .frame(minWidth: 300)
            }
        }
        .task {
            await searchStore.loadResources()
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
            if searchStore.isLoading && searchStore.results.isEmpty {
                ProgressView("搜索中...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if searchStore.results.isEmpty && !searchStore.query.isEmpty {
                ContentUnavailableView(
                    "无结果",
                    systemImage: "magnifyingglass",
                    description: Text("尝试其他关键词")
                )
            } else if let error = searchStore.errorMessage {
                VStack {
                    Text("出错了")
                        .font(.headline)
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Button("重试") {
                        Task { await searchStore.search() }
                    }
                }
                .padding()
            } else if searchStore.results.isEmpty {
                ContentUnavailableView(
                    "开始搜索",
                    systemImage: "film",
                    description: Text("在服务器上搜索视频内容")
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

    private var emptyDetailPlaceholder: some View {
        ContentUnavailableView {
            Label("选择一个结果查看详情", systemImage: "doc.text.magnifyingglass")
        }
    }
}

struct SearchResultRow: View {
    let result: SearchResult

    var body: some View {
        HStack(spacing: 12) {
            if !result.poster.isEmpty, let url = URL(string: result.poster) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .empty:
                        Color.gray.opacity(0.3)
                            .frame(width: 60, height: 80)
                            .cornerRadius(4)
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                            .frame(width: 60, height: 80)
                            .clipped()
                            .cornerRadius(4)
                    case .failure:
                        Color.gray.opacity(0.3)
                            .frame(width: 60, height: 80)
                            .cornerRadius(4)
                    @unknown default:
                        EmptyView()
                    }
                }
            } else {
                Color.gray.opacity(0.3)
                    .frame(width: 60, height: 80)
                    .cornerRadius(4)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text(result.title)
                    .font(.body)
                    .lineLimit(1)
                Text(result.sourceName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                HStack(spacing: 8) {
                    Text(result.year)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                    if !result.episodes.isEmpty {
                        Text("共\(result.episodes.count)集")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .padding(.vertical, 4)
    }
}
