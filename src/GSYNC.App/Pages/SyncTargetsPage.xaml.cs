using System.Diagnostics;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.Data.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SyncTargetsPage : Page
{
    private SyncTargetsPageViewModel? _viewModel;
    private AddGameWizardViewModel? _returnWizard;
    private GameTargetSetup? _returnGame;
    private bool _isEditingTarget;

    public SyncTargetsPage()
    {
        InitializeComponent();
        ResponsiveSplitLayout.Attach(TargetPanes, TargetDetailPane, 900, new GridLength(320), new GridLength(1, GridUnitType.Star));
        TryInitializePage();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is WizardTargetSetup context)
        {
            _returnWizard = context.Wizard;
            ReturnToWizardButton.Content = _viewModel?.IsChinese == true ? "返回添加游戏" : "Return to setup";
            ReturnToWizardButton.Visibility = Visibility.Visible;
        }
        else if (e.Parameter is GameTargetSetup gameContext)
        {
            _returnGame = gameContext;
            ReturnToWizardButton.Content = _viewModel?.IsChinese == true ? "返回游戏详情" : "Return to game";
            ReturnToWizardButton.Visibility = Visibility.Visible;
        }
    }

    private void ReturnToWizard_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditingTarget) return;
        if (_returnWizard is not null)
            Frame?.Navigate(typeof(AddGameWizardPage), new WizardTargetReturn(_returnWizard, _viewModel?.SelectedTargetId));
        else if (_returnGame is not null)
            Frame?.Navigate(typeof(GameDetailsPage), new GameTargetReturn(_returnGame.InstanceId, _viewModel?.SelectedTargetId, _returnGame.RemoteNamespace));
    }

    private void TargetsHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 790;
        Grid.SetRow(TargetHeaderActions, narrow ? 2 : 1);
        Grid.SetColumn(TargetHeaderActions, narrow ? 0 : 1);
        Grid.SetColumnSpan(TargetHeaderActions, narrow ? 2 : 1);
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        App.GetService<SyncTargetStore>().ConnectionChecksChanged += ConnectionChecksChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        App.GetService<SyncTargetStore>().ConnectionChecksChanged -= ConnectionChecksChanged;
    }

    private void ConnectionChecksChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (IsLoaded && _viewModel is not null) await _viewModel.LoadAsync();
        });
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

    private void TargetRow_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditingTarget && _viewModel is not null && sender is FrameworkElement { DataContext: SyncTargetRow row })
        {
            _ = _viewModel.SelectTargetAsync(row);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditingTarget) _ = _viewModel?.LoadAsync(testConnections: true);
    }

    private void TestSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditingTarget) _ = _viewModel?.TestSelectedAsync();
    }

    private async void SetDefaultTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && !_isEditingTarget) await _viewModel.SetSelectedDefaultAsync();
    }

    private void AddTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditingTarget && _viewModel is not null) ShowTargetEditor(null);
    }

    private void EditTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditingTarget || _viewModel?.GetSelectedConfig(includeCredentials: true) is not { } existing)
        {
            return;
        }
        ShowTargetEditor(existing);
    }

    private async void RemoveTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditingTarget || _viewModel?.GetSelectedConfig() is not { } config)
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

    private async void OpenRootButton_Click(object sender, RoutedEventArgs e)
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
            else
            {
                await ShowTargetErrorAsync(_viewModel?.IsChinese == true ? "目标路径不存在，请检查配置。" : "The target path does not exist. Check its configuration.");
            }
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to open target root {Path}.", pathOrUrl);
            await ShowTargetErrorAsync(exception.Message);
        }
    }

    private async Task ShowTargetErrorAsync(string message)
    {
        await DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot,
            Title = _viewModel?.IsChinese == true ? "无法打开目标" : "Cannot open target", Content = message,
            CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close" }).ShowAsync();
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

    private void ShowTargetEditor(SyncTargetConfig? existing)
    {
        if (_viewModel is null)
        {
            return;
        }

        var vm = _viewModel;
        var isChinese = vm.IsChinese;
        var secondaryStyle = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"];

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
            Header = existing?.CredentialsUnavailable == true ? (isChinese ? "密码（请重新输入凭据）" : "Password (re-enter credentials)") : (isChinese ? "密码" : "Password"),
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
            Style = secondaryStyle,
        };
        browseRootButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                rootPathBox.Text = selected;
            }
        };

        // Local-folder fields live in their own panel so the local picker is never shown for a
        // WebDAV target (previously it stayed visible and only picked a path on this machine).
        var localPanel = new StackPanel { Spacing = 10 };
        localPanel.Children.Add(rootPathBox);
        localPanel.Children.Add(browseRootButton);

        // Remote folder browser for WebDAV: connects with the entered URL + credentials and lists
        // remote collections so the user can drill into and pick a folder on the server itself.
        var currentRemotePath = string.Empty;
        var remotePathText = new TextBlock
        {
            Text = "/",
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var remoteUpButton = new Button { Content = isChinese ? "上一级" : "Up", Style = secondaryStyle, IsEnabled = false };
        var remoteList = new ListView
        {
            MaxHeight = 200,
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
        };
        var remoteStatus = new TextBlock { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        var useFolderButton = new Button { Content = isChinese ? "使用此目录" : "Use This Folder", Style = secondaryStyle };
        var browseRemoteButton = new Button { Content = isChinese ? "浏览远程目录…" : "Browse Remote…", Style = secondaryStyle };

        var browserHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        browserHeader.Children.Add(remoteUpButton);
        browserHeader.Children.Add(remotePathText);
        var browserPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        browserPanel.Children.Add(browserHeader);
        browserPanel.Children.Add(remoteList);
        browserPanel.Children.Add(remoteStatus);
        browserPanel.Children.Add(useFolderButton);

        async Task LoadRemoteFoldersAsync()
        {
            remoteStatus.Visibility = Visibility.Visible;
            remoteStatus.Text = isChinese ? "正在加载…" : "Loading…";
            remoteList.Items.Clear();
            remoteUpButton.IsEnabled = false;
            try
            {
                var folders = await vm.BrowseWebDavFoldersAsync(
                    urlBox.Text.Trim(), usernameBox.Text.Trim(), passwordBox.Password, currentRemotePath);
                remotePathText.Text = "/" + currentRemotePath;
                remoteUpButton.IsEnabled = currentRemotePath.Length > 0;
                foreach (var folder in folders)
                {
                    remoteList.Items.Add(folder);
                }

                if (folders.Count == 0)
                {
                    remoteStatus.Text = isChinese ? "（此目录下没有子文件夹）" : "(No subfolders here)";
                }
                else
                {
                    remoteStatus.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception exception)
            {
                remoteStatus.Text = (isChinese ? "加载失败：" : "Failed: ") + exception.Message;
            }
        }

        browseRemoteButton.Click += async (_, _) =>
        {
            browserPanel.Visibility = Visibility.Visible;
            if (string.IsNullOrWhiteSpace(urlBox.Text))
            {
                remoteStatus.Visibility = Visibility.Visible;
                remoteStatus.Text = isChinese ? "请先填写端点 URL。" : "Enter the endpoint URL first.";
                return;
            }

            currentRemotePath = string.Empty;
            await LoadRemoteFoldersAsync();
        };
        remoteList.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is string folder)
            {
                currentRemotePath = string.IsNullOrEmpty(currentRemotePath) ? folder : currentRemotePath + "/" + folder;
                await LoadRemoteFoldersAsync();
            }
        };
        remoteUpButton.Click += async (_, _) =>
        {
            var trimmed = currentRemotePath.TrimEnd('/');
            var slash = trimmed.LastIndexOf('/');
            currentRemotePath = slash <= 0 ? string.Empty : trimmed[..slash];
            await LoadRemoteFoldersAsync();
        };
        useFolderButton.Click += (_, _) =>
        {
            var trimmedBase = urlBox.Text.Trim().TrimEnd('/');
            urlBox.Text = string.IsNullOrEmpty(currentRemotePath) ? trimmedBase
                : trimmedBase + "/" + string.Join('/', currentRemotePath.Split('/').Select(Uri.EscapeDataString));
            browserPanel.Visibility = Visibility.Collapsed;
        };

        var webDavPanel = new StackPanel { Spacing = 10 };
        webDavPanel.Children.Add(urlBox);
        webDavPanel.Children.Add(usernameBox);
        webDavPanel.Children.Add(passwordBox);
        webDavPanel.Children.Add(browseRemoteButton);
        webDavPanel.Children.Add(browserPanel);

        var panel = new StackPanel { Spacing = 12, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);
        panel.Children.Add(webDavPanel);
        panel.Children.Add(localPanel);
        var validationMessage = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        panel.Children.Add(validationMessage);

        void UpdateFieldVisibility()
        {
            var isWebDav = typeBox.SelectedIndex == 0;
            webDavPanel.Visibility = isWebDav ? Visibility.Visible : Visibility.Collapsed;
            localPanel.Visibility = isWebDav ? Visibility.Collapsed : Visibility.Visible;
        }

        typeBox.SelectionChanged += (_, _) => UpdateFieldVisibility();
        UpdateFieldVisibility();

        var saveButton = new Button { Content = isChinese ? "保存目标" : "Save target", Style = (Style)Application.Current.Resources["PrimaryToolbarButtonStyle"] };
        var cancelButton = new Button { Content = isChinese ? "取消" : "Cancel", Style = secondaryStyle };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(saveButton);
        buttons.Children.Add(cancelButton);
        panel.Children.Add(buttons);
        cancelButton.Click += (_, _) => CloseTargetEditor(passwordBox);
        saveButton.Click += async (_, _) =>
        {
            string? error = null;
            if (typeBox.SelectedIndex == 0)
            {
                if (!Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var endpoint) ||
                    (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
                    endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
                {
                    error = isChinese ? "请输入完整的 HTTP 或 HTTPS 地址，且不要包含用户名、查询参数或片段。" : "Enter a complete HTTP or HTTPS URL without user info, a query, or a fragment.";
                }
                else if (existing?.CredentialsUnavailable == true && string.IsNullOrEmpty(passwordBox.Password))
                {
                    error = isChinese ? "原有凭据无法读取，请重新输入密码后保存。" : "Existing credentials could not be read. Re-enter the password before saving.";
                }
            }
            else if (string.IsNullOrWhiteSpace(rootPathBox.Text) || !Path.IsPathFullyQualified(rootPathBox.Text.Trim()))
            {
                error = isChinese ? "请输入或选择完整的本地文件夹路径。" : "Enter or choose an absolute local folder path.";
            }

            if (error is not null)
            {
                validationMessage.Text = error;
                validationMessage.Visibility = Visibility.Visible;
                validationMessage.StartBringIntoView();
                return;
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
            else settings["rootPath"] = rootPathBox.Text.Trim();
            var config = existing ?? new SyncTargetConfig { Name = name, ProviderId = isWebDavTarget ? "webdav" : "local-folder", Settings = settings };
            config.Name = name;
            config.Settings = settings;
            saveButton.IsEnabled = false;
            try
            {
                await vm.SaveTargetAsync(config);
                CloseTargetEditor(passwordBox);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to save sync target.");
                validationMessage.Text = (isChinese ? "保存失败：" : "Could not save: ") + exception.Message;
                validationMessage.Visibility = Visibility.Visible;
                saveButton.IsEnabled = true;
            }
        };

        TargetEditorHeading.Text = existing is null ? (isChinese ? "添加同步目标" : "Add sync target") : (isChinese ? "编辑同步目标" : "Edit sync target");
        TargetEditorContent.Content = panel;
        TargetDetailHeading.Visibility = Visibility.Collapsed;
        TargetEditorHeading.Visibility = Visibility.Visible;
        SetDefaultTargetButton.Visibility = Visibility.Collapsed;
        TargetDetailsScroll.Visibility = Visibility.Collapsed;
        TargetActionsFooter.Visibility = Visibility.Collapsed;
        TargetEditorScroll.Visibility = Visibility.Visible;
        _isEditingTarget = true;
        TargetsHeader.IsHitTestVisible = false;
        TargetListPane.IsHitTestVisible = false;
        TargetEditorScroll.ChangeView(null, 0, null);
        TargetDetailPane.StartBringIntoView();
    }

    private void CloseTargetEditor(PasswordBox passwordBox)
    {
        passwordBox.Password = string.Empty;
        TargetEditorContent.Content = null;
        TargetDetailHeading.Visibility = Visibility.Visible;
        TargetEditorHeading.Visibility = Visibility.Collapsed;
        SetDefaultTargetButton.Visibility = Visibility.Visible;
        TargetDetailsScroll.Visibility = Visibility.Visible;
        TargetActionsFooter.Visibility = Visibility.Visible;
        TargetEditorScroll.Visibility = Visibility.Collapsed;
        _isEditingTarget = false;
        TargetsHeader.IsHitTestVisible = true;
        TargetListPane.IsHitTestVisible = true;
    }
}
