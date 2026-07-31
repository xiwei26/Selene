import SwiftUI

struct LoginView: View {
    private enum Field: Hashable {
        case server
        case username
        case password
    }

    @Environment(AppModel.self) private var appModel
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @State private var serverAddress = ""
    @State private var username = ""
    @State private var password = ""
    @State private var isSigningIn = false
    @State private var logoTapCount = 0
    @State private var showsLocalMode = false
    @State private var subscriptionContent = ""
    @FocusState private var focusedField: Field?
    @ScaledMetric(relativeTo: .largeTitle) private var logoSize: CGFloat = 84

    var body: some View {
        ZStack {
            AppBackground()

            // The form must stay reachable once the keyboard covers most of a
            // compact screen, so the whole layout scrolls. minHeight keeps the
            // centered look while there is room to spare.
            GeometryReader { proxy in
                ScrollView {
                    ViewThatFits(in: .horizontal) {
                        HStack(spacing: 64) {
                            brand
                                .frame(maxWidth: 420)
                            form
                                .frame(maxWidth: 440)
                        }

                        VStack(spacing: 34) {
                            brand
                            form
                        }
                    }
                    .padding(32)
                    .frame(maxWidth: 980)
                    .frame(maxWidth: .infinity, minHeight: proxy.size.height)
                }
                .scrollDismissesKeyboard(.interactively)
            }
        }
        .onAppear {
            // Raising the keyboard at launch on iPhone hides the form behind it.
            guard horizontalSizeClass == .regular else { return }
            focusedField = .server
        }
    }

    private var brand: some View {
        VStack(alignment: horizontalSizeClass == .regular ? .leading : .center, spacing: 18) {
            Image(systemName: "moon.stars.fill")
                .font(.largeTitle.weight(.semibold))
                .foregroundStyle(.white)
                .frame(width: logoSize, height: logoSize)
                .background(AppTheme.accentGradient, in: RoundedRectangle(cornerRadius: 24))
                .shadow(color: AppTheme.accent.opacity(0.25), radius: 24, y: 12)
                .onTapGesture(perform: revealLocalMode)

            VStack(alignment: horizontalSizeClass == .regular ? .leading : .center, spacing: 8) {
                Text("Selene")
                    .font(.largeTitle.bold())
                    .fontDesign(.rounded)
                Text("为 iPad 重新设计的原生 LunaTV 客户端")
                    .font(.title3)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(horizontalSizeClass == .regular ? .leading : .center)
            }
        }
    }

    private var form: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 5) {
                Text("连接媒体服务器")
                    .font(.title2.bold())
                Text("登录信息仅保存在此设备。")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            TextField("服务器地址，例如 https://luna.example.com", text: $serverAddress)
                .textContentType(.URL)
                .keyboardType(.URL)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .focused($focusedField, equals: .server)
                .submitLabel(.next)
                .onSubmit { focusedField = .username }

            TextField("用户名", text: $username)
                .textContentType(.username)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .focused($focusedField, equals: .username)
                .submitLabel(.next)
                .onSubmit { focusedField = .password }

            SecureField("密码", text: $password)
                .textContentType(.password)
                .focused($focusedField, equals: .password)
                .submitLabel(.go)
                .onSubmit { signIn() }

            if showsLocalMode {
                Divider()
                TextField("Base58 本地订阅内容", text: $subscriptionContent, axis: .vertical)
                    .lineLimit(2...5)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()

                Button {
                    appModel.signInLocally(subscription: subscriptionContent)
                } label: {
                    Label("进入本地模式", systemImage: "externaldrive")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .disabled(subscriptionContent.isEmpty)
            }

            if let error = appModel.authenticationError {
                Label(error, systemImage: "exclamationmark.triangle.fill")
                    .font(.callout)
                    .foregroundStyle(.red)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Button(action: signIn) {
                HStack {
                    if isSigningIn {
                        ProgressView()
                            .tint(.white)
                    } else {
                        Image(systemName: "arrow.right.circle.fill")
                    }
                    Text(isSigningIn ? "正在连接…" : "登录")
                        .fontWeight(.semibold)
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 8)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(isSigningIn || serverAddress.isEmpty || username.isEmpty || password.isEmpty)
        }
        .textFieldStyle(.roundedBorder)
        .appSurface(padding: 24)
    }

    private func signIn() {
        guard !isSigningIn else { return }
        focusedField = nil
        isSigningIn = true
        Task {
            await appModel.signIn(
                serverAddress: serverAddress,
                username: username,
                password: password
            )
            isSigningIn = false
        }
    }

    private func revealLocalMode() {
        logoTapCount += 1
        if logoTapCount >= 10 {
            withAnimation {
                showsLocalMode = true
            }
        }
    }
}