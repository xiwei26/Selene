import SwiftUI

struct SearchView: View {
    @Environment(AppModel.self) private var appModel
    @State private var store = SearchStore()
    @State private var showsFilters = false

    var body: some View {
        Group {
            switch store.phase {
            case .idle:
                SearchLandingView(
                    history: appModel.searchHistory,
                    action: submit,
                    deleteAction: { query in
                        Task { await appModel.deleteSearchHistory(query) }
                    },
                    clearAction: {
                        Task { await appModel.clearSearchHistory() }
                    }
                )
            case .loading:
                SearchProgressView(
                    query: store.submittedQuery,
                    results: store.results,
                    completedSources: store.completedSources,
                    totalSources: store.totalSources,
                    currentSource: store.currentSource,
                    warnings: store.sourceWarnings
                )
            case .failed(let message):
                ContentUnavailableView {
                    Label("搜索失败", systemImage: "wifi.exclamationmark")
                } description: {
                    Text(message)
                } actions: {
                    Button("重试") {
                        // Re-trigger the submittedQuery task by bouncing the value.
                        let query = store.submittedQuery
                        store.submittedQuery = ""
                        store.submittedQuery = query
                    }
                    .buttonStyle(.borderedProminent)
                }
            case .loaded:
                if store.results.isEmpty {
                    ContentUnavailableView.search(text: store.submittedQuery)
                } else if store.filteredResults.isEmpty {
                    ContentUnavailableView(
                        "筛选后没有结果",
                        systemImage: "line.3.horizontal.decrease.circle",
                        description: Text("调整来源、年份或屏蔽关键词。")
                    )
                } else {
                    resultList
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .searchable(
            text: $store.query,
            placement: .navigationBarDrawer(displayMode: .always),
            prompt: "搜索电影、剧集或动漫"
        )
        .onSubmit(of: .search) {
            store.submit()
        }
        .searchSuggestions {
            ForEach(store.suggestions) { suggestion in
                Button(suggestion.text) {
                    store.query = suggestion.text
                    store.submit()
                }
            }
        }
        .task(id: store.query) {
            guard let client = appModel.client else { return }
            await store.loadSuggestions(using: client, isLocalMode: appModel.isLocalMode)
        }
        .task(id: store.submittedQuery) {
            guard let client = appModel.client, !store.submittedQuery.isEmpty else { return }
            await store.search(
                using: client,
                useStreaming: !appModel.isLocalMode,
                isLocalMode: appModel.isLocalMode,
                localResources: appModel.session?.localSearchResources ?? []
            )
            if !appModel.searchHistory.contains(store.submittedQuery) {
                appModel.searchHistory.insert(store.submittedQuery, at: 0)
            }
        }
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showsFilters = true
                } label: {
                    Label("筛选搜索结果", systemImage: "line.3.horizontal.decrease.circle")
                }
                .disabled(store.results.isEmpty)
            }
        }
        .sheet(isPresented: $showsFilters) {
            SearchFilterSheet(store: store)
        }
    }

    private func submit(_ query: String) {
        store.query = query
        store.submit()
    }

    @ViewBuilder
    private var resultList: some View {
        VStack(spacing: 0) {
            SourceWarningsView(warnings: store.sourceWarnings)
            results
        }
    }

