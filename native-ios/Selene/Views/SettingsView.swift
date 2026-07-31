import SwiftUI

struct SettingsView: View {
    @Environment(AppModel.self) private var appModel
    @State private var showsSignOutConfirmation = false

    var body: some View {
        Form {
            Section("外观") {
                Picker("主题", selection: themeBinding) {
                    ForEach(AppModel.ThemeMode.allCases) { mode in
                        Text(mode.title).tag(mode)
                    }
                }
            }

            Section("当前服务器") {
                LabeledContent("地址", value: appModel.session?.serverURL.absoluteString ?? "未连接")
                LabeledContent("用户", value: appModel.session?.username ?? "—")
                LabeledContent("连接状态") {
                    Label("已连接", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(AppTheme.accent)
                }
                LabeledContent("模式", value: appModel.session?.isLocalMode == true ? "本地订阅" : "LunaTV 服务器")
            }

            Section("原生重构") {
                LabeledContent("客户端", value: "SwiftUI / AVKit")
                LabeledContent("最低系统", value: "iOS / iPadOS 17")
                LabeledContent("当前阶段", value: "浏览、搜索、详情、播放")
            }

            Section("关于") {
                LabeledContent("版本", value: "1.1.0 (1)")
                Link(
                    destination: URL(string: "https://github.com/SzeMeng76/LunaTV")!
                ) {
                    Label("LunaTV 后端", systemImage: "link")
                }
            }

            Section("LunaTV 平台功能") {
                if let session = appModel.session, !session.isLocalMode {
                    Link(destination: session.serverURL.appendingPathComponent("admin")) {
                        Label("在浏览器中打开管理后台", systemImage: "safari")
                    }
                } else {
                    Text("本地模式不提供服务端管理后台。")
                        .foregroundStyle(.secondary)
                }
            }

            Section("缓存与同步") {
                Button("刷新收藏与播放历史", systemImage: "arrow.clockwise") {
                    Task { await appModel.refreshLibrary() }
                }
                Button("清理图片与网络缓存", systemImage: "trash") {
                    appModel.clearCaches()
                }
            }

            Section {
                Button("退出登录", role: .destructive) {
                    showsSignOutConfirmation = true
                }
            }
        }
        .scrollContentBackground(.hidden)
        .confirmationDialog(
            "退出当前服务器？",
            isPresented: $showsSignOutConfirmation,
            titleVisibility: .visible
        ) {
            Button("退出登录", role: .destructive) {
                appModel.signOut()
            }
            Button("取消", role: .cancel) {}
        } message: {
            Text("退出后需要重新输入服务器地址和登录信息。")
        }
    }

    private var themeBinding: Binding<AppModel.ThemeMode> {
        Binding {
            appModel.themeMode
        } set: {
            appModel.themeMode = $0
        }
    }
}