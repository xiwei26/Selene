import SwiftUI

struct LiveScreenView: View {
    let liveStore: LiveStore
    let provider: LiveProviding

    @State private var showsPlayer = false

    private let columns = [GridItem(.adaptive(minimum: 180, maximum: 260), spacing: 12)]

    var body: some View {
        VStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
            AppPageHeader(
                title: "直播",
                subtitle: "按源和分组浏览直播频道。",
                systemImage: "dot.radiowaves.left.and.right"
            )

            toolbar

            if liveStore.isLoading && liveStore.channels.isEmpty {
                ProgressView("加载直播频道...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let error = liveStore.errorMessage {
                ContentUnavailableView("直播加载失败", systemImage: "exclamationmark.triangle", description: Text(error))
            } else if liveStore.filteredChannels.isEmpty {
                ContentUnavailableView("暂无频道", systemImage: "tv", description: Text("请选择其他直播源或分组"))
            } else {
                ScrollView {
                    LazyVGrid(columns: columns, spacing: 12) {
                        ForEach(liveStore.filteredChannels) { channel in
                            Button {
                                liveStore.selectChannel(channel)
                                showsPlayer = true
                                Task { await loadEPG(for: channel) }
                            } label: {
                                LiveChannelCard(channel: channel)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding()
                }
            }
        }
        .padding(AppTheme.pagePadding)
        .appPageBackground()
        .task {
            await liveStore.loadSources(provider: provider)
            if let source = liveStore.currentSource {
                await liveStore.loadChannels(sourceKey: source.key, provider: provider)
            }
        }
        .sheet(isPresented: $showsPlayer) {
            LivePlayerView(liveStore: liveStore, provider: provider)
                .frame(minWidth: 900, minHeight: 620)
        }
    }

    private var toolbar: some View {
        HStack {
            Picker("直播源", selection: sourceSelection) {
                ForEach(liveStore.sources) { source in
                    Text(source.name).tag(Optional(source.key))
                }
            }
            .frame(maxWidth: 220)

            Picker("分组", selection: groupSelection) {
                Text("全部").tag(Optional<String>.none)
                ForEach(liveStore.channelGroups) { group in
                    Text(group.name).tag(Optional(group.name))
                }
            }
            .frame(maxWidth: 180)

            Button {
                Task {
                    if let source = liveStore.currentSource {
                        await liveStore.loadChannels(sourceKey: source.key, provider: provider)
                    }
                }
            } label: {
                Label("刷新", systemImage: "arrow.clockwise")
            }
            .labelStyle(.iconOnly)
            .help("刷新频道")

            Spacer()
        }
        .appSurface()
    }

    private var sourceSelection: Binding<String?> {
        Binding {
            liveStore.currentSource?.key
        } set: { key in
            guard let key, let source = liveStore.sources.first(where: { $0.key == key }) else { return }
            liveStore.currentSource = source
            Task { await liveStore.loadChannels(sourceKey: key, provider: provider) }
        }
    }

    private var groupSelection: Binding<String?> {
        Binding {
            liveStore.selectedGroup
        } set: { group in
            liveStore.filterByGroup(group)
        }
    }

    private func loadEPG(for channel: LiveChannel) async {
        guard let source = liveStore.currentSource, !channel.tvgId.isEmpty else { return }
        await liveStore.loadEPG(tvgId: channel.tvgId, sourceKey: source.key, provider: provider)
    }
}

private struct LiveChannelCard: View {
    let channel: LiveChannel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                if !channel.logo.isEmpty, let url = URL(string: channel.logo) {
                    AsyncImage(url: url) { image in
                        image.resizable().scaledToFit()
                    } placeholder: {
                        Image(systemName: "tv")
                    }
                    .frame(width: 34, height: 34)
                } else {
                    Image(systemName: "tv")
                        .frame(width: 34, height: 34)
                }
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
        .padding(12)
        .frame(maxWidth: .infinity, minHeight: 110, alignment: .leading)
        .background(AppTheme.elevatedSurface)
        .overlay {
            RoundedRectangle(cornerRadius: AppTheme.radius)
                .stroke(AppTheme.border, lineWidth: 1)
        }
        .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
    }
}
