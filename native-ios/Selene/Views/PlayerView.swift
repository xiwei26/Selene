import AVKit
import SwiftUI

struct PlaybackView: View {
    @Environment(\.dismiss) private var dismiss
    @Environment(\.scenePhase) private var scenePhase
    @Environment(AppModel.self) private var appModel
    @Environment(PlayerCoordinator.self) private var coordinator

    var body: some View {
        GeometryReader { proxy in
            if proxy.size.width > 900 {
                HStack(spacing: 0) {
                    playerSurface
                    Divider()
                    PlaybackSidePanel(coordinator: coordinator)
                        .frame(width: 360)
                        .background(.regularMaterial)
                }
            } else {
                ZStack(alignment: .bottom) {
                    playerSurface
                    if !coordinator.isLive {
                        PlaybackCompactControls(coordinator: coordinator)
                            .background(.ultraThinMaterial)
                    }
                }
            }
        }
        .background(Color.black)
        .statusBarHidden()
        .interactiveDismissDisabled()
        // Teardown + persistence are owned exclusively by MainSplitView's
        // presentation binding so close / dismiss cannot double-save.
        .onChange(of: scenePhase) { _, phase in
            // Backgrounding can be followed by termination without dismiss
            // ever running, so checkpoint progress while keeping the player.
            guard phase != .active else { return }
            persistProgress()
        }
    }

    private var playerSurface: some View {
        ZStack(alignment: .top) {
            Color.black
            if let player = coordinator.player {
                VideoPlayer(player: player)
            }

            HStack(spacing: 12) {
                Button("关闭", systemImage: "xmark", action: close)
                    .buttonStyle(.borderedProminent)
                    .tint(.black.opacity(0.7))

                VStack(alignment: .leading, spacing: 2) {
                    Text(titleText)
                        .font(.headline)
                    if let subtitle = subtitleText {
                        Text(subtitle)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                .foregroundStyle(.white)

                Spacer()
            }
            .padding()

            if let error = coordinator.errorMessage {
                ContentUnavailableView("播放失败", systemImage: "exclamationmark.triangle", description: Text(error))
                    .foregroundStyle(.white)
                    .padding()
                    .background(.black.opacity(0.75), in: RoundedRectangle(cornerRadius: 18))
                    .frame(maxWidth: 520)
            }
        }
    }

    private var titleText: String {
        if coordinator.isLive {
            return coordinator.liveChannelName ?? "直播"
        }
        return coordinator.currentResult?.title ?? "Selene"
    }

    private var subtitleText: String? {
        if coordinator.isLive {
            if let current = coordinator.liveEPG?.programs.first(where: \.isLive) {
                return "\(current.timeRange) \(current.title)"
            }
            return "直播中"
        }
        guard let result = coordinator.currentResult else { return nil }
        return result.episodeTitle(at: coordinator.episodeIndex)
    }

    private func close() {
        dismiss()
    }

    /// Checkpoints progress without disturbing the active player.
    private func persistProgress() {
        guard coordinator.isPlaying, !appModel.isLocalMode else { return }
        guard let record = coordinator.checkpointRecord() else { return }
        Task { await appModel.savePlayRecord(record) }
    }
}

private struct PlaybackSidePanel: View {
    @Bindable var coordinator: PlayerCoordinator

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                if coordinator.isLive {
                    liveSection
                } else {
                    sourceSection
                    episodeSection
                }
            }
            .padding(18)
        }
    }

    private var liveSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("直播", systemImage: "dot.radiowaves.left.and.right")
                .font(.headline)
            Text(coordinator.liveChannelName ?? "未知频道")
                .font(.title3.weight(.semibold))
            if let programs = coordinator.liveEPG?.programs, !programs.isEmpty {
                Text("节目单")
                    .font(.subheadline.weight(.semibold))
                    .padding(.top, 8)
                ForEach(programs.prefix(12)) { program in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(program.timeRange)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        Text(program.title)
                            .font(.callout)
                        if program.isLive {
                            Text("正在播出")
                                .font(.caption2.weight(.semibold))
                                .foregroundStyle(AppTheme.accent)
                        }
                    }
                    .padding(.vertical, 4)
                }
            } else {
                Text("暂无 EPG 节目信息")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var sourceSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("片源", systemImage: "server.rack")
                .font(.headline)
            ForEach(coordinator.sourceResults, id: \.uniqueKey) { source in
                Button {
                    coordinator.switchSource(source)
                } label: {
                    HStack {
                        Text(source.sourceName.isEmpty ? source.source : source.sourceName)
                        Spacer()
                        if source.uniqueKey == coordinator.currentResult?.uniqueKey {
                            Image(systemName: "checkmark.circle.fill")
                        }
                    }
                }
                .buttonStyle(.bordered)
                .tint(AppTheme.accent)
            }
        }
    }

    private var episodeSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Label("选集", systemImage: "rectangle.stack")
                    .font(.headline)
                Spacer()
                Button("倒序", systemImage: "arrow.up.arrow.down") {
                    coordinator.isEpisodeReversed.toggle()
                }
                .labelStyle(.iconOnly)
            }

            LazyVGrid(columns: [GridItem(.adaptive(minimum: 92), spacing: 8)], spacing: 8) {
                ForEach(coordinator.orderedEpisodeIndices, id: \.self) { index in
                    Button(coordinator.currentResult?.episodeTitle(at: index) ?? "第 \(index + 1) 集") {
                        coordinator.playEpisode(index)
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(index == coordinator.episodeIndex ? AppTheme.accent : .gray.opacity(0.45))
                    .lineLimit(1)
                }
            }
        }
    }
}

private struct PlaybackCompactControls: View {
    @Bindable var coordinator: PlayerCoordinator

    var body: some View {
        VStack(spacing: 10) {
            Picker("片源", selection: sourceBinding) {
                ForEach(coordinator.sourceResults, id: \.uniqueKey) { result in
                    Text(result.sourceName.isEmpty ? result.source : result.sourceName)
                        .tag(Optional(result))
                }
            }
            .pickerStyle(.menu)

            ScrollView(.horizontal) {
                HStack {
                    ForEach(coordinator.orderedEpisodeIndices, id: \.self) { index in
                        Button(coordinator.currentResult?.episodeTitle(at: index) ?? "\(index + 1)") {
                            coordinator.playEpisode(index)
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(index == coordinator.episodeIndex ? AppTheme.accent : .gray)
                    }
                }
            }
            .scrollIndicators(.hidden)
        }
        .padding()
    }

    private var sourceBinding: Binding<SearchResult?> {
        Binding {
            coordinator.currentResult
        } set: { value in
            if let value { coordinator.switchSource(value) }
        }
    }
}