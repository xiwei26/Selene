import SwiftUI

enum PlatformFeature: String, Hashable {
    case shortDrama
    case bilibili
    case youtube

    var title: String {
        switch self {
        case .shortDrama: "短剧"
        case .bilibili: "Bilibili"
        case .youtube: "YouTube"
        }
    }

    var subtitle: String {
        switch self {
        case .shortDrama: "推荐、搜索并播放 LunaTV 短剧内容"
        case .bilibili: "浏览和搜索 Bilibili 视频"
        case .youtube: "浏览和搜索 YouTube 视频"
        }
    }

    var icon: String {
        switch self {
        case .shortDrama: "play.square.stack"
        case .bilibili: "tv"
        case .youtube: "play.rectangle"
        }
    }
}

struct PlatformFeatureView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var playerCoordinator
    @Environment(\.openURL) private var openURL

    let feature: PlatformFeature

    @State private var query = ""
    @State private var submittedQuery = ""
    @State private var dramas: [SearchResult] = []
    @State private var platformItems: [MediaPlatformItem] = []
    @State private var phase: LoadPhase = .idle
    @State private var openTask: Task<Void, Never>?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 22) {
                PageHeader(title: feature.title, subtitle: feature.subtitle, systemImage: feature.icon)
                searchBar
                FeatureContent(
                    feature: feature,
                    dramas: dramas,
                    platformItems: platformItems,
                    phase: phase,
                    dramaAction: openDrama,
                    platformAction: openPlatformItem,
                    retryAction: {
                        Task {
                            if submittedQuery.isEmpty {
                                await loadInitial()
                            } else {
                                await runSearch()
                            }
                        }
                    }
                )
            }
            .padding(AppTheme.pagePadding)
            .frame(maxWidth: 1_300, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        .task(id: feature.rawValue) {
            await loadInitial()
        }
        // Driving the search from the submitted query lets SwiftUI cancel the
        // previous one, so a slow response cannot overwrite a newer result.
        .task(id: submittedQuery) {
            await runSearch()
        }
        .onDisappear {
            openTask?.cancel()
        }
    }

    private var searchBar: some View {
        HStack {
            TextField("搜索\(feature.title)", text: $query)
                .textFieldStyle(.roundedBorder)
                .onSubmit(submit)
            Button("搜索", systemImage: "magnifyingglass", action: submit)
                .buttonStyle(.borderedProminent)
                .disabled(query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || phase.isLoading)
        }
        .appSurface()
    }

    private func loadInitial() async {
        guard let client = appModel.client, appModel.session?.isLocalMode != true else {
            phase = .failed("此功能需要连接 LunaTV 服务器")
            return
        }
        phase = .loading
        do {
            switch feature {
            case .shortDrama:
                dramas = try await client.recommendedShortDramas()
            case .bilibili:
                platformItems = try await client.bilibiliPopular()
            case .youtube:
                platformItems = try await client.youtubePopular()
            }
            phase = .loaded
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }

    private func submit() {
        let text = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        submittedQuery = text
    }

    private func runSearch() async {
        guard !submittedQuery.isEmpty, let client = appModel.client else { return }
        let text = submittedQuery
        phase = .loading
        do {
            switch feature {
            case .shortDrama:
                dramas = try await client.searchShortDramas(text)
            case .bilibili:
                platformItems = try await client.searchBilibili(text)
            case .youtube:
                platformItems = try await client.searchYouTube(text)
            }
            phase = .loaded
        } catch {
            // A cancelled task has been superseded; its state is stale.
            guard !Task.isCancelled else { return }
            phase = .failed(error.localizedDescription)
        }
    }

    private func openDrama(_ drama: SearchResult) {
        guard let client = appModel.client else { return }
        openTask?.cancel()
        openTask = Task {
            do {
                let detail = try await client.shortDramaDetail(id: drama.id, name: drama.title) ?? drama
                guard !Task.isCancelled else { return }
                playerCoordinator.sourceResults = [detail]
                playerCoordinator.open(result: detail)
            } catch {
                guard !Task.isCancelled else { return }
                phase = .failed(error.localizedDescription)
            }
        }
    }

    private func openPlatformItem(_ item: MediaPlatformItem) {
        if let url = URL(string: item.url) {
            openURL(url)
        }
    }
}

private struct FeatureContent: View {
    let feature: PlatformFeature
    let dramas: [SearchResult]
    let platformItems: [MediaPlatformItem]
    let phase: LoadPhase
    let dramaAction: (SearchResult) -> Void
    let platformAction: (MediaPlatformItem) -> Void
    let retryAction: () -> Void

    var body: some View {
        switch phase {
        case .idle, .loading:
            ProgressView("正在加载\(feature.title)…")
                .frame(maxWidth: .infinity, minHeight: 240)
        case .failed(let message):
            ContentUnavailableView {
                Label("加载失败", systemImage: "exclamationmark.triangle")
            } description: {
                Text(message)
            } actions: {
                Button("重试", action: retryAction)
                    .buttonStyle(.borderedProminent)
            }
            .frame(maxWidth: .infinity, minHeight: 240)
        case .loaded:
            if feature == .shortDrama {
                dramaGrid
            } else {
                platformGrid
            }
        }
    }

    private var dramaGrid: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 145), spacing: 16)], spacing: 20) {
            ForEach(dramas, id: \.uniqueKey) { drama in
                Button {
                    dramaAction(drama)
                } label: {
                    VStack(alignment: .leading, spacing: 8) {
                        RemotePosterView(urlString: drama.poster)
                        Text(drama.title)
                            .font(.subheadline.weight(.semibold))
                            .lineLimit(2)
                        Text(drama.description ?? "短剧")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(2)
                    }
                }
                .buttonStyle(.plain)
            }
        }
    }

    private var platformGrid: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 240, maximum: 360), spacing: 16)], spacing: 18) {
            ForEach(platformItems) { item in
                Button {
                    platformAction(item)
                } label: {
                    VStack(alignment: .leading, spacing: 9) {
                        AsyncImage(url: URL(string: item.cover)) { image in
                            image.resizable().scaledToFill()
                        } placeholder: {
                            Rectangle().fill(Color.secondary.opacity(0.1))
                        }
                        .frame(height: 145)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        Text(item.title)
                            .font(.headline)
                            .foregroundStyle(.primary)
                            .lineLimit(2)
                        Text(item.author.isEmpty ? item.source : item.author)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .appSurface()
                }
                .buttonStyle(.plain)
            }
        }
    }
}