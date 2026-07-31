import SwiftUI

struct MediaDetailView: View {
    @Environment(AppModel.self) private var appModel
    @State private var store = PlayableResourceStore()
    @State private var detailStore = MediaDetailStore()
    @State private var showsPoster = false

    let movie: DoubanMovie

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 28) {
                hero
                detailInformation

                switch store.phase {
                case .idle:
                    Label(
                        "选择“查找播放资源”后，会通过 LunaTV 搜索可用片源。",
                        systemImage: "info.circle"
                    )
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .appSurface()
                case .loading:
                    ProgressView("正在聚合可播放资源…")
                        .frame(maxWidth: .infinity, minHeight: 140)
                        .appSurface()
                case .failed(let message):
                    ContentUnavailableView(
                        "资源搜索失败",
                        systemImage: "wifi.exclamationmark",
                        description: Text(message)
                    )
                    .appSurface()
                case .loaded:
                    resourceList
                }
            }
            .padding(AppTheme.pagePadding)
            .frame(maxWidth: 1000, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        .background(AppBackground())
        .navigationTitle(movie.title)
        .navigationBarTitleDisplayMode(.inline)
        .task(id: movie.id) {
            guard let client = appModel.client else { return }
            await detailStore.load(id: movie.id, using: client)
        }
        .fullScreenCover(isPresented: $showsPoster) {
            PosterViewer(urlString: posterURL)
        }
    }

    private var hero: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 28) {
                poster
                    .frame(width: 220)
                metadata
            }

            VStack(alignment: .leading, spacing: 20) {
                poster
                    .frame(maxWidth: 220)
                metadata
            }
        }
    }

    private var poster: some View {
        Button {
            showsPoster = true
        } label: {
            RemotePosterView(urlString: posterURL)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("查看海报大图")
    }

    private var posterURL: String {
        detailStore.details?.poster.nonEmpty ?? movie.poster
    }

    private var metadata: some View {
        VStack(alignment: .leading, spacing: 15) {
            Text(detailStore.details?.title.nonEmpty ?? movie.title)
                .font(.largeTitle.bold())

            HStack(spacing: 12) {
                if let rate = detailStore.details?.rate ?? movie.rate, !rate.isEmpty {
                    Label(rate, systemImage: "star.fill")
                        .foregroundStyle(.orange)
                }
                let year = detailStore.details?.year.nonEmpty ?? movie.year
                if !year.isEmpty {
                    Label(year, systemImage: "calendar")
                }
                if let duration = detailStore.details?.duration, !duration.isEmpty {
                    Label(duration, systemImage: "clock")
                }
            }
            .font(.subheadline.weight(.medium))

            if let genres = detailStore.details?.genres, !genres.isEmpty {
                Text(genres.joined(separator: " · "))
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(.secondary)
            }

            Button {
                Task {
                    guard let client = appModel.client else { return }
                    await store.find(title: movie.title, using: client)
                }
            } label: {
                Label("查找播放资源", systemImage: "play.circle.fill")
                    .frame(maxWidth: 260)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(store.phase.isLoading)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var detailInformation: some View {
        if let details = detailStore.details {
            VStack(alignment: .leading, spacing: 14) {
                if let summary = details.summary, !summary.isEmpty {
                    Text("简介")
                        .font(.title2.bold())
                    Text(summary)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }

                if !details.directors.isEmpty {
                    metadataLine("导演", details.directors)
                }
                if !details.screenwriters.isEmpty {
                    metadataLine("编剧", details.screenwriters)
                }
                if !details.actors.isEmpty {
                    metadataLine("主演", Array(details.actors.prefix(12)))
                }
                if !details.countries.isEmpty {
                    metadataLine("制片地区", details.countries)
                }
                if !details.languages.isEmpty {
                    metadataLine("语言", details.languages)
                }
                if let releaseDate = details.releaseDate, !releaseDate.isEmpty {
                    Text("上映日期：\(releaseDate)")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
            }
            .appSurface()

            if !details.recommends.isEmpty {
                VStack(alignment: .leading, spacing: 12) {
                    Text("相关推荐")
                        .font(.title2.bold())
                    ScrollView(.horizontal) {
                        LazyHStack(spacing: 14) {
                            ForEach(details.recommends) { item in
                                NavigationLink(
                                    value: DoubanMovie(
                                        id: item.id,
                                        title: item.title,
                                        poster: item.poster,
                                        rate: item.rate,
                                        year: ""
                                    )
                                ) {
                                    VStack(alignment: .leading, spacing: 7) {
                                        RemotePosterView(urlString: item.poster)
                                            .frame(width: 128, height: 180)
                                        Text(item.title)
                                            .font(.subheadline.weight(.semibold))
                                            .lineLimit(2)
                                            .frame(width: 128, alignment: .leading)
                                        if let rate = item.rate, !rate.isEmpty {
                                            Label(rate, systemImage: "star.fill")
                                                .font(.caption)
                                                .foregroundStyle(.orange)
                                        }
                                    }
                                }
                                .buttonStyle(.plain)
                            }
                        }
                    }
                    .scrollIndicators(.hidden)
                }
            }
        } else if detailStore.phase.isLoading {
            ProgressView("正在加载豆瓣详情…")
                .frame(maxWidth: .infinity, minHeight: 80)
                .appSurface()
        }
    }

    private func metadataLine(_ title: String, _ values: [String]) -> some View {
        Text("\(title)：\(values.joined(separator: " / "))")
            .font(.callout)
            .foregroundStyle(.secondary)
            .textSelection(.enabled)
    }

    @ViewBuilder
    private var resourceList: some View {
        if store.resources.isEmpty {
            ContentUnavailableView(
                "未找到可播放资源",
                systemImage: "rectangle.slash",
                description: Text("可以尝试返回搜索页，使用更短的片名搜索。")
            )
            .appSurface()
        } else {
            VStack(alignment: .leading, spacing: 12) {
                Text("可播放资源")
                    .font(.title2.bold())

                ForEach(store.resources) { result in
                    NavigationLink(value: result) {
                        SearchResultRow(result: result)
                            .appSurface()
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }
}

private struct PosterViewer: View {
    @Environment(\.dismiss) private var dismiss
    let urlString: String

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()
            AsyncImage(url: URL(string: urlString)) { phase in
                switch phase {
                case .success(let image):
                    image
                        .resizable()
                        .scaledToFit()
                case .failure:
                    ContentUnavailableView("海报加载失败", systemImage: "photo.badge.exclamationmark")
                        .foregroundStyle(.white)
                default:
                    ProgressView()
                        .tint(.white)
                }
            }
            .padding(40)
        }
        .overlay(alignment: .topTrailing) {
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark.circle.fill")
                    .font(.largeTitle)
                    .symbolRenderingMode(.hierarchical)
            }
            .buttonStyle(.plain)
            .foregroundStyle(.white)
            .padding(24)
            .accessibilityLabel("关闭海报")
        }
    }
}

private extension String {
    var nonEmpty: String? { isEmpty ? nil : self }
}