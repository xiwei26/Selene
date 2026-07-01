import SwiftUI
import AppKit

struct LunaFeatureView: View {
    enum Kind: String {
        case shortDrama, bilibili, youtube

        var title: String {
            switch self {
            case .shortDrama: return "短剧"
            case .bilibili: return "Bilibili"
            case .youtube: return "YouTube"
            }
        }

        var subtitle: String {
            switch self {
            case .shortDrama: return "推荐、搜索、解析播放 LunaTV 短剧内容。"
            case .bilibili: return "浏览和搜索 Bilibili 视频。"
            case .youtube: return "浏览和搜索 YouTube 视频。"
            }
        }

        var icon: String {
            switch self {
            case .shortDrama: return "play.square.stack"
            case .bilibili: return "tv"
            case .youtube: return "play.rectangle"
            }
        }
    }

    let kind: Kind
    let provider: ServerAPIClient
    let onShortDramaDetail: (SearchResult) -> Void

    @State private var searchText = ""
    @State private var shortDramas: [SearchResult] = []
    @State private var platformItems: [MediaPlatformItem] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var featureDisabled = false

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(title: kind.title, subtitle: kind.subtitle, systemImage: kind.icon)
                searchRow

                if isLoading {
                    ProgressView()
                        .controlSize(.large)
                        .frame(maxWidth: .infinity, minHeight: 180)
                } else if featureDisabled {
                    disabledPanel
                } else if let errorMessage {
                    ContentUnavailableView("加载失败", systemImage: "exclamationmark.triangle", description: Text(errorMessage))
                        .frame(maxWidth: .infinity, minHeight: 240)
                } else if kind == .shortDrama {
                    shortDramaGrid
                } else {
                    platformGrid
                }
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
        .task(id: kind.rawValue) {
            await loadInitial()
        }
    }

    private var searchRow: some View {
        HStack(spacing: 10) {
            TextField(kind == .shortDrama ? "搜索短剧" : "搜索 \(kind.title)", text: $searchText)
                .textFieldStyle(.roundedBorder)
                .onSubmit { Task { await search() } }

            Button {
                Task { await search() }
            } label: {
                Label("搜索", systemImage: "magnifyingglass")
            }
            .disabled(searchText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || isLoading)
        }
        .appSurface()
    }

    private var disabledPanel: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("功能未启用", systemImage: "lock.trianglebadge.exclamationmark")
                .font(.headline)
            Text("请在 LunaTV 管理后台开启 \(kind.title) 功能。\(errorMessage ?? "")")
                .foregroundStyle(.secondary)
            Button {
                openAdminPanel()
            } label: {
                Label("打开管理后台", systemImage: "gearshape")
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .appSurface()
    }

    private var shortDramaGrid: some View {
        Group {
            if shortDramas.isEmpty {
                ContentUnavailableView("暂无短剧", systemImage: "play.square.stack", description: Text("后端没有返回短剧内容"))
                    .frame(maxWidth: .infinity, minHeight: 240)
            } else {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 160), spacing: 14)], spacing: 14) {
                    ForEach(shortDramas) { item in
                        Button {
                            Task { await openShortDrama(item) }
                        } label: {
                            PosterTile(title: item.title, poster: item.poster, subtitle: item.description ?? "短剧")
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }

    private var platformGrid: some View {
        Group {
            if platformItems.isEmpty {
                ContentUnavailableView("暂无内容", systemImage: kind.icon, description: Text("后端没有返回平台视频"))
                    .frame(maxWidth: .infinity, minHeight: 240)
            } else {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 220), spacing: 14)], spacing: 14) {
                    ForEach(platformItems) { item in
                        Button {
                            if let url = URL(string: item.url) {
                                NSWorkspace.shared.open(url)
                            }
                        } label: {
                            LandscapeTile(item: item)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }

    private func loadInitial() async {
        await runLoading {
            switch kind {
            case .shortDrama:
                shortDramas = try await provider.getRecommendedShortDramas()
                platformItems = []
            case .bilibili:
                platformItems = try await provider.getBilibiliPopular()
                shortDramas = []
            case .youtube:
                platformItems = try await provider.getYouTubePopular(regionCode: "US")
                shortDramas = []
            }
        }
    }

    private func search() async {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return }
        await runLoading {
            switch kind {
            case .shortDrama:
                shortDramas = try await provider.searchShortDramas(query: query)
                platformItems = []
            case .bilibili:
                platformItems = try await provider.searchBilibili(query: query)
                shortDramas = []
            case .youtube:
                platformItems = try await provider.searchYouTube(query: query)
                shortDramas = []
            }
        }
    }

    private func runLoading(_ operation: () async throws -> Void) async {
        isLoading = true
        errorMessage = nil
        featureDisabled = false
        defer { isLoading = false }
        do {
            try await operation()
        } catch let error as APIError where error.isFeatureDisabled {
            errorMessage = error.localizedDescription
            featureDisabled = true
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func openShortDrama(_ item: SearchResult) async {
        do {
            if let detail = try await provider.getShortDramaDetail(id: item.id, name: item.title) {
                onShortDramaDetail(detail)
            }
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func openAdminPanel() {
        NSWorkspace.shared.open(provider.baseURL.appendingPathComponent("admin"))
    }
}

private struct PosterTile: View {
    let title: String
    let poster: String
    let subtitle: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            AsyncPoster(urlString: poster)
                .frame(height: 224)
                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))

            Text(title)
                .font(.callout.weight(.semibold))
                .lineLimit(2)

            if let subtitle, !subtitle.isEmpty {
                Text(subtitle)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .appSurface()
    }
}

private struct LandscapeTile: View {
    let item: MediaPlatformItem

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            AsyncPoster(urlString: item.cover, systemImage: "play.rectangle")
                .frame(height: 124)
                .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))

            Text(item.title)
                .font(.callout.weight(.semibold))
                .lineLimit(2)

            Text(item.author.isEmpty ? item.source : item.author)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)

            if !item.description.isEmpty {
                Text(item.description)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .appSurface()
    }
}

private struct AsyncPoster: View {
    let urlString: String
    var systemImage = "film"

    var body: some View {
        ZStack {
            AppTheme.surface
            if let url = URL(string: urlString), !urlString.isEmpty {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFit()
                    default:
                        placeholder
                    }
                }
            } else {
                placeholder
            }
        }
    }

    private var placeholder: some View {
        Image(systemName: systemImage)
            .font(.largeTitle)
            .foregroundStyle(.secondary)
    }
}
