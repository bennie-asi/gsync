using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class ToolbarFilterRow : UserControl
{
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText),
        typeof(string),
        typeof(ToolbarFilterRow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(
        nameof(SearchPlaceholder),
        typeof(string),
        typeof(ToolbarFilterRow),
        new PropertyMetadata("Search library"));

    public static readonly DependencyProperty FilterPlaceholderProperty = DependencyProperty.Register(
        nameof(FilterPlaceholder),
        typeof(string),
        typeof(ToolbarFilterRow),
        new PropertyMetadata("Status"));

    public static readonly DependencyProperty FilterItemsProperty = DependencyProperty.Register(
        nameof(FilterItems),
        typeof(IEnumerable),
        typeof(ToolbarFilterRow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedFilterProperty = DependencyProperty.Register(
        nameof(SelectedFilter),
        typeof(object),
        typeof(ToolbarFilterRow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent),
        typeof(object),
        typeof(ToolbarFilterRow),
        new PropertyMetadata(null));

    public ToolbarFilterRow()
    {
        InitializeComponent();
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public string FilterPlaceholder
    {
        get => (string)GetValue(FilterPlaceholderProperty);
        set => SetValue(FilterPlaceholderProperty, value);
    }

    public IEnumerable FilterItems
    {
        get => (IEnumerable)GetValue(FilterItemsProperty);
        set => SetValue(FilterItemsProperty, value);
    }

    public object SelectedFilter
    {
        get => GetValue(SelectedFilterProperty);
        set => SetValue(SelectedFilterProperty, value);
    }

    public object ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
