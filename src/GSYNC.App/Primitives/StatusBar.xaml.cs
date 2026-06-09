using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class StatusBar : UserControl
{
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("GSYNC 1.0.2"));

    public static readonly DependencyProperty ConnectionTextProperty = DependencyProperty.Register(
        nameof(ConnectionText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("WebDAV Desktop-Main"));

    public static readonly DependencyProperty StorageTextProperty = DependencyProperty.Register(
        nameof(StorageText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("12.4GB"));

    public static readonly DependencyProperty LogTextProperty = DependencyProperty.Register(
        nameof(LogText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("Log"));

    public static readonly DependencyProperty NetTextProperty = DependencyProperty.Register(
        nameof(NetText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("Net"));

    public static readonly DependencyProperty IdleTextProperty = DependencyProperty.Register(
        nameof(IdleText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("Idle"));

    public StatusBar()
    {
        InitializeComponent();
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string ConnectionText
    {
        get => (string)GetValue(ConnectionTextProperty);
        set => SetValue(ConnectionTextProperty, value);
    }

    public string StorageText
    {
        get => (string)GetValue(StorageTextProperty);
        set => SetValue(StorageTextProperty, value);
    }

    public string LogText
    {
        get => (string)GetValue(LogTextProperty);
        set => SetValue(LogTextProperty, value);
    }

    public string NetText
    {
        get => (string)GetValue(NetTextProperty);
        set => SetValue(NetTextProperty, value);
    }

    public string IdleText
    {
        get => (string)GetValue(IdleTextProperty);
        set => SetValue(IdleTextProperty, value);
    }
}
