using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class SearchField : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SearchField),
        new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(SearchField),
        new PropertyMetadata("Search"));

    public SearchField()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private bool _isComposing;

    private static void OnTextPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var field = (SearchField)sender;
        var value = e.NewValue as string ?? string.Empty;
        if (!field._isComposing && field.SearchTextBox is not null && field.SearchTextBox.Text != value)
            field.SearchTextBox.Text = value;
    }

    private void SearchTextBox_TextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args) => _isComposing = true;

    private void SearchTextBox_TextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args)
    {
        _isComposing = false;
        Text = sender.Text;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isComposing && Text != SearchTextBox.Text) Text = SearchTextBox.Text;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyUnfocusedVisualState();
    }

    private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        SearchContainer.Background = (Brush)Application.Current.Resources["AppSurfaceAltBrush"];
        SearchContainer.BorderBrush = (Brush)Application.Current.Resources["AppPrimaryBrush"];
        SearchIcon.Foreground = (Brush)Application.Current.Resources["AppTextSecondaryBrush"];
    }

    private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyUnfocusedVisualState();
    }

    private void ApplyUnfocusedVisualState()
    {
        SearchContainer.Background = (Brush)Application.Current.Resources["AppInputBrush"];
        SearchContainer.BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"];
        SearchIcon.Foreground = (Brush)Application.Current.Resources["AppTextDimBrush"];
    }
}
