import SwiftUI

struct CategoryView: View {
    @Environment(AppModel.self) private var appModel
    @State private var store = CategoryStore()

    let category: MediaCategory

    private let columns = [
        GridItem(.adaptive(minimum: 145, maximum: 190), spacing: 18, alignment: .top)
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
                    ContentUnavailableView(
                        "无法加载\(category.title)",
                        systemImage: "wifi.exclamationmark",
                        description: Text(message)
                    )
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
            guard let client = appModel.client else { return }
            await store.load(category: category, using: client, force: true)
        }
        .task(id: appModel.session?.id) {
            guard let client = appModel.client else { return }
            await store.load(category: category, using: client)
        }
    }
}