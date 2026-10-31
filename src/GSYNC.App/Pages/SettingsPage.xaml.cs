using System.Diagnostics;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Logging;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using GSYNC.Core.Services.Sync;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SettingsPage : Page
{
    private SettingsPageViewModel? _viewModel;
    private AutoPullCoordinator? _autoPull;

    public SettingsPage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _autoPull = App.GetService<AutoPullCoordinator>();
        _autoPull.StatusChanged += AutoPull_StatusChanged;
        RefreshAutoPullStatus();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_autoPull is not null) _autoPull.StatusChanged -= AutoPull_StatusChanged;
        base.OnNavigatedFrom(e);
    }

    private void AutoPull_StatusChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshAutoPullStatus);

    private void RefreshAutoPullStatus()
    {
        if (_autoPull is null || _viewModel is null) return;
        var zh = _viewModel.IsChinese;
        if (!_autoPull.Enabled) { AutoPullStatusText.Text = zh ? "当前状态：已关闭" : "Current status: off"; return; }
        var statuses = _autoPull.Statuses;
        AutoPullStatusText.Text = statuses.Count == 0 ? (zh ? "当前状态：等待检查" : "Current status: waiting to check") :
            string.Join(Environment.NewLine, statuses.Select(status => string.IsNullOrWhiteSpace(status.GameName)
                ? AutoPullStatusFormatter.Format(status, zh) : $"{status.GameName}: {AutoPullStatusFormatter.Format(status, zh)}"));
    }

    private void MainContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var showGroups = e.NewSize.Width >= 850;
        SettingsGroupsColumn.Width = new GridLength(showGroups ? 280 : 0);
        SettingsGroupsPane.Visibility = showGroups ? Visibility.Visible : Visibility.Collapsed;
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
            _ = _viewModel.LoadGamesAsync();
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
        ThemeBehaviorSheet.Title = viewModel.ThemeBehaviorTitle;
        RestoreDefaultsButton.Content = viewModel.RestoreDefaultsText;
        SaveSettingsButton.Content = viewModel.SaveSettingsText;
        OpenLogsDirButton.Content = viewModel.OpenLogsDirText;
        OpenDataDirButton.Content = viewModel.OpenDataDirText;
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

    private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.RestoreDefaults();
        ApplyLocalizedStaticText(_viewModel);

        SettingsFeedback.Text = _viewModel.IsChinese ? "已恢复默认值，保存后生效。" : "Defaults restored. Save to apply them.";
    }

    private void CancelSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        _viewModel.Load();
        SettingsFeedback.Text = _viewModel.IsChinese ? "已取消未保存的修改。" : "Unsaved changes discarded.";
    }

    private void SettingGroup_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border) return;
        border.Background = (Brush)Application.Current.Resources["AppSurfaceRaisedBrush"];
        border.BorderBrush = (Brush)Application.Current.Resources["AppPrimaryBrush"];
    }

    private void SettingGroup_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border) return;
        border.Background = (Brush)Application.Current.Resources["AppSurfaceAltBrush"];
        border.BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"];
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var settings = _viewModel.BuildSettings();
        var savedInChinese = _viewModel.IsChinese;
        var previousLanguage = App.GetService<ILocalizationService>().CurrentLanguageTag;
        try
        {
            _viewModel.SaveSettings();
            SerilogBootstrap.SetMinimumLevel(settings.LogLevel);
            ApplyAppearance(settings);
            SettingsFeedback.Text = savedInChinese ? "设置已保存。" : "Settings saved.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save settings.");
            SettingsFeedback.Text = (savedInChinese ? "保存失败：" : "Could not save: ") + exception.Message;
            return;
        }
        if (!string.Equals(previousLanguage, App.GetService<ILocalizationService>().CurrentLanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            Frame?.Navigate(typeof(SettingsPage));
        }
    }

    private void GroupsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not UiSettingOption option)
        {
            return;
        }

        switch (option.SectionId)
        {
            case "appearance":
                ThemeBehaviorSheet.StartBringIntoView();
                break;
            case "behavior":
                BehaviorSection.StartBringIntoView();
                break;
            case "logging":
                LoggingSection.StartBringIntoView();
                break;
            case "diagnostics":
                DiagnosticsSection.StartBringIntoView();
                break;
            case "games":
                GamesSection.StartBringIntoView();
                break;
        }
    }

    private void OpenLogsDirButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsDirectory = _viewModel?.GetLogsDirectory() ?? new GSYNC.Data.Services.AppPathService().GetLogsDirectory();
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logsDirectory}\""));
        }
        catch
        {
        }
    }

    private void OpenDataDirButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataDirectory = _viewModel?.GetDataDirectory() ?? new GSYNC.Data.Services.AppPathService().GetAppDataRoot();
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dataDirectory}\""));
        }
        catch
        {
        }
    }

    private void GameEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: UiGameMetaRow game })
            Frame?.Navigate(typeof(GameDetailsPage), new GameDetailsEditContext(game.InstanceId));
    }

    private static void ApplyAppearance(AppUiSettings settings)
    {
        AppPalette.Apply(Application.Current.Resources, settings.ThemeMode == AppUiSettings.ThemeLight);
        // Application.RequestedTheme can only be set during app construction; assigning it at
        // runtime throws and crashes the app. Switch the theme on the window root element instead.
        if (App.Current.MainWindow.Content is FrameworkElement root)
        {
            root.RequestedTheme = settings.ThemeMode == AppUiSettings.ThemeLight
                ? ElementTheme.Light
                : ElementTheme.Dark;
        }

        if (Application.Current.Resources is not ResourceDictionary resources)
        {
            return;
        }

        var isCompact = string.Equals(settings.DensityMode, AppUiSettings.DensityCompact, StringComparison.OrdinalIgnoreCase);
        resources["AppPagePadding"] = isCompact ? new Thickness(22, 22, 22, 22) : new Thickness(28, 28, 28, 28);
        resources["AppInlinePadding"] = isCompact ? new Thickness(10, 8, 10, 8) : new Thickness(12, 10, 12, 10);
        resources["AppPanelPadding"] = isCompact ? new Thickness(14, 14, 14, 14) : new Thickness(18, 18, 18, 18);
        resources["AppDenseRowHeight"] = isCompact ? 36d : 42d;
        resources["AppSectionSpacing"] = isCompact ? 14d : 18d;
    }

}