    @ViewBuilder
    private var results: some View {
        if store.isAggregating {
            List(store.filteredAggregates) { aggregate in
                NavigationLink(value: aggregate.representative) {
                    VStack(alignment: .leading, spacing: 5) {
                        SearchResultRow(result: aggregate.representative)
                        Text("\(aggregate.sourceNames.count) 个来源 · \(aggregate.sourceNames.joined(separator: " / "))")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
                .listRowBackground(Color.clear)
            }
            .listStyle(.plain)
            .scrollContentBackground(.hidden)
        } else {
            List(store.filteredResults, id: \.uniqueKey) { result in
                NavigationLink(value: result) {
                    SearchResultRow(result: result)
                }
                .listRowBackground(Color.clear)
            }
            .listStyle(.plain)
            .scrollContentBackground(.hidden)
        }
    }
}

/// Per-source SSE failures are recoverable: other sources still returned
/// results, so this stays collapsed and secondary rather than interrupting.
private struct SourceWarningsView: View {
    let warnings: [String]

    var body: some View {
        if !warnings.isEmpty {
            DisclosureGroup("\(warnings.count) 个片源未返回结果") {
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(warnings, id: \.self) { warning in
                        Text(warning)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.top, 4)
            }
            .font(.caption)
            .foregroundStyle(.secondary)
            .padding(.horizontal)
            .padding(.vertical, 8)
        }
    }
}

private struct SearchFilterSheet: View {
    @Environment(\.dismiss) private var dismiss
    @Bindable var store: SearchStore

    var body: some View {
        NavigationStack {
            Form {
                Section("显示") {
                    Toggle("合并同名内容", isOn: $store.isAggregating)
                    Toggle("年份从新到旧", isOn: $store.sortNewestFirst)
                }

                Section("来源与年份") {
                    Picker("来源", selection: $store.sourceFilter) {
                        Text("全部来源").tag(String?.none)
                        ForEach(store.availableSources, id: \.self) { source in
                            Text(source).tag(Optional(source))
                        }
                    }
                    Picker("年份", selection: $store.yearFilter) {
                        Text("全部年份").tag(String?.none)
                        ForEach(store.availableYears, id: \.self) { year in
                            Text(year).tag(Optional(year))
                        }
                    }
                }

                Section {
                    TextField("屏蔽关键词", text: $store.blockedKeywordsText)
                } header: {
                    Text("内容过滤")
                } footer: {
                    Text("匹配标题、简介、来源或类型；多个关键词使用英文逗号分隔。")
                }

                Button("清除筛选", role: .destructive) {
                    store.clearFilters()
                }
            }
            .navigationTitle("搜索筛选")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("完成") { dismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}

private struct SearchProgressView: View {
    let query: String
    let results: [SearchResult]
    let completedSources: Int
    let totalSources: Int
    let currentSource: String?
    let warnings: [String]

    var body: some View {
        VStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 8) {
                Text("正在搜索“\(query)”")
                    .font(.headline)
                if totalSources > 0 {
                    ProgressView(value: Double(completedSources), total: Double(totalSources))
                    Text("\(completedSources) / \(totalSources) 个片源\(sourceSuffix)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                } else {
                    ProgressView()
                }
            }
            .padding(.horizontal)

            SourceWarningsView(warnings: warnings)

            if !results.isEmpty {
                List(results, id: \.uniqueKey) { result in
                    NavigationLink(value: result) {
                        SearchResultRow(result: result)
                    }
                    .listRowBackground(Color.clear)
                }
                .listStyle(.plain)
                .scrollContentBackground(.hidden)
            }
        }
        .padding(.top)
    }

    private var sourceSuffix: String {
        guard let currentSource, !currentSource.isEmpty else { return "" }
        return " · \(currentSource)"
    }
}

private struct SearchLandingView: View {
    let history: [String]
    let action: (String) -> Void
    let deleteAction: (String) -> Void
    let clearAction: () -> Void

    var body: some View {
        VStack(spacing: 20) {
            ContentUnavailableView(
                "搜索片名",
                systemImage: "magnifyingglass",
                description: Text("输入片名后按下搜索，Selene 会聚合 LunaTV 的可播放资源。")
            )
            if !history.isEmpty {
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Text("最近搜索")
                            .font(.headline)
                        Spacer()
                        Button("清空", role: .destructive, action: clearAction)
                            .font(.caption)
                    }
                    FlowLayout(spacing: 8) {
                        ForEach(history.prefix(10), id: \.self) { query in
                            Button(query) { action(query) }
                                .buttonStyle(.bordered)
                                .contextMenu {
                                    Button("删除", role: .destructive) {
                                        deleteAction(query)
                                    }
                                }
                        }
                    }
                }
                .frame(maxWidth: 680, alignment: .leading)
                .appSurface()
            }
        }
        .padding()
    }
}

private struct FlowLayout: Layout {
    let spacing: CGFloat

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let width = proposal.width ?? 600
        var x: CGFloat = 0
        var y: CGFloat = 0
        var rowHeight: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x + size.width > width, x > 0 {
                x = 0
                y += rowHeight + spacing
                rowHeight = 0
            }
            x += size.width + spacing
            rowHeight = max(rowHeight, size.height)
        }
        return CGSize(width: width, height: y + rowHeight)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var x = bounds.minX
        var y = bounds.minY
        var rowHeight: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x + size.width > bounds.maxX, x > bounds.minX {
                x = bounds.minX
                y += rowHeight + spacing
                rowHeight = 0
            }
            subview.place(at: CGPoint(x: x, y: y), proposal: ProposedViewSize(size))
            x += size.width + spacing
            rowHeight = max(rowHeight, size.height)
        }
    }
}