using System.Diagnostics;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Logging;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        AddNewTargetButton.Content = viewModel.AddNewTargetText;
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

    private async void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.RestoreDefaults();
        ApplyLocalizedStaticText(_viewModel);

        await ShowInfoAsync(
            _viewModel.IsChinese ? "已恢复默认设置" : "Defaults restored",
            _viewModel.IsChinese
                ? "外观、日志和行为设置已恢复为默认值，保存后会写入配置。"
                : "Appearance, logging, and behavior settings were restored to defaults. Save to persist them.");
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var settings = _viewModel.BuildSettings();
        var savedInChinese = _viewModel.IsChinese;
        _viewModel.SaveSettings();
        SerilogBootstrap.SetMinimumLevel(settings.LogLevel);
        ApplyAppearance(settings);

        await ShowInfoAsync(
            savedInChinese ? "设置已保存" : "Settings saved",
            savedInChinese ? "语言、主题、密度、日志级别与行为控制已保存。" : "Language, theme, density, log level, and behavior settings were saved.");
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
            case "targets":
                Frame?.Navigate(typeof(SyncTargetsPage));
                break;
            case "games":
                GamesSection.StartBringIntoView();
                break;
        }
    }

    private void AddNewTargetButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SyncTargetsPage));
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

    private async void GameEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UiGameMetaRow game } || _viewModel is null)
        {
            return;
        }

        await ShowGameEditDialogAsync(game);
    }

    private async Task ShowGameEditDialogAsync(UiGameMetaRow game)
    {
        if (_viewModel is null) return;

        var isChinese = _viewModel.IsChinese;
        var instance = await _viewModel.GetGameInstanceAsync(game.InstanceId);
        if (instance is null) return;

        var dimBrush = Application.Current.Resources.TryGetValue("AppTextDimBrush", out var db) ? db as Brush : null;
        var secBrush = Application.Current.Resources.TryGetValue("AppTextSecondaryBrush", out var sb) ? sb as Brush : null;
        var secStyle = Application.Current.Resources.TryGetValue("SecondaryToolbarButtonStyle", out var ss) ? ss as Style : null;

        // ── Display name ──────────────────────────────────────────────────────
        var displayNameBox = new TextBox
        {
            Header = (object)(isChinese ? "显示名称" : "Display Name"),
            Text = instance.DisplayName,
            PlaceholderText = isChinese ? "游戏名称" : "Game name",
        };

        // ── Install directory ──────────────────────────────────────────────────
        var installDirBox = new TextBox
        {
            Text = instance.InstallDirectory ?? string.Empty,
            PlaceholderText = isChinese ? "安装路径（可选）" : "Install path (optional)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var browseBtn = new Button { Content = isChinese ? "浏览" : "Browse", Style = secStyle };
        browseBtn.Click += async (_, _) =>
        {
            var picked = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (picked is not null) installDirBox.Text = picked;
        };

        var installDirRow = new Grid { ColumnSpacing = 8 };
        installDirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        installDirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        installDirRow.Children.Add(installDirBox);
        Grid.SetColumn(browseBtn, 1);
        installDirRow.Children.Add(browseBtn);

        var installDirPanel = new StackPanel { Spacing = 6 };
        installDirPanel.Children.Add(new TextBlock
        {
            Text = isChinese ? "安装目录" : "Install Directory",
            Foreground = dimBrush,
        });
        installDirPanel.Children.Add(installDirRow);

        // ── Variables ─────────────────────────────────────────────────────────
        var variablePairs = new List<(TextBox Key, TextBox Value)>();
        var variablesPanel = new StackPanel { Spacing = 4 };

        foreach (var (k, v) in instance.Variables.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddVariableRow(variablePairs, variablesPanel, k, v, secStyle);
        }

        var addVarBtn = new Button { Content = isChinese ? "+ 添加变量" : "+ Add Variable", Style = secStyle };
        addVarBtn.Click += (_, _) => AddVariableRow(variablePairs, variablesPanel, string.Empty, string.Empty, secStyle);

        var varHeader = new TextBlock
        {
            Text = isChinese ? "游戏专属变量" : "Game-Specific Variables",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var varNote = new TextBlock
        {
            Text = isChinese
                ? "键名自动规范为 %UPPER_SNAKE_CASE%，覆盖同名全局变量，仅对此游戏生效。"
                : "Key names are normalized to %UPPER_SNAKE_CASE% and override matching global variables for this game only.",
            FontSize = 11,
            Foreground = secBrush,
            TextWrapping = TextWrapping.WrapWholeWords,
        };

        // ── Game ID info ──────────────────────────────────────────────────────
        var metaText = new TextBlock
        {
            Text = $"Game ID: {game.GameId}  ·  {game.SourceLabel}",
            FontSize = 11,
            Foreground = dimBrush,
        };

        // ── Assemble content ──────────────────────────────────────────────────
        var content = new StackPanel { Spacing = 12, Width = 460 };
        content.Children.Add(metaText);
        content.Children.Add(displayNameBox);
        content.Children.Add(installDirPanel);
        content.Children.Add(varHeader);
        content.Children.Add(varNote);
        content.Children.Add(variablesPanel);
        content.Children.Add(addVarBtn);

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "编辑游戏元数据" : "Edit Game Metadata",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = isChinese ? "保存" : "Save",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var newName = displayNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        var newVariables = variablePairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key.Text))
            .ToDictionary(
                pair => pair.Key.Text.Trim().Trim('%').ToUpperInvariant().Replace(' ', '_'),
                pair => pair.Value.Text.Trim(),
                StringComparer.OrdinalIgnoreCase);

        await _viewModel.UpdateGameInstanceAsync(
            game.InstanceId,
            newName,
            string.IsNullOrWhiteSpace(installDirBox.Text) ? null : installDirBox.Text.Trim(),
            newVariables);
    }

    private static void AddVariableRow(
        List<(TextBox Key, TextBox Value)> pairs,
        StackPanel panel,
        string key,
        string value,
        Style? buttonStyle)
    {
        var keyBox = new TextBox
        {
            PlaceholderText = "VARIABLE_NAME",
            Text = key,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Width = 170,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valueBox = new TextBox
        {
            PlaceholderText = "value",
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var pair = (keyBox, valueBox);
        pairs.Add(pair);

        var removeBtn = new Button
        {
            Content = "×",
            Style = buttonStyle,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(keyBox);
        Grid.SetColumn(valueBox, 1);
        row.Children.Add(valueBox);
        Grid.SetColumn(removeBtn, 2);
        row.Children.Add(removeBtn);

        removeBtn.Click += (_, _) =>
        {
            pairs.Remove(pair);
            panel.Children.Remove(row);
        };

        panel.Children.Add(row);
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
        Frame?.Navigate(typeof(SyncTargetsPage));
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

    private static void ApplyAppearance(AppUiSettings settings)
    {
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
