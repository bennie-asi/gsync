using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SettingsPage : Page
{
    private SettingsPageViewModel? _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            Log.Information("Initializing SettingsPage.");
            _viewModel = App.GetService<SettingsPageViewModel>();
            DataContext = _viewModel;
            ApplyLocalizedStaticText(_viewModel);
            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            Log.Information("SettingsPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "SettingsPage initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
    }

    private void ApplyLocalizedStaticText(SettingsPageViewModel viewModel)
    {
        var localization = App.GetService<ILocalizationService>();
        TitleTable.Title = localization.GetString("Settings.Title");
        TitleTable.Subtitle = localization.GetString("Settings.Subtitle");
        TitleTable.FooterText = viewModel.ThemeName;
        AppearanceHeader.Title = localization.GetString("Settings.AppearanceTitle");
        AppearanceHeader.Subtitle = localization.GetString("Settings.AppearanceSubtitle");
        ThemeBehaviorSheet.Title = localization.GetString("Settings.ThemeBehaviorTitle");
        RestoreDefaultsButton.Content = localization.GetString("Settings.Button.RestoreDefaults");
        ApplyThemeButton.Content = localization.GetString("Settings.Button.ApplyTheme");
        SaveSettingsButton.Content = localization.GetString("Settings.Button.SaveSettings");
        AddNewTargetButton.Content = localization.GetString("Settings.Button.AddTarget");
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }
}
