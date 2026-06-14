import SwiftUI
import AVKit

struct LivePlayerView: View {
    let liveStore: LiveStore
    let provider: LiveProviding

    @State private var player: AVPlayer?

    var body: some View {
        HSplitView {
            VStack(spacing: 0) {
                if let player {
                    VideoPlayer(player: player)
                        .onDisappear {
                            player.pause()
                        }
                } else {
                    ContentUnavailableView("选择频道播放", systemImage: "play.tv")
                }

                if let channel = liveStore.currentChannel {
                    HStack {
                        Text(channel.name)
                            .font(.headline)
                        Spacer()
                        Button {
                            start(channel)
                        } label: {
                            Label("重载", systemImage: "arrow.clockwise")
                        }
                        .labelStyle(.iconOnly)
                        .help("重新加载直播流")
                    }
                    .padding()
                }
            }
            .frame(minWidth: 520)

            VStack(alignment: .leading, spacing: 12) {
                Text("频道")
                    .font(.headline)
                List(liveStore.filteredChannels, selection: channelSelection) { channel in
                    Text(channel.name)
                }
                Divider()
                epgView
            }
            .padding()
            .frame(minWidth: 260)
        }
        .onAppear {
            if let channel = liveStore.currentChannel {
                start(channel)
            }
        }
    }

    private var channelSelection: Binding<LiveChannel.ID?> {
        Binding {
            liveStore.currentChannel?.id
        } set: { id in
            guard let id, let channel = liveStore.filteredChannels.first(where: { $0.id == id }) else { return }
            liveStore.selectChannel(channel)
            start(channel)
            Task {
                if let source = liveStore.currentSource, !channel.tvgId.isEmpty {
                    await liveStore.loadEPG(tvgId: channel.tvgId, sourceKey: source.key, provider: provider)
                }
            }
        }
    }

    private var epgView: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("节目单")
                .font(.headline)
            if let epg = liveStore.currentEPG, !epg.programs.isEmpty {
                List(epg.programs) { program in
                    VStack(alignment: .leading, spacing: 4) {
                        HStack {
                            Text(program.timeRange)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            if program.isLive {
                                Text("直播中")
                                    .font(.caption2)
                                    .foregroundStyle(.red)
                            }
                        }
                        Text(program.title)
                            .font(.body)
                    }
                }
            } else {
                Text("暂无 EPG")
                    .foregroundStyle(.secondary)
            }
        }
    }

    private func start(_ channel: LiveChannel) {
        guard let url = URL(string: channel.url) else { return }
        let player = AVPlayer(url: url)
        self.player = player
        player.play()
    }
}
