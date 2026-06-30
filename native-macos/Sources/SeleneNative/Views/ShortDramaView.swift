import SwiftUI

struct ShortDramaView: View {
    @Bindable var store: ShortDramaStore
    let onPlayURL: (URL, SearchResult, Int) -> Void
    private let columns = [GridItem(.adaptive(minimum: 240, maximum: 320), spacing: 12)]

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            content
        }
        .task {
            if store.items.isEmpty {
                await store.loadInitial()
            }
        }
        .refreshable {
            await store.loadInitial()
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                TextField("Search short dramas", text: $store.searchQuery)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { Task { await store.search() } }
                Button {
                    Task { await store.search() }
                } label: {
                    Label("Search", systemImage: "magnifyingglass")
                }
                .disabled(store.isLoading)
            }
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 8) {
                    Button("Recommended") {
                        store.selectedCategory = nil
                        Task { await store.loadInitial() }
                    }
                    .buttonStyle(.bordered)
                    ForEach(store.categories) { category in
                        Button(category.name) {
                            Task { await store.load(category: category) }
                        }
                        .buttonStyle(.bordered)
                    }
                }
            }
        }
        .padding()
    }

    @ViewBuilder
    private var content: some View {
        if store.isLoading && store.items.isEmpty {
            ProgressView("Loading short dramas...")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if let error = store.errorMessage, store.items.isEmpty {
            ContentUnavailableView("Short drama unavailable", systemImage: "exclamationmark.triangle", description: Text(error))
        } else if store.items.isEmpty {
            ContentUnavailableView("No short dramas", systemImage: "play.rectangle")
        } else {
            ScrollView {
                if let error = store.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal)
                }
                LazyVGrid(columns: columns, spacing: 12) {
                    ForEach(store.items) { item in
                        card(item)
                    }
                }
                .padding()
                Button {
                    Task { await store.loadMore() }
                } label: {
                    Label("Load more", systemImage: "arrow.down.circle")
                }
                .padding(.bottom)
                .disabled(store.isLoading)
            }
        }
    }

    private func card(_ item: ShortDramaItem) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            VideoCardView(
                title: item.name,
                poster: item.cover,
                sourceName: "Short Drama",
                year: item.year ?? "",
                subtitle: item.episodeCount.map { "\($0) episodes" }
            )
            Button {
                Task {
                    if let request = await store.playRequest(for: item, episode: 1) {
                        onPlayURL(request.url, request.result, request.index)
                    }
                }
            } label: {
                Label("Play", systemImage: "play.fill")
            }
        }
        .padding(10)
        .background(.regularMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}
