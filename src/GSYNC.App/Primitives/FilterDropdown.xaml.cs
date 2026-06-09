using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                var optionValue = item ?? string.Empty;
                var option = new Button
                {
                    Content = optionValue.ToString() ?? string.Empty,
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

        var flyoutBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["AppSurfaceAltBrush"],
            BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = itemsPanel,
        };

        _flyout = new Flyout
        {
            Content = flyoutBorder,
        };

        _flyout.ShowAt(DropdownButton);
    }
}
