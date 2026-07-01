import SwiftUI

struct AdminView: View {
    let provider: ServerAPIClient

    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var successMessage: String?
    @State private var youtube = YouTubeAdminConfig()
    @State private var bilibiliEnabled = false
    @State private var bilibiliUser: BilibiliAdminUserInfo?
    @State private var bilibiliLoginStatus: String?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(
                    title: "管理后台",
                    subtitle: "配置 LunaTV 平台能力。",
                    systemImage: "gearshape.2"
                )

                if isLoading {
                    ProgressView("加载中...")
                        .frame(maxWidth: .infinity, minHeight: 120)
                }

                if let errorMessage {
                    Text(errorMessage)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .appSurface()
                }

                if let successMessage {
                    Text(successMessage)
                        .font(.caption)
                        .foregroundStyle(.green)
                        .appSurface()
                }

                youtubeSection
                bilibiliSection
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
        .task {
            await load()
        }
    }

    private var youtubeSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            AppSectionHeader(title: "YouTube 配置")
            Toggle("启用 YouTube 搜索", isOn: $youtube.enabled)
            SecureField("API Key", text: $youtube.apiKey)
                .textFieldStyle(.roundedBorder)
            Toggle("启用 Demo 模式", isOn: $youtube.enableDemo)
            Stepper("最大结果数 \(youtube.maxResults)", value: $youtube.maxResults, in: 1...50)

            Text("可用地区")
                .font(.caption.weight(.semibold))
            tagEditor(values: YouTubeAdminConfig.defaultRegions, selection: $youtube.enabledRegions)

            Text("可用分类")
                .font(.caption.weight(.semibold))
            tagEditor(values: YouTubeAdminConfig.defaultCategories, selection: $youtube.enabledCategories)

            Button {
                Task { await saveYouTube() }
            } label: {
                Label("保存 YouTube 配置", systemImage: "checkmark.circle")
            }
        }
        .appSurface()
    }

    private var bilibiliSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            AppSectionHeader(title: "Bilibili 配置")
            if let bilibiliUser {
                Text("已登录：\(bilibiliUser.username) (UID: \(bilibiliUser.mid))")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else if bilibiliLoginStatus != "logged_in" {
                Text("未登录 B站账号，可先启用基础搜索。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Toggle("启用 Bilibili 搜索", isOn: $bilibiliEnabled)
            Button {
                Task { await saveBilibili() }
            } label: {
                Label("保存 Bilibili 配置", systemImage: "checkmark.circle")
            }
        }
        .appSurface()
    }

    private func tagEditor(values: [String], selection: Binding<[String]>) -> some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 140), spacing: 8)], alignment: .leading, spacing: 8) {
            ForEach(values, id: \.self) { value in
                Toggle(value, isOn: Binding(
                    get: { selection.wrappedValue.contains(value) },
                    set: { enabled in
                        if enabled {
                            if !selection.wrappedValue.contains(value) {
                                selection.wrappedValue.append(value)
                            }
                        } else {
                            selection.wrappedValue.removeAll { $0 == value }
                        }
                    }
                ))
                .toggleStyle(.checkbox)
            }
        }
    }

    private func load() async {
        isLoading = true
        errorMessage = nil
        successMessage = nil
        defer { isLoading = false }
        do {
            guard let config = try await provider.getAdminConfig() else { return }
            youtube = config.youTubeConfig ?? YouTubeAdminConfig()
            if youtube.enabledRegions.isEmpty {
                youtube.enabledRegions = YouTubeAdminConfig.defaultRegions
            }
            if youtube.enabledCategories.isEmpty {
                youtube.enabledCategories = YouTubeAdminConfig.defaultCategories
            }
            bilibiliEnabled = config.bilibiliConfig?.enabled ?? false
            bilibiliUser = config.bilibiliConfig?.userInfo
            bilibiliLoginStatus = config.bilibiliConfig?.loginStatus
        } catch {
            youtube = YouTubeAdminConfig()
            bilibiliEnabled = false
            bilibiliUser = nil
            bilibiliLoginStatus = nil
            errorMessage = error.localizedDescription
        }
    }

    private func saveYouTube() async {
        await save {
            try await provider.saveYouTubeConfig(youtube)
            successMessage = "YouTube 配置已保存"
        }
    }

    private func saveBilibili() async {
        await save {
            try await provider.saveBilibiliConfig(enabled: bilibiliEnabled)
            successMessage = "Bilibili 配置已保存"
        }
    }

    private func save(_ operation: () async throws -> Void) async {
        errorMessage = nil
        successMessage = nil
        do {
            try await operation()
        } catch {
            errorMessage = error.localizedDescription
        }
    }
}
