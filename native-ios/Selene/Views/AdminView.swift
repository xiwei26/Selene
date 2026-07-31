import SwiftUI

struct AdminView: View {
    @Environment(AppModel.self) private var appModel

    @State private var config: AdminConfig?
    @State private var youtube = YouTubeAdminConfig()
    @State private var bilibiliEnabled = false
    @State private var phase: LoadPhase = .idle
    @State private var savedMessage: String?
    @State private var saveTask: Task<Void, Never>?

    /// Writes are only meaningful once the config actually loaded; in local mode the
    /// client points at a placeholder base URL, so a POST would go nowhere useful.
    private var canEdit: Bool {
        phase == .loaded && !appModel.isLocalMode
    }

    var body: some View {
        Form {
            if phase.isLoading {
                ProgressView("正在读取管理配置…")
            }
            if case .failed(let message) = phase {
                ContentUnavailableView("管理配置加载失败", systemImage: "gearshape.2", description: Text(message))
            }

            Section("站点") {
                LabeledContent("站点名称", value: config?.siteConfig?.siteName ?? "LunaTV")
                LabeledContent("当前角色", value: config?.role ?? "未知")
                if let announcement = config?.siteConfig?.announcement, !announcement.isEmpty {
                    Text(announcement)
                }
            }

            Section("YouTube") {
                Toggle("启用 YouTube", isOn: $youtube.enabled)
                SecureField("API Key", text: $youtube.apiKey)
                Toggle("允许演示数据", isOn: $youtube.enableDemo)
                Stepper("最大结果数：\(youtube.maxResults)", value: $youtube.maxResults, in: 5...50, step: 5)
                Button("保存 YouTube 配置", action: saveYouTube)
            }
            .disabled(!canEdit)

            Section("Bilibili") {
                Toggle("启用 Bilibili", isOn: $bilibiliEnabled)
                LabeledContent("登录状态", value: config?.bilibiliConfig?.loginStatus ?? "未知")
                Button("保存 Bilibili 配置", action: saveBilibili)
            }
            .disabled(!canEdit)

            Section("短剧") {
                LabeledContent("主接口", value: config?.shortDramaConfig?.primaryApiUrl ?? "未配置")
                LabeledContent("备用接口", value: config?.shortDramaConfig?.alternativeApiUrl ?? "未配置")
            }

            if let savedMessage {
                Section {
                    Label(savedMessage, systemImage: "checkmark.circle.fill")
                        .foregroundStyle(AppTheme.accent)
                }
            }
        }
        .scrollContentBackground(.hidden)
        .task(id: appModel.session?.id) {
            await load()
        }
    }

    private func load() async {
        guard let client = appModel.client, !appModel.isLocalMode else {
            phase = .failed("管理后台需要 LunaTV 服务器会话")
            return
        }
        phase = .loading
        do {
            config = try await client.adminConfig()
            youtube = config?.youTubeConfig ?? YouTubeAdminConfig()
            bilibiliEnabled = config?.bilibiliConfig?.enabled ?? false
            phase = .loaded
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }

    private func saveYouTube() {
        save(message: "YouTube 配置已保存") { client in
            try await client.saveYouTubeConfig(youtube)
        }
    }

    private func saveBilibili() {
        save(message: "Bilibili 配置已保存") { client in
            try await client.saveBilibiliConfig(enabled: bilibiliEnabled)
        }
    }

    /// Serializes admin writes: a second tap replaces the in-flight request instead
    /// of racing it, so the last value the user chose is the one that lands.
    private func save(
        message: String,
        write: @escaping @MainActor (LunaTVClient) async throws -> Void
    ) {
        guard canEdit, let client = appModel.client else { return }
        saveTask?.cancel()
        saveTask = Task {
            do {
                try await write(client)
                guard !Task.isCancelled else { return }
                savedMessage = message
            } catch {
                // A superseded save must not overwrite the newer request's outcome.
                guard !isCancellation(error) else { return }
                phase = .failed(error.localizedDescription)
            }
        }
    }
}