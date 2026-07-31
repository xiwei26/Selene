import SwiftUI

struct LiveView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var coordinator
    @State private var store = LiveStore()

    private let columns = [GridItem(.adaptive(minimum: 180, maximum: 280), spacing: 14)]

    var body: some View {
        VStack(spacing: 0) {
            LiveFilterBar(store: store)
                .padding()

            Group {
                switch store.phase {
                case .idle, .loading:
                    ProgressView("正在加载直播频道…")
                case .failed(let message):
                    ContentUnavailableView {
                        Label("直播加载失败", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(message)
                    } actions: {
                        Button("重试") {
                            Task { await reloadSources() }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                default:
                    if store.filteredChannels.isEmpty {
                        ContentUnavailableView("暂无频道", systemImage: "tv", description: Text("请选择其他直播源或分组。"))
                    } else {
                        ScrollView {
                            LazyVGrid(columns: columns, spacing: 14) {
                                ForEach(store.filteredChannels) { channel in
                                    Button {
                                        open(channel)
                                    } label: {
                                        LiveChannelCard(channel: channel)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                            .padding(AppTheme.pagePadding)
                        }
                    }
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .task(id: appModel.session?.id) {
            await reloadSources()
        }
        // Channel loading keys off the selection instead of the picker's
        // onChange, so the first source assigned by loadSources also loads and
        // a superseded load is cancelled rather than racing the new one.
        .task(id: store.selectedSource?.id) {
            guard let source = store.selectedSource else { return }
            await store.loadChannels(
                source: source,
                isLocalMode: appModel.isLocalMode,
                client: appModel.client
            )
        }
    }

    private func reloadSources() async {
        await store.loadSources(
            isLocalMode: appModel.isLocalMode,
            localSources: appModel.session?.localLiveSources ?? [],
            client: appModel.client
        )
    }

    private func open(_ channel: LiveChannel) {
        // Live and VOD share one fullScreenCover owned by MainSplitView.
        coordinator.openLive(channel: channel, epg: store.epg)
        Task {
            await store.loadEPG(
                channel: channel,
                isLocalMode: appModel.isLocalMode,
                client: appModel.client
            )
            coordinator.updateLiveEPG(store.epg)
        }
    }
}

private struct LiveFilterBar: View {
    @Bindable var store: LiveStore

    var body: some View {
        // Long Chinese source names squeeze a single row at compact and narrow
        // iPad widths, so fall back to stacked rows.
        ViewThatFits(in: .horizontal) {
            HStack {
                pickers
                Spacer()
                channelCount
            }

            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    pickers
                }
                HStack {
                    Spacer()
                    channelCount
                }
            }
        }
        .appSurface()
    }

    @ViewBuilder
    private var pickers: some View {
        Picker("直播源", selection: $store.selectedSource) {
            ForEach(store.sources) { source in
                Text(source.name).tag(Optional(source))
            }
        }
        .lineLimit(1)

        Picker("分组", selection: $store.selectedGroup) {
            Text("全部").tag(Optional<String>.none)
            ForEach(store.groups, id: \.self) { group in
                Text(group).tag(Optional(group))
            }
        }
        .lineLimit(1)
    }

    private var channelCount: some View {
        Text("\(store.filteredChannels.count) 个频道")
            .font(.caption)
            .foregroundStyle(.secondary)
            .lineLimit(1)
            .fixedSize()
    }
}

private struct LiveChannelCard: View {
    let channel: LiveChannel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                AsyncImage(url: URL(string: channel.logo)) { image in
                    image.resizable().scaledToFit()
                } placeholder: {
                    Image(systemName: "tv")
                }
                .frame(width: 38, height: 38)
                Spacer()
                Text(channel.group)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
            Text(channel.name)
                .font(.headline)
                .lineLimit(2)
            Text("点击播放")
                .font(.caption2)
                .foregroundStyle(AppTheme.accent)
        }
        .frame(maxWidth: .infinity, minHeight: 110, alignment: .leading)
        .appSurface()
    }
}
