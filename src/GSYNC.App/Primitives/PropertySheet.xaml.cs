using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class PropertySheet : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PropertySheet),
        new PropertyMetadata("Properties"));

    public static readonly DependencyProperty PanelContentProperty = DependencyProperty.Register(
        nameof(PanelContent),
        typeof(object),
        typeof(PropertySheet),
        new PropertyMetadata(null));

    public PropertySheet()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }
}
