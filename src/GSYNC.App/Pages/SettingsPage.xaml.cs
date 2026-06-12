using System.Diagnostics;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
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
            ThrowIfInitializationForcedToFail("settings");
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
        AppearanceHeader.Title = localization.GetString("Settings.AppearanceTitle");
        AppearanceHeader.Subtitle = viewModel.AppearanceSubtitle;
        ThemeBehaviorSheet.Title = viewModel.ThemeBehaviorTitle;
        RestoreDefaultsButton.Content = localization.GetString("Settings.Button.RestoreDefaults");
        ApplyThemeButton.Content = localization.GetString("Settings.Button.ApplyTheme");
        SaveSettingsButton.Content = localization.GetString("Settings.Button.SaveSettings");
        AddNewTargetButton.Content = localization.GetString("Settings.Button.AddTarget");
    }

    private static void ThrowIfInitializationForcedToFail(string pageKey)
    {
        var configured = Environment.GetEnvironmentVariable("GSYNC_FAIL_PAGE_INIT")?.Trim().ToLowerInvariant();
        if (string.Equals(configured, pageKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Forced {pageKey} page initialization failure for diagnostics.");
        }
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }

    private async void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var defaultLanguage = _viewModel.SupportedLanguages.FirstOrDefault(option => option.Tag == AppUiSettings.DefaultLanguageTag)
            ?? _viewModel.SupportedLanguages[0];
        if (!Equals(_viewModel.SelectedLanguage, defaultLanguage))
        {
            _viewModel.SelectedLanguage = defaultLanguage;
        }

        await ShowInfoAsync(
            _viewModel.IsChinese ? "已恢复默认设置" : "Defaults restored",
            _viewModel.IsChinese
                ? "界面语言已恢复为默认值。"
                : "The interface language has been restored to its default value.");
    }

    private async void ApplyThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowInfoAsync(
            _viewModel?.IsChinese == true ? "主题已应用" : "Theme applied",
            _viewModel?.IsChinese == true ? "当前版本固定使用深色主题。" : "The current build uses the dark theme preset.");
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await ShowInfoAsync(
            _viewModel.IsChinese ? "设置已保存" : "Settings saved",
            _viewModel.IsChinese ? "语言等界面设置已立即生效。" : "Interface settings such as language take effect immediately.");
    }

    private void AddNewTargetButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SyncTargetsPage));
    }

    private void PreviewPrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: UiPreviewRow row })
        {
            OpenPreviewPath(row.Path);
        }
    }

    private void PreviewSecondaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: UiPreviewRow row })
        {
            OpenPreviewPath(row.Path);
        }
    }

    private static void OpenPreviewPath(string pathOrUrl)
    {
        try
        {
            if (Directory.Exists(pathOrUrl))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{pathOrUrl}\""));
            }
            else if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
            }
        }
        catch
        {
        }
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }
}
