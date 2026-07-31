import AVKit
import SwiftUI

struct PlaybackView: View {
    @Environment(\.dismiss) private var dismiss
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
                    PlaybackCompactControls(coordinator: coordinator)
                        .background(.ultraThinMaterial)
                }
            }
        }
        .background(Color.black)
        .statusBarHidden()
        .interactiveDismissDisabled()
        .onDisappear(perform: persistAndStop)
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
                    Text(coordinator.currentResult?.title ?? "Selene")
                        .font(.headline)
                    if let result = coordinator.currentResult {
                        Text(result.episodeTitle(at: coordinator.episodeIndex))
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

    private func close() {
        persistAndStop()
        dismiss()
    }

    private func persistAndStop() {
        guard coordinator.isPlaying else { return }
        let record = coordinator.makeRecord()
        coordinator.stop()
        if let record {
            Task { await appModel.savePlayRecord(record) }
        }
    }
}

private struct PlaybackSidePanel: View {
    @Bindable var coordinator: PlayerCoordinator

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                sourceSection
                episodeSection
            }
            .padding(18)
        }
    }

    private var sourceSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("片源", systemImage: "server.rack")
                .font(.headline)
            ForEach(coordinator.sourceResults) { source in
                Button {
                    coordinator.switchSource(source)
                } label: {
                    HStack {
                        Text(source.sourceName.isEmpty ? source.source : source.sourceName)
                        Spacer()
                        if source.id == coordinator.currentResult?.id,
                           source.source == coordinator.currentResult?.source {
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
                ForEach(coordinator.sourceResults) { result in
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