using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class ActivityFeed : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(ActivityFeed),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ContentHeightProperty = DependencyProperty.Register(
        nameof(ContentHeight),
        typeof(double),
        typeof(ActivityFeed),
        new PropertyMetadata(220d));

    public ActivityFeed()
    {
        InitializeComponent();
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double ContentHeight
    {
        get => (double)GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }
}
