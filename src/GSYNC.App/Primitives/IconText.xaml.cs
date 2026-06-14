using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

/// <summary>
/// A compact "icon + label" pair, typically used as the content of a Button so the
/// glyph inherits the button's foreground (keeping hover/selection states intact).
/// Set only <see cref="Glyph"/> for an icon-only button; the label collapses when empty.
/// </summary>
public sealed partial class IconText : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(IconText),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(IconText),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty GlyphSizeProperty = DependencyProperty.Register(
        nameof(GlyphSize),
        typeof(double),
        typeof(IconText),
        new PropertyMetadata(18d));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(IconText),
        new PropertyMetadata(8d));

    public IconText()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateLabelVisibility();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconText control)
        {
            control.UpdateLabelVisibility();
        }
    }

    private void UpdateLabelVisibility()
    {
        if (LabelText is not null)
        {
            LabelText.Visibility = string.IsNullOrEmpty(Text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
