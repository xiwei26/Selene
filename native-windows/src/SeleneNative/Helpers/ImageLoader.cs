using Microsoft.UI.Xaml.Media.Imaging;

namespace SeleneNative.Helpers;

/// <summary>
/// Centralized creation of <see cref="BitmapImage"/> instances from remote URLs so
/// the View layer doesn't have to import <c>Microsoft.UI.Xaml.Media.Imaging</c>.
/// </summary>
public static class ImageLoader
{
    public static BitmapImage? TryCreate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new BitmapImage(uri) : null;
    }
}
