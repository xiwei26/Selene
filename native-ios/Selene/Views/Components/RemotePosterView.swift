import SwiftUI
import UIKit

struct RemotePosterView: View {
    let urlString: String

    @Environment(\.displayScale) private var displayScale

    @State private var image: UIImage?
    @State private var didFail = false
    @State private var renderedWidth: CGFloat = 0
    /// Which URL produced `image`, so a recycled cell does not show the previous
    /// row's poster while the new one loads.
    @State private var loadedURL: URL?

    private var url: URL? {
        URL(string: urlString)
    }

    var body: some View {
        content
            .aspectRatio(2 / 3, contentMode: .fit)
            .background(Color.secondary.opacity(0.08))
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .stroke(Color.primary.opacity(0.08), lineWidth: 1)
            }
            // Measured in a background so the reader cannot influence the
            // aspect-ratio layout the call sites depend on.
            .background {
                GeometryReader { proxy in
                    Color.clear
                        .onAppear { renderedWidth = proxy.size.width }
                        .onChange(of: proxy.size.width) { _, width in
                            renderedWidth = width
                        }
                }
            }
            // `.task(id:)` cancels the in-flight load when a grid cell is recycled
            // onto a different poster.
            .task(id: TargetKey(url: url, width: targetPixelWidth)) {
                await load()
            }
            // Every call site pairs the poster with a text label that already names
            // the item, so an unlabeled image here would only add noise.
            .accessibilityHidden(true)
    }

    @ViewBuilder
    private var content: some View {
        if let image {
            Image(uiImage: image)
                .resizable()
                .scaledToFill()
        } else if didFail || url == nil {
            placeholder
        } else {
            ZStack {
                Color.secondary.opacity(0.08)
                ProgressView()
                    .controlSize(.small)
            }
        }
    }

    private var targetPixelWidth: Int {
        Int((renderedWidth * displayScale).rounded())
    }

    /// Waits for the measured width so the poster is decoded once, at the size it
    /// will actually be drawn.
    private func load() async {
        if loadedURL != url {
            image = nil
            didFail = false
            loadedURL = url
        }
        guard let url, targetPixelWidth > 0 else { return }
        do {
            let loaded = try await PosterImageCache.shared.poster(
                for: url,
                maxPixelSize: targetPixelWidth
            )
            guard !Task.isCancelled else { return }
            if let loaded {
                image = loaded
                didFail = false
            } else {
                didFail = true
            }
        } catch {
            // A recycled cell is not a load failure.
            guard !isCancellation(error) else { return }
            didFail = true
        }
    }

    private var placeholder: some View {
        ZStack {
            Color.secondary.opacity(0.08)
            Image(systemName: "photo")
                .font(.title2)
                .foregroundStyle(.tertiary)
        }
    }
}

/// Reloads only when the URL changes or the cell grows enough to need more pixels,
/// so Dynamic Type resizes do not thrash the decoder.
private struct TargetKey: Equatable {
    let url: URL?
    let bucket: Int

    init(url: URL?, width: Int) {
        self.url = url
        bucket = width / 64
    }
}
