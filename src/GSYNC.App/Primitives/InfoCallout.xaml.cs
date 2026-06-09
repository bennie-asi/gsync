using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class InfoCallout : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(InfoCallout),
        new PropertyMetadata("Info", OnCalloutPropertyChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(InfoCallout),
        new PropertyMetadata("Informational message", OnCalloutPropertyChanged));

    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(string),
        typeof(InfoCallout),
        new PropertyMetadata("Info", OnCalloutPropertyChanged));

    public InfoCallout()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySeverityStyling();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Severity
    {
        get => (string)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    private static void OnCalloutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfoCallout callout)
        {
            callout.ApplySeverityStyling();
        }
    }

    private void ApplySeverityStyling()
    {
        var brushKey = Severity?.Trim().ToLowerInvariant() switch
        {
            "warning" => "AppWarningBrush",
            "error" => "AppDangerBrush",
            _ => "AppPrimaryBrush",
        };

        AccentBar.Background = (Brush)Application.Current.Resources[brushKey];
    }
}
