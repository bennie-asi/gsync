using GSYNC.App.Infrastructure.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class AppNavRail : UserControl
{
    public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
        nameof(SelectedKey),
        typeof(string),
        typeof(AppNavRail),
        new PropertyMetadata("library", OnSelectedKeyChanged));

    public static readonly DependencyProperty AppVersionProperty = DependencyProperty.Register(
        nameof(AppVersion),
        typeof(string),
        typeof(AppNavRail),
        new PropertyMetadata("v1.0.2", OnAppVersionChanged));

    public event EventHandler<NavigationRequestedEventArgs>? NavigationRequested;

    public AppNavRail()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyAppVersion();
            ApplySelectionState();
        };
    }

    public string SelectedKey
    {
        get => (string)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    public string AppVersion
    {
        get => (string)GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }

    public void ApplyLocalization(ILocalizationService localizationService)
    {
        LibraryTextBlock.Text = localizationService.GetString("MainWindow.Nav.Library");
        TargetsTextBlock.Text = localizationService.GetString("MainWindow.Nav.Targets");
        VariablesTextBlock.Text = localizationService.GetString("MainWindow.Nav.Variables");
        HistoryTextBlock.Text = localizationService.GetString("MainWindow.Nav.History");
        SettingsTextBlock.Text = localizationService.GetString("MainWindow.Nav.Settings");
    }

    private static void OnSelectedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppNavRail rail)
        {
            rail.ApplySelectionState();
        }
    }

    private static void OnAppVersionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AppNavRail rail)
        {
            rail.ApplyAppVersion();
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string key)
        {
            NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs(key));
        }
    }

    private void ApplyAppVersion()
    {
        if (AppVersionTextBlock is not null)
        {
            AppVersionTextBlock.Text = AppVersion;
        }
    }

    private void ApplySelectionState()
    {
        ApplyButtonState(LibraryButton, SelectedKey == "library");
        ApplyButtonState(TargetsButton, SelectedKey == "targets");
        ApplyButtonState(VariablesButton, SelectedKey == "variables");
        ApplyButtonState(HistoryButton, SelectedKey == "history");
        ApplyButtonState(SettingsButton, SelectedKey == "settings");
    }

    private static void ApplyButtonState(Button button, bool isSelected)
    {
        var resources = Application.Current.Resources;
        button.Background = (Brush)resources[isSelected ? "AppSurfaceRaisedBrush" : "AppSurfaceAltBrush"];
        button.Foreground = (Brush)resources[isSelected ? "AppTextPrimaryBrush" : "AppTextSecondaryBrush"];
        button.Opacity = isSelected ? 1 : 0.88;
    }
}

public sealed class NavigationRequestedEventArgs(string key) : EventArgs
{
    public string Key { get; } = key;
}
