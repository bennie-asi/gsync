using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class InspectorPanel : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(InspectorPanel),
        new PropertyMetadata("Inspector"));

    public static readonly DependencyProperty PanelContentProperty = DependencyProperty.Register(
        nameof(PanelContent),
        typeof(object),
        typeof(InspectorPanel),
        new PropertyMetadata(null));

    public InspectorPanel()
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
