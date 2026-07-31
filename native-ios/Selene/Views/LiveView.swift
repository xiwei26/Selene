import AVKit
import SwiftUI

struct LiveView: View {
    @Environment(AppModel.self) private var appModel
    @State private var store = LiveStore()
    @State private var playerChannel: LiveChannel?

    private let columns = [GridItem(.adaptive(minimum: 180, maximum: 280), spacing: 14)]

    var body: some View {
        VStack(spacing: 0) {
            LiveFilterBar(store: store, sourceAction: selectSource)
                .padding()

            Group {
                switch store.phase {
                case .idle, .loading:
                    ProgressView("正在加载直播频道…")
                case .failed(let message):
                    ContentUnavailableView("直播加载失败", systemImage: "wifi.exclamationmark", description: Text(message))
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
            await store.loadSources(appModel: appModel)
        }
        .fullScreenCover(item: $playerChannel) { channel in
            LivePlayerSheet(channel: channel, epg: store.epg)
        }
    }

    private func selectSource(_ source: LiveSource) {
        Task { await store.loadChannels(source: source, appModel: appModel) }
    }

    private func open(_ channel: LiveChannel) {
        playerChannel = channel
        Task { await store.loadEPG(channel: channel, appModel: appModel) }
    }
}

private struct LiveFilterBar: View {
    @Bindable var store: LiveStore
    let sourceAction: (LiveSource) -> Void

    var body: some View {
        HStack {
            Picker("直播源", selection: $store.selectedSource) {
                ForEach(store.sources) { source in
                    Text(source.name).tag(Optional(source))
                }
            }
            .onChange(of: store.selectedSource) { _, value in
                if let value { sourceAction(value) }
            }

            Picker("分组", selection: $store.selectedGroup) {
                Text("全部").tag(Optional<String>.none)
                ForEach(store.groups, id: \.self) { group in
                    Text(group).tag(Optional(group))
                }
            }

            Spacer()
            Text("\(store.filteredChannels.count) 个频道")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .appSurface()
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
            Text(channel.url)
                .font(.caption2)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity, minHeight: 110, alignment: .leading)
        .appSurface()
    }
}

private struct LivePlayerSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var player: AVPlayer

    let channel: LiveChannel
    let epg: EPGData?

    init(channel: LiveChannel, epg: EPGData?) {
        self.channel = channel
        self.epg = epg
        if let url = URL(string: channel.url) {
            _player = State(initialValue: AVPlayer(url: url))
        } else {
            _player = State(initialValue: AVPlayer())
        }
    }

    var body: some View {
        ZStack(alignment: .top) {
            Color.black.ignoresSafeArea()
            VideoPlayer(player: player)
                .ignoresSafeArea()
            HStack {
                Button("关闭", systemImage: "xmark") { dismiss() }
                    .buttonStyle(.borderedProminent)
                    .tint(.black.opacity(0.7))
                VStack(alignment: .leading) {
                    Text(channel.name).font(.headline)
                    if let current = epg?.programs.first(where: \.isLive) {
                        Text("\(current.timeRange) \(current.title)").font(.caption)
                    }
                }
                .foregroundStyle(.white)
                Spacer()
            }
            .padding()
        }
        .onAppear { player.play() }
        .onDisappear { player.pause() }
    }
}