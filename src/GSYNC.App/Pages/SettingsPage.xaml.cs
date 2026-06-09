using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        var viewModel = App.GetService<SettingsPageViewModel>();
        DataContext = viewModel;
        Loaded += (_, _) => ApplyLocalizedStaticText(viewModel);
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
}
