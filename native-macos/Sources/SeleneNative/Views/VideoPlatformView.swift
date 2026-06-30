import SwiftUI

struct VideoPlatformView: View {
    @Bindable var store: VideoPlatformStore
    let kind: VideoPlatformKind
    let onPlayURL: (URL, SearchResult, Int) -> Void
    private let columns = [GridItem(.adaptive(minimum: 260, maximum: 360), spacing: 12)]

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
                TextField("Search \(kind.title)", text: $store.searchQuery)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { Task { await store.search() } }
                Button {
                    Task { await store.search() }
                } label: {
                    Label("Search", systemImage: "magnifyingglass")
                }
                .disabled(store.isLoading)
            }
            if kind == .youtube, !store.regions.isEmpty {
                Picker("Region", selection: $store.selectedRegion) {
                    ForEach(store.regions) { region in
                        Text(region.name).tag(Optional(region))
                    }
                }
                .pickerStyle(.menu)
                .onChange(of: store.selectedRegion) {
                    Task { await store.loadInitial() }
                }
            }
        }
        .padding()
    }

    @ViewBuilder
    private var content: some View {
        if store.isLoading && store.items.isEmpty {
            ProgressView("Loading \(kind.title)...")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if let error = store.errorMessage, store.items.isEmpty {
            ContentUnavailableView("\(kind.title) unavailable", systemImage: "exclamationmark.triangle", description: Text(error))
        } else if store.items.isEmpty {
            ContentUnavailableView("No results", systemImage: "play.rectangle")
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
                if store.nextPageToken != nil || kind == .bilibili {
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
    }

    private func card(_ item: VideoPlatformItem) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            VideoCardView(
                title: item.title,
                poster: item.cover,
                sourceName: item.author ?? kind.title,
                year: item.publishedAt ?? "",
                subtitle: item.views ?? item.duration
            )
            Button {
                if let request = store.directPlayRequest(for: item) {
                    onPlayURL(request.url, request.result, request.index)
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
