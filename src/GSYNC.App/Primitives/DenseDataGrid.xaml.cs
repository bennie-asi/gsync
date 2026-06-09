using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class DenseDataGrid : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(DenseDataGrid),
        new PropertyMetadata("Table"));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(DenseDataGrid),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GridContentProperty = DependencyProperty.Register(
        nameof(GridContent),
        typeof(object),
        typeof(DenseDataGrid),
        new PropertyMetadata(null));

    public static readonly DependencyProperty FooterTextProperty = DependencyProperty.Register(
        nameof(FooterText),
        typeof(string),
        typeof(DenseDataGrid),
        new PropertyMetadata(string.Empty));

    public DenseDataGrid()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object GridContent
    {
        get => GetValue(GridContentProperty);
        set => SetValue(GridContentProperty, value);
    }

    public string FooterText
    {
        get => (string)GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }
}
