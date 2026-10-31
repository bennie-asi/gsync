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
    private AutoUpdateCoordinator? _autoUpdate;
    private CancellationTokenSource? _downloadCancellation;
    private string? _downloadedUpdate;
    private bool _refreshingStartup = true;

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
        _autoUpdate = App.GetService<AutoUpdateCoordinator>();
        _autoUpdate.Changed += Updates_Changed;
        RefreshUpdateStatus();
        RefreshAutoPullStatus();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_autoPull is not null) _autoPull.StatusChanged -= AutoPull_StatusChanged;
        if (_updates is not null) _updates.Changed -= Updates_Changed;
        if (_installer is not null) _installer.Changed -= Updates_Changed;
        if (_autoUpdate is not null) _autoUpdate.Changed -= Updates_Changed;
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
        AutoPullStatusText.Text = statuses.Count == 0 ? (zh ? "等待检查。" : "Waiting to check.") :
            (zh ? $"已检查 {statuses.Count} 个游戏；具体状态请查看游戏详情。" : $"Checked {statuses.Count} games. See each game for its status.");
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
        StartupDescription.Text = zh ? "登录 Windows 后启动 GSYNC；更改立即生效。" : "Start GSYNC when you sign in to Windows. Changes apply immediately.";
        StartupToggle.Header = zh ? "登录时启动" : "Start at sign-in";
        StartupToggle.OnContent = zh ? "开" : "On";
        StartupToggle.OffContent = zh ? "关" : "Off";
        UpdateStartupPathButton.Content = zh ? "使用当前程序路径" : "Use current app location";
        WindowsStartupButton.Content = zh ? "打开 Windows 启动设置" : "Open Windows startup settings";
        CloseToTrayToggle.Header = zh ? "关闭窗口后在后台运行" : "Keep running when the window is closed";
        CloseToTrayToggle.OnContent = zh ? "开" : "On";
        CloseToTrayToggle.OffContent = zh ? "关" : "Off";
        TrayDescription.Text = zh ? "在系统托盘中单击图标打开窗口，右键选择退出。更改保存后生效。" : "Click the notification-area icon to open the window, or right-click it to exit. Save settings to apply changes.";
        UpdatesSection.Title = zh ? "关于与更新" : "About and updates";
        VersionText.Text = (zh ? "当前版本：" : "Current version: ") + DesktopFeatures.CurrentVersion.Split('+')[0];
        UpdateStatus.Text = zh ? "尚未检查应用更新。" : "Application updates have not been checked.";
        CheckUpdatesButton.Content = zh ? "检查更新" : "Check for updates";
        ReleasesButton.Content = zh ? "前往 GitHub 发行页" : "Open GitHub releases";
        AutoCheckToggle.Header = zh ? "自动检查更新（运行期间每天最多一次）" : "Automatically check for updates (at most once a day while running)";
        AutoCheckToggle.OnContent = zh ? "开" : "On";
        AutoCheckToggle.OffContent = zh ? "关" : "Off";
        AutoInstallToggle.Header = zh ? "自动下载并在同步结束后安装（将重启应用）" : "Automatically download and install after sync completes (restarts the app)";
        AutoInstallToggle.OnContent = zh ? "开" : "On";
        AutoInstallToggle.OffContent = zh ? "关" : "Off";
        AutoUpdateNote.Text = zh ? "自动安装需要开启自动检查；更改保存后生效。" : "Automatic installation requires automatic checks. Save settings to apply changes.";
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
        _refreshingStartup = true;
        try
        {
            var command = DesktopFeatures.StartupCommand();
            var mismatch = command is not null && !string.Equals(command, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase);
            StartupToggle.IsEnabled = true;
            StartupToggle.IsOn = command is not null;
            UpdateStartupPathButton.Visibility = mismatch ? Visibility.Visible : Visibility.Collapsed;
            StartupStatus.Visibility = command is null ? Visibility.Collapsed : Visibility.Visible;
            StartupStatus.Text = command is null ? (zh ? "未启用。" : "Not enabled.") : mismatch
                ? (zh ? "启动项指向其他位置：" : "Startup points to a different location: ") + command
                : (zh ? "已登记；Windows 可在启动设置中禁用此项。" : "Registered. Windows can disable this entry in startup settings.");
        }
        catch (Exception ex)
        {
            StartupToggle.IsEnabled = false;
            UpdateStartupPathButton.Visibility = Visibility.Collapsed;
            StartupStatus.Visibility = Visibility.Visible;
            StartupStatus.Text = (zh ? "无法读取启动状态：" : "Cannot read startup status: ") + ex.Message;
        }
        finally { _refreshingStartup = false; }
    }

    private async void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_refreshingStartup) return;
        var zh = _viewModel?.IsChinese == true;
        var enabled = StartupToggle.IsOn;
        StartupToggle.IsEnabled = false;
        try
        {
            var existing = DesktopFeatures.StartupCommand();
            if (existing is not null && !string.Equals(existing, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase))
            {
                var dialog = DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot, Title = zh ? "更改启动项？" : "Change startup entry?",
                    Content = (zh ? "当前启动项指向：" : "The current startup entry points to: ") + existing,
                    PrimaryButtonText = enabled ? (zh ? "使用当前程序" : "Use current app") : (zh ? "关闭启动项" : "Disable startup"),
                    CloseButtonText = zh ? "取消" : "Cancel", DefaultButton = ContentDialogButton.Close });
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) { RefreshStartupStatus(); return; }
            }
            DesktopFeatures.SetStartup(enabled);
            RefreshStartupStatus();
        }
        catch (Exception ex) { RefreshStartupStatus(); StartupStatus.Visibility = Visibility.Visible; StartupStatus.Text = (zh ? "无法更改启动设置：" : "Cannot change startup settings: ") + ex.Message; }
    }

    private async void UpdateStartupPath_Click(object sender, RoutedEventArgs e)
    {
        var zh = _viewModel?.IsChinese == true;
        try
        {
            var existing = DesktopFeatures.StartupCommand();
            if (existing is not null && !string.Equals(existing, DesktopFeatures.ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase))
            {
                var dialog = DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot, Title = zh ? "更新启动路径？" : "Update startup location?",
                    Content = (zh ? "当前启动项：" : "Current entry: ") + existing + Environment.NewLine + (zh ? "改为使用当前程序？" : "Use the current app instead?"),
                    PrimaryButtonText = zh ? "使用当前程序" : "Use current app", CloseButtonText = zh ? "取消" : "Cancel", DefaultButton = ContentDialogButton.Close });
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                DesktopFeatures.SetStartup(true);
            }
            RefreshStartupStatus();
        }
        catch (Exception ex) { StartupStatus.Text = (zh ? "无法更新启动路径：" : "Cannot update startup location: ") + ex.Message; }
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
        var automatic = _autoUpdate?.Status ?? new AutoUpdateStatus("idle");
        var installing = install.State is "preparing" or "waiting" or "starting" || automatic.State == "downloading";
        InstallStatusText.Text = install.State switch
        {
            "preparing" => zh ? "正在准备已签名的更新…" : "Preparing the signed update…",
            "waiting" => zh ? "等待同步和恢复任务结束后自动安装；你可以继续使用应用。" : "Waiting for sync and restore tasks to finish before installing; you can keep using the app.",
            "starting" => zh ? "任务已结束，正在退出并启动更新程序…" : "Tasks are complete. Closing the app and starting the updater…",
            "cancelled" => zh ? "已取消等待安装。" : "Update installation was cancelled.",
            "error" => (zh ? "无法安装更新：" : "Cannot install update: ") + install.Message,
            _ => string.Empty,
        };
        if (install.State is not ("preparing" or "waiting" or "starting"))
            InstallStatusText.Text = automatic.State switch
            {
                "downloading" => zh ? $"正在自动下载并验证版本 {automatic.Version}…" : $"Automatically downloading and verifying version {automatic.Version}…",
                "deferred" => zh ? $"版本 {automatic.Version} 的自动安装已暂停，可手动安装或等待下一个版本。" : $"Automatic installation of {automatic.Version} is paused; install manually or wait for a newer version.",
                "error" => (zh ? "自动更新失败，将在下次检查时重试：" : "Automatic update failed; it will retry on the next check: ") + automatic.Message,
                _ => InstallStatusText.Text,
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
        catch (Exception exception)
        {
            SettingsFeedback.Text = (_viewModel?.IsChinese == true ? "无法打开日志目录：" : "Cannot open the logs directory: ") + exception.Message;
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
        catch (Exception exception)
        {
            SettingsFeedback.Text = (_viewModel?.IsChinese == true ? "无法打开数据目录：" : "Cannot open the data directory: ") + exception.Message;
        }
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
