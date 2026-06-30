using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SeleneNative.Converters;

/// <summary>
/// Maps a boolean to <see cref="Visibility"/>. Pass <c>True</c> to hide, <c>False</c> to show by default;
/// use the converter parameter <c>invert</c> to flip.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is bool b && b;
        if (parameter is string text &&
            string.Equals(text, "invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is Visibility visibility && visibility == Visibility.Visible;
        if (parameter is string text &&
            string.Equals(text, "invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible;
    }
}
