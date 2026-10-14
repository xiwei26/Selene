using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace SeleneNative;

/// <summary>
/// Binds imperative UI to compiled ThemeResource references in the shell's
/// visual tree, so requested themes and high contrast update existing pages.
/// </summary>
internal static class ThemeResources
{
    private static ThemeResourcePalette? _palette;

    public static void Initialize(Grid root)
    {
        if (_palette is not null)
        {
            return;
        }

        _palette = new ThemeResourcePalette
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            IsTabStop = false,
        };
        root.Children.Add(_palette);
    }

    public static void Set(FrameworkElement element, DependencyProperty property, string resourceKey)
    {
        var palette = _palette ?? throw new InvalidOperationException("Initialize theme resources before building pages.");
        element.SetBinding(property, new Binding
        {
            Source = palette.BrushSource(resourceKey),
            Path = new PropertyPath(nameof(Border.Background)),
            Mode = BindingMode.OneWay,
        });
    }
}
