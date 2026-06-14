using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class FilterDropdown : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(FilterDropdown),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(FilterDropdown),
        new PropertyMetadata(null, OnSelectionChanged));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(FilterDropdown),
        new PropertyMetadata("Select", OnSelectionChanged));

    private Flyout? _flyout;

    public FilterDropdown()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateDisplayText();
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FilterDropdown dropdown)
        {
            dropdown.UpdateDisplayText();
        }
    }

    private void UpdateDisplayText()
    {
        DisplayTextBlock.Text = SelectedItem?.ToString() ?? Placeholder;
    }

    private void DropdownButton_OnClick(object sender, RoutedEventArgs e)
    {
        var itemsPanel = new StackPanel { Spacing = 4, MinWidth = 180 };

        var clearOption = new Button
        {
            Content = Placeholder,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
            MinWidth = 164,
        };
        clearOption.Click += (_, _) =>
        {
            SelectedItem = null;
            _flyout?.Hide();
        };
        itemsPanel.Children.Add(clearOption);

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                var optionValue = item ?? string.Empty;
                var optionText = optionValue.ToString() ?? string.Empty;

                // The clear option above already represents the placeholder/"all" choice,
                // so skip any source item that duplicates it to avoid showing it twice.
                if (string.Equals(optionText, Placeholder, StringComparison.Ordinal))
                {
                    continue;
                }

                var option = new Button
                {
                    Content = optionText,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
                    MinWidth = 164,
                };

                option.Click += (_, _) =>
                {
                    SelectedItem = optionValue;
                    _flyout?.Hide();
                };

                itemsPanel.Children.Add(option);
            }
        }

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = itemsPanel,
        };

        var flyoutBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["AppSurfaceAltBrush"],
            BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = scrollViewer,
        };

        _flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            Content = flyoutBorder,
        };

        _flyout.ShowAt(DropdownButton);
    }
}
