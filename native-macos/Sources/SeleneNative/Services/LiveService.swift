import Foundation

protocol LiveProviding: Sendable {
    func getLiveSources() async throws -> [LiveSource]
    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel]
    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData?
}

final class LiveServiceClient: LiveProviding, Sendable {
    private let provider: ContentProvider?
    private let localSources: [LiveSource]
    private let session: URLSession
    private let cache: CacheService

    init(
        provider: ContentProvider? = nil,
        localSources: [LiveSource] = [],
        session: URLSession = .shared,
        cache: CacheService = .shared
    ) {
        self.provider = provider
        self.localSources = localSources
        self.session = session
        self.cache = cache
    }

    func getLiveSources() async throws -> [LiveSource] {
        if !localSources.isEmpty {
            return localSources.filter { !$0.disabled }
        }
        return try await provider?.getLiveSources() ?? []
    }

    func getLiveChannels(sourceKey: String) async throws -> [LiveChannel] {
        if let cached: [LiveChannel] = cache.load(key: "live-channels-\(sourceKey)", maxAge: 2 * 60 * 60) {
            return cached
        }

        if let provider, localSources.isEmpty {
            let channels = try await provider.getLiveChannels(sourceKey: sourceKey)
            try? cache.save(key: "live-channels-\(sourceKey)", data: channels, maxAge: 2 * 60 * 60)
            return channels
        }

        guard let source = localSources.first(where: { $0.key == sourceKey }),
              let url = URL(string: source.url) else {
            return []
        }
        let (data, _) = try await session.data(from: url)
        guard let text = Self.decodeText(data) else { throw APIError.parseError }
        let channels = try Self.parseM3U(text)
        try? cache.save(key: "live-channels-\(sourceKey)", data: channels, maxAge: 2 * 60 * 60)
        return channels
    }

    func getLiveEPG(tvgId: String, sourceKey: String) async throws -> EpgData? {
        if let cached: EpgData = cache.load(key: "live-epg-\(sourceKey)-\(tvgId)", maxAge: 2 * 60 * 60) {
            return cached
        }

        if let provider, localSources.isEmpty {
            let epg = try await provider.getLiveEPG(tvgId: tvgId, sourceKey: sourceKey)
            if let epg {
                try? cache.save(key: "live-epg-\(sourceKey)-\(tvgId)", data: epg, maxAge: 2 * 60 * 60)
            }
            return epg
        }

        guard let source = localSources.first(where: { $0.key == sourceKey }),
              !source.epg.isEmpty,
              let url = URL(string: source.epg) else {
            return nil
        }
        let (data, _) = try await session.data(from: url)
        guard let text = Self.decodeText(data) else { throw APIError.parseError }
        let epg = try Self.parseEPG(text, tvgId: tvgId, source: sourceKey, epgUrl: source.epg)
        try? cache.save(key: "live-epg-\(sourceKey)-\(tvgId)", data: epg, maxAge: 2 * 60 * 60)
        return epg
    }

    static func parseM3U(_ content: String) throws -> [LiveChannel] {
        let lines = content.components(separatedBy: .newlines)
        var channels: [LiveChannel] = []
        var pendingInfo: String?

        for rawLine in lines {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !line.isEmpty else { continue }
            if line.hasPrefix("#EXTINF") {
                pendingInfo = line
                continue
            }
            guard !line.hasPrefix("#"), let info = pendingInfo else { continue }
            let name = info.components(separatedBy: ",").last?.trimmingCharacters(in: .whitespacesAndNewlines) ?? line
            let tvgId = attribute("tvg-id", in: info)
            let logo = attribute("tvg-logo", in: info)
            let group = attribute("group-title", in: info)
            channels.append(
                LiveChannel(
                    id: "\(name)-\(line)",
                    tvgId: tvgId,
                    name: name,
                    logo: logo,
                    group: group.isEmpty ? "未分组" : group,
                    url: line,
                    isFavorite: false
                )
            )
            pendingInfo = nil
        }

        return channels
    }

    static func parseEPG(_ content: String, tvgId: String, source: String, epgUrl: String) throws -> EpgData {
        let programmePattern = #"<programme[^>]*channel="([^"]+)"[^>]*start="([^"]+)"[^>]*stop="([^"]+)"[^>]*>([\s\S]*?)</programme>"#
        let regex = try NSRegularExpression(pattern: programmePattern)
        let range = NSRange(content.startIndex..<content.endIndex, in: content)
        let programmes = regex.matches(in: content, range: range).compactMap { match -> EpgProgram? in
            guard let channelRange = Range(match.range(at: 1), in: content),
                  let startRange = Range(match.range(at: 2), in: content),
                  let stopRange = Range(match.range(at: 3), in: content),
                  let bodyRange = Range(match.range(at: 4), in: content) else {
                return nil
            }
            let channelId = String(content[channelRange])
            guard channelId == tvgId else { return nil }
            let body = String(content[bodyRange])
            return EpgProgram(
                channelId: channelId,
                title: firstTag("title", in: body),
                startTime: parseEPGDate(String(content[startRange])) ?? Date(),
                endTime: parseEPGDate(String(content[stopRange])) ?? Date(),
                description: firstTag("desc", in: body)
            )
        }

        return EpgData(tvgId: tvgId, source: source, epgUrl: epgUrl, programs: programmes)
    }

    private static func decodeText(_ data: Data) -> String? {
        String(data: data, encoding: .utf8) ?? String(data: data, encoding: .isoLatin1)
    }

    private static func attribute(_ name: String, in text: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"\#(name)="([^"]*)""#),
              let match = regex.firstMatch(in: text, range: NSRange(text.startIndex..<text.endIndex, in: text)),
              let range = Range(match.range(at: 1), in: text) else {
            return ""
        }
        return String(text[range])
    }

    private static func firstTag(_ name: String, in text: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: #"<\#(name)[^>]*>([\s\S]*?)</\#(name)>"#),
              let match = regex.firstMatch(in: text, range: NSRange(text.startIndex..<text.endIndex, in: text)),
              let range = Range(match.range(at: 1), in: text) else {
            return ""
        }
        return String(text[range]).trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func parseEPGDate(_ value: String) -> Date? {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyyMMddHHmmss Z"
        if let date = formatter.date(from: value) {
            return date
        }
        formatter.dateFormat = "yyyyMMddHHmmss"
        return formatter.date(from: value)
    }
}
