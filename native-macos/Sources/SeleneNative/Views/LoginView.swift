import SwiftUI

struct LoginView: View {
    @Environment(SessionStore.self) private var sessionStore
    @State private var serverURL: String = ""
    @State private var username: String = ""
    @State private var password: String = ""
    @State private var isLoggingIn: Bool = false
    @State private var displayError: String?

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "play.rectangle.fill")
                .font(.system(size: 48))
                .foregroundStyle(.tint)

            Text("Selene")
                .font(.title)
                .bold()

            VStack(alignment: .leading, spacing: 4) {
                Text("服务器地址")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("https://example.com", text: $serverURL)
                    .textFieldStyle(.roundedBorder)
                    .autocorrectionDisabled()
                    .textInputAutocapitalization(.never)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("用户名")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("用户名", text: $username)
                    .textFieldStyle(.roundedBorder)
                    .textInputAutocapitalization(.never)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text("密码")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                SecureField("密码", text: $password)
                    .textFieldStyle(.roundedBorder)
            }

            if let error = displayError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            Button {
                Task { await attemptLogin() }
            } label: {
                if isLoggingIn {
                    ProgressView()
                        .controlSize(.small)
                } else {
                    Text("登录")
                }
            }
            .keyboardShortcut(.defaultAction)
            .disabled(isLoggingIn || serverURL.isEmpty || username.isEmpty || password.isEmpty)

            Button("清空") {
                serverURL = ""
                username = ""
                password = ""
                displayError = nil
            }
            .buttonStyle(.plain)
            .foregroundStyle(.secondary)

            Spacer()
        }
        .padding(30)
        .frame(width: 360, height: 380)
        .onAppear {
            if let existingURL = sessionStore.session?.serverURL.absoluteString {
                serverURL = existingURL
            }
            if let existingUser = sessionStore.session?.username {
                username = existingUser
            }
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
