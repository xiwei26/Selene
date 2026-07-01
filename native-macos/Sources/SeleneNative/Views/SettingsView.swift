import SwiftUI
import AppKit

struct SettingsView: View {
    let sessionStore: SessionStore
    let themeStore: ThemeStore
    let versionService: VersionService

    @State private var updateInfo: VersionInfo?
    @State private var isCheckingUpdate = false
    @State private var updateError: String?

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: AppTheme.sectionSpacing) {
                AppPageHeader(
                    title: "设置",
                    subtitle: "账户、外观、更新和缓存。",
                    systemImage: "gearshape"
                )

                VStack(alignment: .leading, spacing: 12) {
                    AppSectionHeader(title: "外观")
                    Picker("主题", selection: Bindable(themeStore).mode) {
                        ForEach(ThemeStore.ThemeMode.allCases) { mode in
                            Text(mode.title).tag(mode)
                        }
                    }
                }
                .appSurface()

                VStack(alignment: .leading, spacing: 12) {
                    AppSectionHeader(title: "账户")
                    LabeledContent("服务器", value: sessionStore.session?.serverURL.absoluteString ?? "")
                    LabeledContent("用户", value: sessionStore.session?.username ?? "")
                    if sessionStore.session?.isLocalMode == true {
                        LabeledContent("模式", value: "本地订阅")
                    }
                    Button("退出登录", role: .destructive) {
                        sessionStore.logout()
                    }
                }
                .appSurface()

                VStack(alignment: .leading, spacing: 12) {
                    AppSectionHeader(title: "LunaTV 平台功能")
                    if let session = sessionStore.session, !session.isLocalMode {
                        Text("服务端：\(session.serverURL.absoluteString)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        Button {
                            NSWorkspace.shared.open(session.serverURL.appendingPathComponent("admin"))
                        } label: {
                            Label("在浏览器中打开管理后台", systemImage: "safari")
                        }
                    } else {
                        Text("请先登录 LunaTV 服务器以查看和管理平台功能状态。")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                .appSurface()

                VStack(alignment: .leading, spacing: 12) {
                    AppSectionHeader(title: "更新")
                    HStack {
                        Text(updateInfo.map { "发现新版本 \($0.version)" } ?? "当前版本 1.0.0")
                        Spacer()
                        Button(isCheckingUpdate ? "检查中..." : "检查更新") {
                            Task { await checkUpdate() }
                        }
                        .disabled(isCheckingUpdate)
                    }
                    if let releaseNotes = updateInfo?.releaseNotes {
                        Text(releaseNotes)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    if let updateError {
                        Text(updateError)
                            .font(.caption)
                            .foregroundStyle(.red)
                    }
                }
                .appSurface()

                VStack(alignment: .leading, spacing: 12) {
                    AppSectionHeader(title: "缓存")
                    Button("清理缓存") {
                        CacheService.shared.clearExpired()
                    }
                }
                .appSurface()
            }
            .padding(AppTheme.pagePadding)
        }
        .appPageBackground()
    }

    private func checkUpdate() async {
        isCheckingUpdate = true
        defer { isCheckingUpdate = false }
        do {
            updateInfo = try await versionService.checkForUpdate(currentVersion: "1.0.0")
            updateError = updateInfo == nil ? "当前已是最新版本" : nil
        } catch {
            updateError = error.localizedDescription
        }
    }
}
