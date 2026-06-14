using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class Badge : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(Badge),
        new PropertyMetadata("Ready", OnBadgePropertyChanged));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant),
        typeof(string),
        typeof(Badge),
        new PropertyMetadata("Ready", OnBadgePropertyChanged));

    public Badge()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyVariantStyling();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Variant
    {
        get => (string)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    private static void OnBadgePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Badge badge)
        {
            badge.ApplyVariantStyling();
        }
    }

    private void ApplyVariantStyling()
    {
        var brushKey = BadgePalette.ResolveBrushKey(Variant);
        var accentBrush = (Brush)Application.Current.Resources[brushKey];
        BadgeText.Foreground = accentBrush;

        if (string.IsNullOrWhiteSpace(Text))
        {
            Text = Variant ?? "Ready";
        }
    }
}
