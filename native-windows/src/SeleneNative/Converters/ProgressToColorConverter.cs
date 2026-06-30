using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SeleneNative.Converters;

/// <summary>
/// Maps a 0..1 progress ratio to a brush color, blending between the system accent and
/// a neutral secondary brush to indicate playback progress visually.
/// </summary>
public sealed class ProgressToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var progress = value switch
        {
            double d => Math.Clamp(d, 0d, 1d),
            float f => Math.Clamp(f, 0f, 1f),
            int i => Math.Clamp(i / 100d, 0d, 1d),
            _ => 0d,
        };

        // Brand color (red-ish) -> muted gray. Tweak palette in one place.
        var start = Color.FromArgb(0xFF, 0xE5, 0x4B, 0x4B);
        var end = Color.FromArgb(0xFF, 0x99, 0x99, 0x99);
        var r = (byte)(start.R + (end.R - start.R) * progress);
        var g = (byte)(start.G + (end.G - start.G) * progress);
        var b = (byte)(start.B + (end.B - start.B) * progress);
        return new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
