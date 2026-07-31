import CoreGraphics
import Foundation
import ImageIO
import UIKit

/// Decoded-image cache keyed by URL plus target width. `AsyncImage` keeps no
/// decoded copy, so a recycled `LazyVGrid` cell re-downloads and re-decodes the
/// full-resolution poster every time it scrolls back into view.
/// `Sendable` by way of its members: `NSCache` and `URLSession` are both
/// internally synchronized, and neither stored property is ever reassigned.
final class PosterImageCache: Sendable {
    static let shared = PosterImageCache()

    /// `NSCache` is thread-safe but predates `Sendable` annotation in the SDK.
    nonisolated(unsafe) private let cache = NSCache<NSString, UIImage>()
    private let session: URLSession

    /// Retained so `clear()` can wipe disk responses, not only the memory cache.
    private let urlCache: URLCache

    private init() {
        cache.totalCostLimit = 64 * 1024 * 1024

        let urlCache = URLCache(
            memoryCapacity: 32 * 1024 * 1024,
            diskCapacity: 256 * 1024 * 1024,
            diskPath: "selene-posters"
        )
        self.urlCache = urlCache

        let configuration = URLSessionConfiguration.default
        configuration.urlCache = urlCache
        configuration.requestCachePolicy = .returnCacheDataElseLoad
        session = URLSession(configuration: configuration)
    }

    /// Drops decoded bitmaps and the dedicated poster HTTP cache.
    func clear() {
        cache.removeAllObjects()
        urlCache.removeAllCachedResponses()
    }

    /// Returns a poster downsampled to `maxPixelSize`, decoding at most once per
    /// distinct size. Throws `CancellationError` when the calling task is torn down.
    func poster(for url: URL, maxPixelSize: Int) async throws -> UIImage? {
        let key = "\(url.absoluteString)|\(maxPixelSize)" as NSString
        if let cached = cache.object(forKey: key) {
            return cached
        }

        let (data, _) = try await session.data(from: url)
        try Task.checkCancellation()

        guard let image = Self.downsample(data: data, maxPixelSize: maxPixelSize) else {
            return nil
        }
        if let bytes = image.cgImage.map({ $0.bytesPerRow * $0.height }) {
            cache.setObject(image, forKey: key, cost: bytes)
        } else {
            cache.setObject(image, forKey: key)
        }
        return image
    }

    /// Decodes straight to the display size. Posters arrive far larger than the
    /// cells that show them, so full-size decoding dominates scroll cost.
    private static func downsample(data: Data, maxPixelSize: Int) -> UIImage? {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { return nil }
        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceShouldCacheImmediately: true,
            kCGImageSourceThumbnailMaxPixelSize: maxPixelSize
        ]
        guard let thumbnail = CGImageSourceCreateThumbnailAtIndex(
            source,
            0,
            options as CFDictionary
        ) else { return nil }
        return UIImage(cgImage: thumbnail)
    }
}
