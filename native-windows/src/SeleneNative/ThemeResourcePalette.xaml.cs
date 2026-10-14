using Microsoft.UI.Xaml.Controls;

namespace SeleneNative;

public sealed partial class ThemeResourcePalette : UserControl
{
    public ThemeResourcePalette()
    {
        InitializeComponent();
    }

    internal Border BrushSource(string key) => key switch
    {
        "LayerFillColorDefaultBrush" => LayerSurface,
        "CardBackgroundFillColorDefaultBrush" => CardSurface,
        "CardStrokeColorDefaultBrush" => CardStroke,
        "ControlFillColorSecondaryBrush" => SecondarySurface,
        "TextFillColorPrimaryBrush" => PrimaryText,
        "TextFillColorSecondaryBrush" => SecondaryText,
        _ => throw new ArgumentException($"Unsupported theme resource: {key}", nameof(key)),
    };
}
