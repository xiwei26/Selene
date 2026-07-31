import Foundation

/// M3U parsing for local subscription playlists. Transport lives in
/// `LunaTVClient`; this type only turns playlist text into channels.
enum LocalLiveService {
    static func parseM3U(_ text: String) -> [LiveChannel] {
        var channels: [LiveChannel] = []
        var pendingMetadata: String?

        for rawLine in text.components(separatedBy: .newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.hasPrefix("#EXTINF") {
                pendingMetadata = line
            } else if !line.isEmpty, !line.hasPrefix("#"), let metadata = pendingMetadata {
                let name = metadata.split(separator: ",", maxSplits: 1).last.map(String.init) ?? "未知频道"
                let tvgID = attribute("tvg-id", in: metadata)
                let logo = attribute("tvg-logo", in: metadata)
                let group = attribute("group-title", in: metadata).nonEmpty ?? "未分组"
                channels.append(
                    LiveChannel(
                        id: "\(tvgID)-\(line)",
                        tvgID: tvgID,
                        name: name,
                        logo: logo,
                        group: group,
                        url: line
                    )
                )
                pendingMetadata = nil
            }
        }
        return channels
    }

    private static func attribute(_ key: String, in text: String) -> String {
        guard let range = text.range(of: "\(key)=\"") else { return "" }
        let suffix = text[range.upperBound...]
        guard let end = suffix.firstIndex(of: "\"") else { return "" }
        return String(suffix[..<end])
    }
}

private extension String {
    var nonEmpty: String? { isEmpty ? nil : self }
}
