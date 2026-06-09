using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class KeyValueList : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(KeyValueList),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ContentMaxHeightProperty = DependencyProperty.Register(
        nameof(ContentMaxHeight),
        typeof(double),
        typeof(KeyValueList),
        new PropertyMetadata(double.PositiveInfinity));

    public KeyValueList()
    {
        InitializeComponent();
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double ContentMaxHeight
    {
        get => (double)GetValue(ContentMaxHeightProperty);
        set => SetValue(ContentMaxHeightProperty, value);
    }
}
