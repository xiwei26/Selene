import SwiftUI

struct CategoryView: View {
    @Environment(AppModel.self) private var appModel
    @State private var store = CategoryStore()

    let category: MediaCategory

    private let columns = [
        // No maximum: PosterCard scales with Dynamic Type and would overflow a
        // capped column at accessibility text sizes.
        GridItem(.adaptive(minimum: 145), spacing: 18, alignment: .top)
    ]

    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 22) {
                PageHeader(
                    title: category.title,
                    subtitle: category.subtitle,
                    systemImage: category.systemImage
                )

                switch store.phase {
                case .idle, .loading:
                    ProgressView("正在加载\(category.title)…")
                        .frame(maxWidth: .infinity, minHeight: 240)
                case .failed(let message):
                    ContentUnavailableView {
                        Label("无法加载\(category.title)", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(message)
                    } actions: {
                        Button("重试") {
                            Task { await reload(force: true) }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .frame(maxWidth: .infinity, minHeight: 300)
                case .loaded:
                    if store.items.isEmpty {
                        ContentUnavailableView(
                            "暂无内容",
                            systemImage: category.systemImage,
                            description: Text("服务器没有返回\(category.title)数据。")
                        )
                        .frame(maxWidth: .infinity, minHeight: 300)
                    } else {
                        LazyVGrid(columns: columns, alignment: .leading, spacing: 22) {
                            ForEach(store.items) { movie in
                                NavigationLink(value: movie) {
                                    PosterCard(movie: movie)
                                }
                                .buttonStyle(.plain)
                            }
                        }
                    }
                }
            }
            .padding(AppTheme.pagePadding)
            .frame(maxWidth: 1300, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        .refreshable {
            await reload(force: true)
        }
        // Keyed on the category as well as the session so the correct data loads
        // even if this view is reused instead of recreated per category.
        .task(id: LoadKey(session: appModel.session?.id, category: category)) {
            await reload(force: false)
        }
    }

    private func reload(force: Bool) async {
        guard let client = appModel.client else { return }
        await store.load(category: category, using: client, force: force)
    }

    private struct LoadKey: Hashable {
        let session: UUID?
        let category: MediaCategory
    }
}
