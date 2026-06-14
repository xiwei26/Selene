import SwiftUI

@Observable
@MainActor
final class LiveStore {
    var sources: [LiveSource] = []
    var channels: [LiveChannel] = []
    var filteredChannels: [LiveChannel] = []
    var channelGroups: [LiveChannelGroup] = []
    var currentSource: LiveSource?
    var currentChannel: LiveChannel?
    var currentEPG: EpgData?
    var selectedGroup: String?
    var isLoading: Bool = false
    var errorMessage: String?

    func loadSources(provider: LiveProviding) async {
        isLoading = true
        defer { isLoading = false }
        do {
            sources = try await provider.getLiveSources().filter { !$0.disabled }
            currentSource = currentSource ?? sources.first
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func loadChannels(sourceKey: String, provider: LiveProviding) async {
        isLoading = true
        defer { isLoading = false }
        do {
            channels = try await provider.getLiveChannels(sourceKey: sourceKey)
            rebuildGroups()
            filterByGroup(selectedGroup)
            currentChannel = channels.first
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func loadEPG(tvgId: String, sourceKey: String, provider: LiveProviding) async {
        do {
            currentEPG = try await provider.getLiveEPG(tvgId: tvgId, sourceKey: sourceKey)
        } catch {
            currentEPG = nil
        }
    }

    func selectChannel(_ channel: LiveChannel) {
        currentChannel = channel
    }

    func filterByGroup(_ group: String?) {
        selectedGroup = group
        guard let group else {
            filteredChannels = channels
            return
        }
        filteredChannels = channels.filter { $0.group == group }
    }

    func rebuildGroups() {
        let grouped = Dictionary(grouping: channels) { channel in
            channel.group.isEmpty ? "未分组" : channel.group
        }
        channelGroups = grouped.map { LiveChannelGroup(name: $0.key, channels: $0.value) }
            .sorted { $0.name < $1.name }
        filteredChannels = channels
    }
}
