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
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Updates;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SettingsPage : Page
{
    private SettingsPageViewModel? _viewModel;
    private AutoPullCoordinator? _autoPull;
    private AppUpdateService? _updates;
    private AppUpdateInstaller? _installer;
    private CancellationTokenSource? _downloadCancellation;
    private string? _downloadedUpdate;

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
        _updates = App.GetService<AppUpdateService>();
        _updates.Changed += Updates_Changed;
        _installer = App.GetService<AppUpdateInstaller>();
        _installer.Changed += Updates_Changed;
        RefreshUpdateStatus();
        RefreshAutoPullStatus();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_autoPull is not null) _autoPull.StatusChanged -= AutoPull_StatusChanged;
        if (_updates is not null) _updates.Changed -= Updates_Changed;
        if (_installer is not null) _installer.Changed -= Updates_Changed;
        _downloadCancellation?.Cancel();
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
            InitializeDesktopFeatures();
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

    private void InitializeDesktopFeatures()
    {
        var zh = _viewModel?.IsChinese == true;
        StartupSection.Title = zh ? "启动" : "Startup";
        StartupDescription.Text = zh ? "登录 Windows 后启动 GSYNC；自动同步仍按独立设置运行。" : "Start GSYNC after signing in to Windows. Automatic sync uses its own setting.";
        EnableStartupButton.Content = zh ? "启用 / 使用当前位置" : "Enable / use this location";
        DisableStartupButton.Content = zh ? "关闭自启" : "Disable startup";
        WindowsStartupButton.Content = zh ? "Windows 启动设置" : "Windows startup settings";
        UpdatesSection.Title = zh ? "关于与更新" : "About and updates";
        VersionText.Text = (zh ? "当前版本：" : "Current version: ") + DesktopFeatures.CurrentVersion.Split('+')[0];
        UpdateStatus.Text = zh ? "尚未检查应用更新。" : "Application updates have not been checked.";
        CheckUpdatesButton.Content = zh ? "检查更新" : "Check for updates";
        ReleasesButton.Content = zh ? "前往 GitHub 发行页" : "Open GitHub releases";
        AutoCheckToggle.Header = zh ? "启动时自动检查更新（保存后生效，每天最多一次）" : "Check for updates on startup (save to apply, at most once a day)";
        AutoCheckToggle.OnContent = zh ? "开" : "On";
        AutoCheckToggle.OffContent = zh ? "关" : "Off";
        DownloadUpdateButton.Content = zh ? "下载更新" : "Download update";
        CancelUpdateButton.Content = zh ? "取消下载" : "Cancel download";
        OpenUpdateFolderButton.Content = zh ? "打开下载位置" : "Open download location";
        InstallUpdateButton.Content = zh ? "退出并安装更新" : "Exit and install update";
        CancelInstallButton.Content = zh ? "取消等待" : "Cancel waiting";
        RefreshStartupStatus();
    }

    private void RefreshStartupStatus()
    {
        var zh = _viewModel?.IsChinese == true;
        try
        {
            var command = DesktopFeatures.StartupCommand();
            if (command is null) StartupStatus.Text = zh ? "当前用户未登记开机自启。" : "Startup is not registered for this user.";
            else if (!string.Equals(command, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase))
                StartupStatus.Text = (zh ? "启动路径与当前程序不一致：" : "Startup points to a different location: ") + command;
            else StartupStatus.Text = zh ? "已登记启动项；实际启动状态由 Windows 管理。" : "Startup is registered; Windows controls whether it runs.";
        }
        catch (Exception ex) { StartupStatus.Text = (zh ? "无法读取启动状态：" : "Cannot read startup status: ") + ex.Message; }
    }

    private async void EnableStartup_Click(object sender, RoutedEventArgs e)
    {
        var zh = _viewModel?.IsChinese == true;
        try
        {
            var existing = DesktopFeatures.StartupCommand();
            if (existing is not null && !string.Equals(existing, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase))
            {
                var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = zh ? "更新启动路径？" : "Update startup location?",
                    Content = (zh ? "当前登记：" : "Registered: ") + existing + Environment.NewLine + (zh ? "使用当前程序位置替换它？" : "Replace it with the current location?"),
                    PrimaryButtonText = zh ? "使用当前位置" : "Use current location", CloseButtonText = zh ? "取消" : "Cancel", DefaultButton = ContentDialogButton.Close };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            DesktopFeatures.SetStartup(true);
            RefreshStartupStatus();
        }
        catch (Exception ex) { StartupStatus.Text = (zh ? "无法设置自启：" : "Cannot configure startup: ") + ex.Message; }
    }

    private async void DisableStartup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = DesktopFeatures.StartupCommand();
            if (existing is not null && !string.Equals(existing, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase))
            {
                var zh = _viewModel?.IsChinese == true;
                var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = zh ? "移除此启动项？" : "Remove this startup entry?",
                    Content = (zh ? "该启动项指向其他位置：" : "This entry points to another location: ") + existing,
                    PrimaryButtonText = zh ? "移除" : "Remove", CloseButtonText = zh ? "取消" : "Cancel", DefaultButton = ContentDialogButton.Close };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            DesktopFeatures.SetStartup(false);
            RefreshStartupStatus();
        }
        catch (Exception ex) { StartupStatus.Text = (_viewModel?.IsChinese == true ? "无法关闭自启：" : "Cannot disable startup: ") + ex.Message; }
    }

    private async void WindowsStartup_Click(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")); }
        catch (Exception ex) { StartupStatus.Text = ex.Message; }
    }

    private async void Releases_Click(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri(DesktopFeatures.ReleasesUrl)); }
        catch (Exception ex) { UpdateStatus.Text = ex.Message; }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_updates is not null) await _updates.CheckAsync(false, App.Current.LifetimeToken);
        }
        catch (OperationCanceledException) when (App.Current.LifetimeToken.IsCancellationRequested) { }
        catch (Exception ex) { UpdateStatus.Text = (_viewModel?.IsChinese == true ? "检查失败：" : "Update check failed: ") + ex.Message; }
    }

    private void Updates_Changed(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshUpdateStatus);

    private void RefreshUpdateStatus()
    {
        if (_updates is null) return;
        var zh = _viewModel?.IsChinese == true;
        var result = _updates.Snapshot;
        if (_downloadCancellation is null && _updates.LastDownload is { } download && File.Exists(download.Path) &&
            (result.Version is null || string.Equals(result.Version, download.Version, StringComparison.Ordinal)))
        {
            _downloadedUpdate = download.Path;
            OpenUpdateFolderButton.Visibility = Visibility.Visible;
            if (string.IsNullOrWhiteSpace(DownloadStatus.Text))
                DownloadStatus.Text = zh ? $"本次运行已下载版本 {download.Version}。" : $"Version {download.Version} was downloaded in this session.";
        }
        else if (_downloadCancellation is null && result.Version is not null && _updates.LastDownload is { } previous &&
                 !string.Equals(result.Version, previous.Version, StringComparison.Ordinal))
        {
            _downloadedUpdate = null;
            OpenUpdateFolderButton.Visibility = Visibility.Collapsed;
            DownloadStatus.Text = string.Empty;
        }
        else if (_downloadCancellation is null && _downloadedUpdate is not null && !File.Exists(_downloadedUpdate))
        {
            _downloadedUpdate = null;
            OpenUpdateFolderButton.Visibility = Visibility.Collapsed;
            DownloadStatus.Text = zh ? "下载的文件已不存在，请重新下载。" : "The downloaded file is no longer available. Download it again.";
        }
        UpdateStatus.Text = result.State switch
        {
            "checking" => zh ? "正在检查更新…" : "Checking for updates…",
            "available" => zh ? $"发现新版本 {result.Version}。" : $"Version {result.Version} is available.",
            "current" => zh ? "上次检查时已是最新版本。" : "You were up to date at the last check.",
            "none" => zh ? "未找到公开发行版，或无法访问此仓库。" : "No public release was found, or the repository is inaccessible.",
            "limited" => zh ? "GitHub 暂时限制了请求，请稍后重试。" : "GitHub has rate-limited requests. Try again later.",
            "noasset" => zh ? "发现新版本，但没有适用于 Windows x64 的更新包。" : "A new version is available, but no Windows x64 package was found.",
            "unverified" => zh ? "此版本无法在应用内校验，请前往 GitHub 下载。" : "This release cannot be verified in the app. Download it from GitHub.",
            "error" => (zh ? "检查失败：" : "Update check failed: ") + result.Message,
            "invalid" => zh ? "无法比较版本，请查看 GitHub 发行页。" : "Cannot compare versions. Check GitHub releases.",
            _ => zh ? "尚未检查应用更新。" : "Application updates have not been checked."
        };
        if (result.LastAttempt is { } last) UpdateStatus.Text += (zh ? "\n上次检查：" : "\nLast checked: ") + last.ToLocalTime().ToString("g");
        if (result.LastSuccess is { } success && result.LastSuccess != result.LastAttempt)
            UpdateStatus.Text += (zh ? "\n上次成功检查：" : "\nLast successful check: ") + success.ToLocalTime().ToString("g");
        ReleaseNotes.Text = result.Notes ?? string.Empty;
        ReleaseNotesContainer.Visibility = string.IsNullOrWhiteSpace(ReleaseNotes.Text) ? Visibility.Collapsed : Visibility.Visible;
        var install = _installer?.Status ?? new InstallStatus("idle");
        var installing = install.State is "preparing" or "waiting" or "starting";
        InstallStatusText.Text = install.State switch
        {
            "preparing" => zh ? "正在准备已签名的更新…" : "Preparing the signed update…",
            "waiting" => zh ? "等待同步和恢复任务结束后自动安装；你可以继续使用应用。" : "Waiting for sync and restore tasks to finish before installing; you can keep using the app.",
            "starting" => zh ? "任务已结束，正在退出并启动更新程序…" : "Tasks are complete. Closing the app and starting the updater…",
            "cancelled" => zh ? "已取消等待安装。" : "Update installation was cancelled.",
            "error" => (zh ? "无法安装更新：" : "Cannot install update: ") + install.Message,
            _ => string.Empty,
        };
        var lastResult = UpdateResultStore.Read(App.GetService<IAppPathService>().GetAppDataRoot());
        LastUpdateResultText.Text = lastResult?.State switch
        {
            "installed" => zh ? $"版本 {lastResult.Version} 已安装并通过启动检查。" : $"Version {lastResult.Version} was installed and passed its startup check.",
            "rolled-back" => zh ? $"版本 {lastResult.Version} 更新失败，已恢复原版本：{LocalizeUpdateError(lastResult.Message)}" : $"Update {lastResult.Version} failed and the previous version was restored: {lastResult.Message}",
            "recovery-required" => zh ? $"更新未能自动恢复，备份保存在 {lastResult.RecoveryDirectory}：{lastResult.Message}" : $"Automatic recovery needs attention. Backups are at {lastResult.RecoveryDirectory}: {lastResult.Message}",
            "error" => zh ? $"更新未完成：{lastResult.Message}" : $"Update did not finish: {lastResult.Message}",
            _ => string.Empty,
        };
        DownloadStatus.Visibility = string.IsNullOrWhiteSpace(DownloadStatus.Text) ? Visibility.Collapsed : Visibility.Visible;
        InstallStatusText.Visibility = string.IsNullOrWhiteSpace(InstallStatusText.Text) ? Visibility.Collapsed : Visibility.Visible;
        LastUpdateResultText.Visibility = string.IsNullOrWhiteSpace(LastUpdateResultText.Text) ? Visibility.Collapsed : Visibility.Visible;
        CheckUpdatesButton.IsEnabled = result.State != "checking" && _downloadCancellation is null && !installing;
        DownloadUpdateButton.Visibility = result.State == "available" && _downloadCancellation is null && !installing ? Visibility.Visible : Visibility.Collapsed;
        InstallUpdateButton.Visibility = _downloadedUpdate is not null && _updates.LastDownload?.IsSigned == true && _downloadCancellation is null && !installing ? Visibility.Visible : Visibility.Collapsed;
        CancelInstallButton.Visibility = install.State is "preparing" or "waiting" ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string LocalizeUpdateError(string message) => message switch
    {
        "The updated program failed its startup check." => "新版本未通过启动检查。",
        "The updated program did not complete its startup check." => "新版本启动检查超时。",
        "An interrupted update was rolled back." => "中断的更新已恢复。",
        _ => message,
    };

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updates is null || _downloadCancellation is not null) return;
        var zh = _viewModel?.IsChinese == true;
        _downloadedUpdate = null;
        OpenUpdateFolderButton.Visibility = Visibility.Collapsed;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(App.Current.LifetimeToken);
        _downloadCancellation = cancellation;
        CancelUpdateButton.Visibility = Visibility.Visible;
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.Value = 0;
        RefreshUpdateStatus();
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (_downloadCancellation != cancellation || cancellation.IsCancellationRequested) return;
            UpdateProgress.Value = 100d * p.Downloaded / p.Total;
            DownloadStatus.Text = $"{p.Downloaded / 1048576d:F1} / {p.Total / 1048576d:F1} MB";
            DownloadStatus.Visibility = Visibility.Visible;
        });
        try
        {
            _downloadedUpdate = await _updates.DownloadAsync(progress, cancellation.Token);
            DownloadStatus.Text = _updates.LastDownload?.IsSigned == true
                ? (zh ? "下载完成，发行签名和文件完整性校验通过。" : "Download complete. The release signature and file integrity were verified.")
                : (zh ? "下载完成，但该发行版没有可信签名。请退出后解压到新文件夹手动更新。" : "Download complete, but this release has no trusted signature. Exit and extract it to a new folder to update manually.");
            OpenUpdateFolderButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { DownloadStatus.Text = zh ? "下载已取消。" : "Download cancelled."; }
        catch (Exception ex) { DownloadStatus.Text = (zh ? "下载失败：" : "Download failed: ") + ex.Message; }
        finally
        {
            _downloadCancellation = null;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            UpdateProgress.Visibility = Visibility.Collapsed;
            RefreshUpdateStatus();
        }
    }

    private void CancelUpdate_Click(object sender, RoutedEventArgs e) => _downloadCancellation?.Cancel();

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_installer is null || _updates?.LastDownload?.IsSigned != true) return;
        var zh = _viewModel?.IsChinese == true;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = zh ? "安装已签名的更新？" : "Install the signed update?",
            Content = zh ? "GSYNC 将等待正在运行和排队的同步及恢复任务结束，然后退出并重启。启动检查失败时会尝试恢复原版本。"
                : "GSYNC will wait for running and queued sync or restore tasks, then exit and restart. If the startup check fails, it will try to restore the previous version.",
            PrimaryButtonText = zh ? "等待并安装" : "Wait and install",
            CloseButtonText = zh ? "暂不安装" : "Not now",
            DefaultButton = ContentDialogButton.Close,
        });
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _ = App.Current.RunApplicationTaskAsync(token => _installer.InstallWhenIdleAsync(token));
    }

    private void CancelInstall_Click(object sender, RoutedEventArgs e) => _installer?.Cancel();

    private void OpenUpdateFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadedUpdate is null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + _downloadedUpdate + "\"") { UseShellExecute = true }); }
        catch (Exception ex) { DownloadStatus.Text = ex.Message; }
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
            case "startup":
                StartupSection.StartBringIntoView();
                break;
            case "updates":
                UpdatesSection.StartBringIntoView();
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
        App.Current.ApplyTheme(settings.ThemeMode);

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
