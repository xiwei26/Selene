import SwiftUI

struct AdminView: View {
    @Environment(AppModel.self) private var appModel

    @State private var config: AdminConfig?
    @State private var youtube = YouTubeAdminConfig()
    @State private var bilibiliEnabled = false
    @State private var phase: LoadPhase = .idle
    @State private var savedMessage: String?

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

            Section("Bilibili") {
                Toggle("启用 Bilibili", isOn: $bilibiliEnabled)
                LabeledContent("登录状态", value: config?.bilibiliConfig?.loginStatus ?? "未知")
                Button("保存 Bilibili 配置", action: saveBilibili)
            }

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
        guard let client = appModel.client, appModel.session?.isLocalMode != true else {
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
        Task {
            do {
                try await appModel.client?.saveYouTubeConfig(youtube)
                savedMessage = "YouTube 配置已保存"
            } catch {
                phase = .failed(error.localizedDescription)
            }
        }
    }

    private func saveBilibili() {
        Task {
            do {
                try await appModel.client?.saveBilibiliConfig(enabled: bilibiliEnabled)
                savedMessage = "Bilibili 配置已保存"
            } catch {
                phase = .failed(error.localizedDescription)
            }
        }
    }
}