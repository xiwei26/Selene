import SwiftUI

@Observable
final class HistoryStore {
    var playRecords: [PlayRecord] = []
    var isLoading: Bool = false
    var errorMessage: String?

    func loadRecords(provider: ContentProvider) async {
        isLoading = true
        defer { isLoading = false }
        do {
            playRecords = try await provider.getPlayRecords()
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func saveRecord(_ record: PlayRecord, provider: ContentProvider) async {
        do {
            try await provider.savePlayRecord(record)
            if let index = playRecords.firstIndex(where: { $0.id == record.id }) {
                playRecords[index] = record
            } else {
                playRecords.insert(record, at: 0)
            }
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func deleteRecord(source: String, id: String, provider: ContentProvider) async {
        do {
            try await provider.deletePlayRecord(source: source, id: id)
            playRecords.removeAll { $0.id == "\(source)+\(id)" }
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func clearRecords(provider: ContentProvider) async {
        do {
            try await provider.clearPlayRecords()
            playRecords = []
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func recordFor(source: String, id: String) -> PlayRecord? {
        playRecords.first { $0.id == "\(source)+\(id)" }
    }

    func resumePosition(source: String, id: String) -> (index: Int, playTime: Int)? {
        guard let record = recordFor(source: source, id: id), record.playTime > 0 else { return nil }
        return (record.index, record.playTime)
    }
}
