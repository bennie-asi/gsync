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
                new ResizableTableColumn { Key = "device", Header = _viewModel.IsChinese ? "设备" : "Device", BindingPath = nameof(HistoryRow.Device), Width = 140, MinWidth = 120 },
                new ResizableTableColumn { Key = "size", Header = _viewModel.IsChinese ? "大小" : "Size", BindingPath = nameof(HistoryRow.Size), Width = 84, MinWidth = 72 },
            ];

            HistoryTable.RowInvoked -= HistoryTable_RowInvoked;
            HistoryTable.RowInvoked += HistoryTable_RowInvoked;

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
        if (_viewModel is null)
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
