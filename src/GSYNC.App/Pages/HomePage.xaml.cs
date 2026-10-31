using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.App.Primitives;
using GSYNC.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class HomePage : Page
{
    private LibraryPageViewModel? _viewModel;

    public HomePage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    public LibraryPageViewModel? ViewModel => _viewModel;

    private void TryInitializePage()
    {
        try
        {
            ThrowIfInitializationForcedToFail("library");
            Log.Information("Initializing HomePage.");
            _viewModel = App.GetService<LibraryPageViewModel>();
            DataContext = _viewModel;
            ApplyLocalizedStaticText();

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            RefreshButton.Click -= RefreshButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            SyncNowButton.Click -= CheckAndSyncButton_Click;
            SyncNowButton.Click += CheckAndSyncButton_Click;
            UploadAllButton.Click -= SyncNowButton_Click;
            UploadAllButton.Click += SyncNowButton_Click;
            _ = _viewModel.LoadAsync();
            Log.Information("HomePage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "HomePage initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
    }

    private void ApplyLocalizedStaticText()
    {
        if (_viewModel is null)
        {
            return;
        }

        SearchBoxControl.PlaceholderText = _viewModel.SearchPlaceholder;
        SortButton.Content = _viewModel.SortText;
        RefreshButton.Content = _viewModel.RefreshText;
        AddGameButton.Content = _viewModel.AddGameText;
        SyncNowButton.Content = _viewModel.SyncNowText;
        UploadAllButton.Content = _viewModel.UploadAllText;
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }

    private static void ThrowIfInitializationForcedToFail(string pageKey)
    {
        var configured = Environment.GetEnvironmentVariable("GSYNC_FAIL_PAGE_INIT")?.Trim().ToLowerInvariant();
        if (string.Equals(configured, pageKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Forced {pageKey} page initialization failure for diagnostics.");
        }
    }

    private void OpenAddGameWizard_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(AddGameWizardPage));
    }

    private void MainContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrowHeader = e.NewSize.Width < 800;
        Grid.SetRow(HeaderActions, narrowHeader ? 1 : 0);
        Grid.SetColumn(HeaderActions, narrowHeader ? 0 : 1);
        Grid.SetColumnSpan(HeaderActions, narrowHeader ? 2 : 1);

        var narrowFilters = e.NewSize.Width < 700;
        Grid.SetColumnSpan(SearchBoxControl, narrowFilters ? 4 : 1);
        Grid.SetRow(StatusFilter, narrowFilters ? 1 : 0);
        Grid.SetColumn(StatusFilter, narrowFilters ? 0 : 1);
        StatusFilter.HorizontalAlignment = narrowFilters ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        Grid.SetRow(SortButton, narrowFilters ? 1 : 0);
        Grid.SetColumn(SortButton, narrowFilters ? 1 : 2);
        Grid.SetRow(RefreshButton, narrowFilters ? 1 : 0);
        Grid.SetColumn(RefreshButton, narrowFilters ? 2 : 3);
    }

    private void OpenTargets_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SyncTargetsPage));
    }

    private void OpenGameDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LibraryGameRow row })
        {
            Frame?.Navigate(typeof(GameDetailsPage), row.InstanceId.ToString("D"));
            return;
        }

        Frame?.Navigate(typeof(GameDetailsPage));
    }

    private async void ReviewConflict_Click(object sender, RoutedEventArgs e)
    {
        var row = _viewModel?.AllGames.FirstOrDefault(game => game.IsConflict);
        if (row is not null) await OpenComparisonAsync(row);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.LoadAsync();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleSort();
    }

    private async void SyncNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var confirm = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "上传所有游戏" : "Upload all games",
            Content = isChinese
                ? "将为所有游戏提交上传任务。如果远端有不同或额外的文件，该游戏的上传会停止，请先比较并处理差异。"
                : "An upload will be queued for every game. Uploads stop when remote files differ or contain extra files; compare and resolve those differences first.",
            PrimaryButtonText = isChinese ? "加入队列" : "Queue uploads",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var (queuedCount, error) = await _viewModel.SyncAllAsync();
        await _viewModel.LoadAsync();
        await ShowQueueResultAsync(queuedCount, error);
    }

    private async void CheckAndSyncButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var games = _viewModel.AllGames;
        var isChinese = _viewModel.IsChinese;
        if (games.Count == 0)
        {
            Frame?.Navigate(typeof(AddGameWizardPage));
            return;
        }
        var row = games[0];
        if (games.Count > 1)
        {
            var selector = new ComboBox { ItemsSource = games, DisplayMemberPath = nameof(LibraryGameRow.Name), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = isChinese ? "选择要比较的游戏。比较后可决定上传、下载或暂不处理。" : "Choose a game to compare, then decide what to upload, download, or leave unchanged.", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(selector);
            var dialog = DialogStyler.Apply(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = isChinese ? "检查并同步" : "Check and sync",
                Content = panel,
                PrimaryButtonText = isChinese ? "比较" : "Compare",
                CloseButtonText = isChinese ? "取消" : "Cancel",
            });
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || selector.SelectedItem is not LibraryGameRow selected) return;
            row = selected;
        }
        await OpenComparisonAsync(row);
    }

    private async void CompareGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LibraryGameRow row }) await OpenComparisonAsync(row);
    }

    private async Task OpenComparisonAsync(LibraryGameRow row)
    {
        if (_viewModel is null) return;
        var (context, error) = await _viewModel.CompareGameAsync(row);
        if (context is not null) { Frame?.Navigate(typeof(ConflictResolutionPage), context); return; }
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel.IsChinese ? "无法比较存档" : "Could not compare saves",
            Content = error ?? string.Empty,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async Task ShowQueueResultAsync(int queuedCount, string? error)
    {
        if (_viewModel is null) return;
        var isChinese = _viewModel.IsChinese;
        var message = error is not null
            ? (isChinese ? $"已加入 {queuedCount} 个任务。提交失败：{error}" : $"{queuedCount} jobs queued. Submission failed: {error}")
            : queuedCount == 0
                ? (isChinese ? "没有可上传的游戏。请先添加游戏。" : "There are no games to upload. Add a game first.")
                : (isChinese ? $"{queuedCount} 个上传任务已加入队列。完成结果会显示在历史中。" : $"{queuedCount} upload jobs were queued. Results will appear in History.");
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = error is null ? (isChinese ? "上传任务" : "Upload jobs") : (isChinese ? "提交未完成" : "Submission incomplete"),
            Content = message,
            PrimaryButtonText = queuedCount > 0 ? (isChinese ? "查看队列" : "View queue") : string.Empty,
            CloseButtonText = isChinese ? "关闭" : "Close",
        });
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            Frame?.Navigate(typeof(QueuePage));
    }
}
