using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Primitives;

public sealed partial class StatusBar : UserControl
{
    public static readonly DependencyProperty WebDavStatusTextProperty = DependencyProperty.Register(
        nameof(WebDavStatusText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("WebDAV: Online"));

    public static readonly DependencyProperty LocalStatusTextProperty = DependencyProperty.Register(
        nameof(LocalStatusText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("Local: Ready"));

    public static readonly DependencyProperty TargetNameTextProperty = DependencyProperty.Register(
        nameof(TargetNameText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("WebDAV-Main"));

    public static readonly DependencyProperty SyncStatusTextProperty = DependencyProperty.Register(
        nameof(SyncStatusText),
        typeof(string),
        typeof(StatusBar),
        new PropertyMetadata("Sync: Ready"));

    public StatusBar()
    {
        InitializeComponent();
    }

    public string WebDavStatusText
    {
        get => (string)GetValue(WebDavStatusTextProperty);
        set => SetValue(WebDavStatusTextProperty, value);
    }

    public string LocalStatusText
    {
        get => (string)GetValue(LocalStatusTextProperty);
        set => SetValue(LocalStatusTextProperty, value);
    }

    public string TargetNameText
    {
        get => (string)GetValue(TargetNameTextProperty);
        set => SetValue(TargetNameTextProperty, value);
    }

    public string SyncStatusText
    {
        get => (string)GetValue(SyncStatusTextProperty);
        set => SetValue(SyncStatusTextProperty, value);
    }
}
