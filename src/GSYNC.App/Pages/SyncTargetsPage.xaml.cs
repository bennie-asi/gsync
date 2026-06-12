using System.Diagnostics;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.Data.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SyncTargetsPage : Page
{
    private SyncTargetsPageViewModel? _viewModel;

    public SyncTargetsPage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            ThrowIfInitializationForcedToFail("targets");
            Log.Information("Initializing SyncTargetsPage.");
            _viewModel = App.GetService<SyncTargetsPageViewModel>();
            DataContext = _viewModel;

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            _ = _viewModel.LoadAsync(testConnections: true);
            Log.Information("SyncTargetsPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "SyncTargetsPage initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
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

    private void TargetRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_viewModel is not null && sender is FrameworkElement { DataContext: SyncTargetRow row })
        {
            _ = _viewModel.SelectTargetAsync(row);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.LoadAsync(testConnections: true);
    }

    private void TestSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.TestSelectedAsync();
    }

    private async void AddTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var config = await ShowTargetEditorAsync(null);
        if (config is not null)
        {
            await _viewModel.SaveTargetAsync(config);
        }
    }

    private async void EditTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.GetSelectedConfig() is not { } existing)
        {
            return;
        }

        var config = await ShowTargetEditorAsync(existing);
        if (config is not null)
        {
            await _viewModel.SaveTargetAsync(config);
        }
    }

    private async void RemoveTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.GetSelectedConfig() is not { } config)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "移除目标" : "Remove Target",
            Content = isChinese
                ? $"确定移除目标 “{config.Name}” 吗？已绑定该目标的游戏将无法继续同步，远端数据不会被删除。"
                : $"Remove target \"{config.Name}\"? Games bound to it can no longer sync; remote data is not deleted.",
            PrimaryButtonText = isChinese ? "移除" : "Remove",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _viewModel.RemoveSelectedAsync();
        }
    }

    private void OpenRootButton_Click(object sender, RoutedEventArgs e)
    {
        var pathOrUrl = _viewModel?.GetSelectedRootPathOrUrl();
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return;
        }

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
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to open target root {Path}.", pathOrUrl);
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsDirectory = new AppPathService().GetLogsDirectory();
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logsDirectory}\""));
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to open logs directory.");
        }
    }

    private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(_viewModel.FailureCodeText))
        {
            return;
        }

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText($"{_viewModel.FailureCodeText}: {_viewModel.FailureMessage}");
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    private async void OpenHelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel.IsChinese ? "同步目标说明" : "Sync Targets Help",
            Content = _viewModel.IsChinese
                ? "在这里管理本地文件夹和 WebDAV 目标，测试连接、编辑配置以及查看绑定关系。"
                : "Manage local-folder and WebDAV targets here, test connectivity, edit configuration, and inspect bindings.",
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async Task<SyncTargetConfig?> ShowTargetEditorAsync(SyncTargetConfig? existing)
    {
        if (_viewModel is null)
        {
            return null;
        }

        var isChinese = _viewModel.IsChinese;

        var nameBox = new TextBox
        {
            Header = isChinese ? "显示名称" : "Display Name",
            Text = existing?.Name ?? string.Empty,
        };
        var typeBox = new ComboBox
        {
            Header = isChinese ? "提供程序类型" : "Provider Type",
            ItemsSource = new[] { "WebDAV", isChinese ? "本地文件夹" : "Local Folder" },
            SelectedIndex = existing?.ProviderId == "webdav" ? 0 : 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = existing is null,
        };
        var urlBox = new TextBox
        {
            Header = isChinese ? "端点 URL" : "Endpoint URL",
            PlaceholderText = "https://dav.example.com/gsync",
            Text = existing?.Settings.GetValueOrDefault("baseUrl") ?? string.Empty,
        };
        var usernameBox = new TextBox
        {
            Header = isChinese ? "用户名" : "Username",
            Text = existing?.Settings.GetValueOrDefault("username") ?? string.Empty,
        };
        var passwordBox = new PasswordBox
        {
            Header = isChinese ? "密码" : "Password",
            Password = existing?.Settings.GetValueOrDefault("password") ?? string.Empty,
        };
        var rootPathBox = new TextBox
        {
            Header = isChinese ? "根目录" : "Root Path",
            PlaceholderText = @"D:\Saves\GSYNC",
            Text = existing?.Settings.GetValueOrDefault("rootPath") ?? string.Empty,
        };
        var browseRootButton = new Button
        {
            Content = isChinese ? "选择文件夹" : "Browse Folder",
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
        };
        browseRootButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                rootPathBox.Text = selected;
            }
        };

        var webDavPanel = new StackPanel { Spacing = 10 };
        webDavPanel.Children.Add(urlBox);
        webDavPanel.Children.Add(usernameBox);
        webDavPanel.Children.Add(passwordBox);

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);
        panel.Children.Add(webDavPanel);
        panel.Children.Add(rootPathBox);
        panel.Children.Add(browseRootButton);

        void UpdateFieldVisibility()
        {
            var isWebDav = typeBox.SelectedIndex == 0;
            webDavPanel.Visibility = isWebDav ? Visibility.Visible : Visibility.Collapsed;
            rootPathBox.Visibility = isWebDav ? Visibility.Collapsed : Visibility.Visible;
        }

        typeBox.SelectionChanged += (_, _) => UpdateFieldVisibility();
        UpdateFieldVisibility();

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existing is null
                ? (isChinese ? "添加同步目标" : "Add Sync Target")
                : (isChinese ? "编辑同步目标" : "Edit Sync Target"),
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = isChinese ? "保存" : "Save",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var isWebDavTarget = typeBox.SelectedIndex == 0;
        var name = string.IsNullOrWhiteSpace(nameBox.Text)
            ? (isWebDavTarget ? "WebDAV" : (isChinese ? "本地文件夹" : "Local Folder"))
            : nameBox.Text.Trim();

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (isWebDavTarget)
        {
            settings["baseUrl"] = urlBox.Text.Trim();
            settings["username"] = usernameBox.Text.Trim();
            settings["password"] = passwordBox.Password;
        }
        else
        {
            settings["rootPath"] = rootPathBox.Text.Trim();
        }

        if (existing is null)
        {
            return new SyncTargetConfig
            {
                Name = name,
                ProviderId = isWebDavTarget ? "webdav" : "local-folder",
                Settings = settings,
            };
        }

        existing.Name = name;
        existing.Settings = settings;
        return existing;
    }
}
