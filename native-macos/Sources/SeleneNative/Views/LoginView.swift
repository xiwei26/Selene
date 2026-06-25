import SwiftUI

struct LoginView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var serverURL: String = ""
    @State private var username: String = ""
    @State private var password: String = ""
    @State private var isLoggingIn: Bool = false
    @State private var displayError: String?
    @State private var logoTapCount = 0
    @State private var showsLocalMode = false
    @State private var subscriptionText = ""

    var body: some View {
        ZStack {
            AppBackdrop()

            VStack(spacing: 18) {
                VStack(spacing: 8) {
                    Image(systemName: "sparkles")
                        .font(.system(size: 30, weight: .semibold))
                        .foregroundStyle(.white)
                        .frame(width: 56, height: 56)
                        .background(AppTheme.accentGradient)
                        .clipShape(RoundedRectangle(cornerRadius: AppTheme.radius))
                        .shadow(color: AppTheme.accent.opacity(0.28), radius: 18, x: 0, y: 8)
                        .onTapGesture {
                            logoTapCount += 1
                            if logoTapCount >= 10 {
                                showsLocalMode = true
                            }
                        }

                    Text("Selene")
                        .font(.system(size: 34, weight: .bold))
                        .foregroundStyle(AppTheme.accentGradient)
                    Text("连接你的媒体服务器")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                VStack(alignment: .leading, spacing: 12) {
                    field("服务器地址") {
                        TextField("https://example.com", text: $serverURL)
                            .textFieldStyle(.roundedBorder)
                            .autocorrectionDisabled()
                    }

                    field("用户名") {
                        TextField("用户名", text: $username)
                            .textFieldStyle(.roundedBorder)
                            .autocorrectionDisabled()
                    }

                    field("密码") {
                        SecureField("密码", text: $password)
                            .textFieldStyle(.roundedBorder)
                    }

                    if let error = displayError {
                        Text(error)
                            .font(.caption)
                            .foregroundStyle(.red)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }

                    if showsLocalMode {
                        Divider()
                        field("本地订阅") {
                            TextField("Base58 订阅内容", text: $subscriptionText)
                                .textFieldStyle(.roundedBorder)
                        }
                        Button("进入本地模式") {
                            sessionStore.loginLocal(subscriptionContent: subscriptionText)
                            displayError = sessionStore.errorMessage
                        }
                        .disabled(subscriptionText.isEmpty)
                    }

                    Button {
                        Task { await attemptLogin() }
                    } label: {
                        HStack(spacing: 8) {
                            if isLoggingIn {
                                ProgressView()
                                    .controlSize(.small)
                            } else {
                                Image(systemName: "lock")
                                Text("立即登录")
                                    .fontWeight(.semibold)
                            }
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 8)
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(AppTheme.accent)
                    .keyboardShortcut(.defaultAction)
                    .disabled(isLoggingIn || serverURL.isEmpty || username.isEmpty || password.isEmpty)

                    HStack {
                        Rectangle()
                            .fill(AppTheme.border)
                            .frame(height: 1)
                        Text("需要重新输入？")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        Rectangle()
                            .fill(AppTheme.border)
                            .frame(height: 1)
                    }

                    HStack {
                        Button {
                            serverURL = ""
                            username = ""
                            password = ""
                            displayError = nil
                        } label: {
                            Label("清空表单", systemImage: "xmark.circle")
                        }
                        .buttonStyle(.borderless)
                        .foregroundStyle(AppTheme.accent)
                    }
                }
                .appSurface(padding: 24)
                .frame(width: 430)
            }

            VStack {
                Spacer()
                HStack(spacing: 8) {
                    Text("v1.0.0")
                    Image(systemName: "checkmark.circle")
                    Text("已是最新")
                }
                .font(.caption.weight(.medium))
                .foregroundStyle(AppTheme.accent)
                .padding(.bottom, 18)
            }
        }
        .frame(width: 600, height: showsLocalMode ? 640 : 560)
        .onAppear {
            if let existingURL = sessionStore.session?.serverURL.absoluteString {
                serverURL = existingURL
            }
            if let existingUser = sessionStore.session?.username {
                username = existingUser
            }
        }
    }

    private func field<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(title)
                .font(.caption.weight(.medium))
                .foregroundStyle(.primary.opacity(0.78))
            content()
        }
    }

    private func attemptLogin() async {
        isLoggingIn = true
        displayError = nil

        guard let baseURL = URLNormalizer.normalize(serverURL) else {
            displayError = "服务器地址无效"
            isLoggingIn = false
            return
        }

        let client = ServerAPIClient(baseURL: baseURL)

        do {
            let session = try await client.login(username: username, password: password)
            sessionStore.login(session: session)
        } catch APIError.unauthorized {
            displayError = "用户名或密码错误"
        } catch {
            displayError = error.localizedDescription
        }

        isLoggingIn = false
    }
}
