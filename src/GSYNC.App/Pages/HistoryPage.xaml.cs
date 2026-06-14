using System.Diagnostics;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class HistoryPage : Page
{
    private HistoryPageViewModel? _viewModel;

    public HistoryPage()
    {
        InitializeComponent();
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

            HistoryTable.Columns =
            [
                new ResizableTableColumn { Key = "time", Header = _viewModel.IsChinese ? "时间" : "Time", BindingPath = nameof(HistoryRow.Time), Width = 112, MinWidth = 92 },
                new ResizableTableColumn { Key = "game", Header = _viewModel.IsChinese ? "游戏" : "Game", BindingPath = nameof(HistoryRow.Game), Width = 180, MinWidth = 140, IsBold = true },
                new ResizableTableColumn { Key = "direction", Header = _viewModel.IsChinese ? "方向" : "Direction", BindingPath = nameof(HistoryRow.Direction), Width = 110, MinWidth = 96 },
                new ResizableTableColumn { Key = "result", Header = _viewModel.IsChinese ? "结果" : "Result", BindingPath = nameof(HistoryRow.Result), Width = 110, MinWidth = 90, CellTemplate = (DataTemplate)Resources["HistoryResultTemplate"] },
                new ResizableTableColumn { Key = "target", Header = _viewModel.IsChinese ? "目标" : "Target", BindingPath = nameof(HistoryRow.Target), Width = 180, MinWidth = 130, IsFillColumn = true },
                new ResizableTableColumn { Key = "source", Header = _viewModel.IsChinese ? "来源" : "Source", BindingPath = nameof(HistoryRow.Source), Width = 140, MinWidth = 120 },
                new ResizableTableColumn { Key = "files", Header = _viewModel.IsChinese ? "文件" : "Files", BindingPath = nameof(HistoryRow.Files), Width = 92, MinWidth = 76 },
            ];

            HistoryTable.RowInvoked -= HistoryTable_RowInvoked;
            HistoryTable.RowInvoked += HistoryTable_RowInvoked;
            HistoryTable.RowDoubleInvoked -= HistoryTable_RowDoubleInvoked;
            HistoryTable.RowDoubleInvoked += HistoryTable_RowDoubleInvoked;

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            _ = _viewModel.LoadAsync();
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
    }

    private void HistoryTable_RowInvoked(object? sender, object item)
    {
        if (_viewModel is not null && item is HistoryRow row)
        {
            _ = _viewModel.SelectRecordAsync(row);
        }
    }

    private async void HistoryTable_RowDoubleInvoked(object? sender, object item)
    {
        if (_viewModel is null || item is not HistoryRow row)
        {
            return;
        }

        await _viewModel.SelectRecordAsync(row);
        await ShowRecordDetailAsync(row);
    }

    private async Task ShowRecordDetailAsync(HistoryRow row)
    {
        if (_viewModel is null)
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var files = await _viewModel.GetRecordSnapshotFilesAsync(row);

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
            Text = isChinese ? $"同步文件（{files.Count}）" : $"Synced Files ({files.Count})",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        panel.Children.Add(filesHeader);

        if (files.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = isChinese
                    ? "该记录未保留逐文件清单（仅在生成快照的操作中可用）。"
                    : "No per-file list was retained for this record (available only for operations that captured a snapshot).",
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
            Content = isChinese
                ? $"将使用快照 {snapshotRow.Name}（{snapshotRow.Timestamp}）覆盖本地存档文件。是否继续？"
                : $"Local save files will be overwritten with snapshot {snapshotRow.Name} ({snapshotRow.Timestamp}). Continue?",
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

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logsDirectory = new GSYNC.Data.Services.AppPathService().GetLogsDirectory();
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logsDirectory}\""));
        }
        catch
        {
        }
    }

    private async void OpenHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel?.IsChinese == true ? "历史与快照说明" : "History & Snapshots Help",
            Content = _viewModel?.IsChinese == true
                ? "此页用于查看同步记录、恢复快照，以及排查历史同步问题。"
                : "Use this page to review sync records, restore snapshots, and investigate previous sync issues.",
            CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }
}
