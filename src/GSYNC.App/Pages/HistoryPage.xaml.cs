using System.Diagnostics;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class HistoryPage : Page
{
    private HistoryPageViewModel? _viewModel;

    public HistoryPage()
    {
        InitializeComponent();
        ResponsiveSplitLayout.Attach(HistoryPanes, HistoryDetailPane, 1000, new GridLength(1, GridUnitType.Star), new GridLength(360));
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            ThrowIfInitializationForcedToFail("history");
            Log.Information("Initializing HistoryPage.");
            _viewModel = App.GetService<HistoryPageViewModel>();
            DataContext = _viewModel;

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            Log.Information("HistoryPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "HistoryPage initialization failed.");
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
        if (_viewModel is not null) _ = _viewModel.LoadAsync();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is null) return;
        await _viewModel.LoadAsync();
        if (e.Parameter is Guid gameId)
        {
            _viewModel.FilterForGame(gameId);
            if (_viewModel.Entries.FirstOrDefault() is { } first) await _viewModel.SelectRecordAsync(first);
        }
    }

    private async void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is not null && HistoryList.SelectedItem is HistoryRow row && !_viewModel.IsRecordSelected(row))
            await _viewModel.SelectRecordAsync(row);
    }

    private async void ViewRecordFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedEntry is { } row) await ShowRecordDetailAsync(row);
    }

    private async Task ShowRecordDetailAsync(HistoryRow row)
    {
        if (_viewModel is null)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var files = await _viewModel.GetRecordSnapshotFilesAsync(row);
        if (!_viewModel.IsRecordSelected(row))
        {
            return;
        }

        var panel = new StackPanel { Spacing = 14, MinWidth = 480 };

        // Record property rows already prepared by SelectRecordAsync.
        var detailsHeader = new TextBlock
        {
            Text = isChinese ? "记录详情" : "Record Details",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        var detailsList = new StackPanel { Spacing = 6 };
        foreach (var detail in _viewModel.RecordDetails)
        {
            detailsList.Children.Add(BuildPropertyRow(detail.Label, detail.Value));
        }

        panel.Children.Add(detailsHeader);
        panel.Children.Add(detailsList);

        var filesHeader = new TextBlock
        {
            Text = isChinese ? $"快照文件（{files.Count}）" : $"Snapshot files ({files.Count})",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        panel.Children.Add(filesHeader);

        if (files.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = isChinese
                    ? "该记录没有可用的关联快照。"
                    : "No associated snapshot is available for this record.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
            });
        }
        else
        {
            var filesPanel = new StackPanel { Spacing = 4 };
            foreach (var file in files)
            {
                filesPanel.Children.Add(BuildPropertyRow(file.Label, file.Value));
            }

            panel.Children.Add(new ScrollViewer
            {
                Content = filesPanel,
                MaxHeight = 260,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });
        }

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{row.Game} · {row.Time}",
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            CloseButtonText = isChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private static Grid BuildPropertyRow(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock { Text = label, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        var valueText = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(labelText);
        grid.Children.Add(valueText);
        return grid;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.LoadAsync();
    }

    private void GoToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(HomePage));
    }

    private async void RestoreLatestSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var latest = _viewModel?.Snapshots.FirstOrDefault();
        if (latest is null)
        {
            return;
        }

        await ConfirmAndRestoreAsync(latest);
    }

    private async void SnapshotFeed_ActionInvoked(object? sender, object item)
    {
        if (item is SnapshotRow snapshotRow)
        {
            await ConfirmAndRestoreAsync(snapshotRow);
        }
    }

    private async Task ConfirmAndRestoreAsync(SnapshotRow snapshotRow)
    {
        if (_viewModel is null || _viewModel.IsRestoreInProgress)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var confirmDialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "恢复快照" : "Restore Snapshot",
            Content = UiFormat.RestoreConfirmationText(_viewModel.DetailGameName, snapshotRow.Name, snapshotRow.Timestamp, snapshotRow.FileCount, isChinese),
            PrimaryButtonText = isChinese ? "恢复" : "Restore",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var error = await _viewModel.RestoreSnapshotAsync(snapshotRow);
        if (error is not null)
        {
            var errorDialog = DialogStyler.Apply(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = isChinese ? "恢复失败" : "Restore Failed",
                Content = error,
                CloseButtonText = isChinese ? "关闭" : "Close",
            });
            await errorDialog.ShowAsync();
        }
    }

    private async void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsDirectory = new GSYNC.Data.Services.AppPathService().GetLogsDirectory();
            Directory.CreateDirectory(logsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logsDirectory}\""));
        }
        catch (Exception exception)
        {
            await DialogStyler.Apply(new ContentDialog { XamlRoot = XamlRoot,
                Title = _viewModel?.IsChinese == true ? "无法打开日志目录" : "Cannot open log folder", Content = exception.Message,
                CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close" }).ShowAsync();
        }
    }

}
