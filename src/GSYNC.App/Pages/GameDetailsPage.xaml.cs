using System.Diagnostics;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class GameDetailsPage : Page
{
    private readonly GameDetailsViewModel _viewModel;

    public GameDetailsPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<GameDetailsViewModel>();
        DataContext = _viewModel;

        ContentItemsTable.Columns =
        [
            new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "条目" : "Item", BindingPath = nameof(ContentItemRow.Name), Width = 200, MinWidth = 156, IsBold = true },
            new ResizableTableColumn { Key = "category", Header = _viewModel.IsChinese ? "分类" : "Category", BindingPath = nameof(ContentItemRow.Category), Width = 110, MinWidth = 92 },
            new ResizableTableColumn { Key = "resolvedPath", Header = _viewModel.IsChinese ? "解析路径" : "Resolved Path", BindingPath = nameof(ContentItemRow.ResolvedPath), Width = 340, MinWidth = 240, IsFillColumn = true },
            new ResizableTableColumn { Key = "policy", Header = _viewModel.IsChinese ? "策略" : "Policy", BindingPath = nameof(ContentItemRow.Policy), Width = 122, MinWidth = 102 },
            new ResizableTableColumn { Key = "status", Header = _viewModel.IsChinese ? "状态" : "Status", BindingPath = nameof(ContentItemRow.Status), Width = 118, MinWidth = 92, CellTemplate = (DataTemplate)Resources["ContentItemStatusTemplate"] },
            new ResizableTableColumn { Key = "enabled", Header = _viewModel.IsChinese ? "开关" : "Toggle", BindingPath = nameof(ContentItemRow.Enabled), Width = 84, MinWidth = 70, CellTemplate = (DataTemplate)Resources["ContentItemToggleTemplate"] },
        ];

        ContentItemsTable.RowInvoked += ContentItemsTable_RowInvoked;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string parameter && Guid.TryParse(parameter, out var instanceId))
        {
            _ = _viewModel.LoadAsync(instanceId);
        }
    }

    private void ContentItemsTable_RowInvoked(object? sender, object item)
    {
        if (item is ContentItemRow row)
        {
            _viewModel.SelectContentItem(row);
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var error = await _viewModel.QueueSyncAsync(SyncDirection.Upload);
        await ShowResultAsync(
            error,
            _viewModel.IsChinese ? "上传任务已加入队列" : "Upload queued",
            _viewModel.IsChinese
                ? "本地存档将上传到当前同步目标，进度可在历史页查看。"
                : "Local saves will be pushed to the active target. Track progress on the History page.");
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "下载远端存档" : "Download remote saves",
            _viewModel.IsChinese
                ? "远端数据将覆盖本地文件。覆盖前会自动创建本地快照，可随时恢复。是否继续？"
                : "Remote data will overwrite local files. A local snapshot is created automatically before overwriting. Continue?"))
        {
            return;
        }

        var error = await _viewModel.QueueSyncAsync(SyncDirection.Download);
        await ShowResultAsync(
            error,
            _viewModel.IsChinese ? "下载任务已加入队列" : "Download queued",
            _viewModel.IsChinese
                ? "下载完成后会自动写入同步记录与快照。"
                : "A sync record and snapshot will be written when the download completes.");
    }

    private async void OpenConflictResolution_Click(object sender, RoutedEventArgs e)
    {
        var (context, error) = await _viewModel.CompareAsync();
        if (error is not null)
        {
            await ShowResultAsync(error, string.Empty, string.Empty);
            return;
        }

        if (context is not null)
        {
            Frame?.Navigate(typeof(ConflictResolutionPage), context);
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "恢复最近快照" : "Restore latest snapshot",
            _viewModel.IsChinese
                ? "将使用最近一次快照覆盖本地存档文件。是否继续？"
                : "Local save files will be overwritten with the latest snapshot. Continue?"))
        {
            return;
        }

        var error = await _viewModel.RestoreLatestSnapshotAsync();
        await ShowResultAsync(
            error,
            _viewModel.IsChinese ? "快照恢复完成" : "Snapshot restored",
            _viewModel.IsChinese ? "本地文件已恢复到快照内容。" : "Local files were restored from the snapshot.");
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _viewModel.SelectedContentItem?.ResolvedPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\""));
            }
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to open folder for {Path}.", path);
        }
    }

    private async void TestPathButton_Click(object sender, RoutedEventArgs e)
    {
        var (exists, message) = _viewModel.TestSelectedPath();
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = exists
                ? (_viewModel.IsChinese ? "路径检查通过" : "Path check passed")
                : (_viewModel.IsChinese ? "路径检查未通过" : "Path check failed"),
            Content = message,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = _viewModel.IsChinese ? "继续" : "Continue",
            CloseButtonText = _viewModel.IsChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Close,
        });

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowResultAsync(string? error, string successTitle, string successMessage)
    {
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = error is null ? successTitle : (_viewModel.IsChinese ? "操作失败" : "Operation failed"),
            Content = error ?? successMessage,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async void NotYetEditableButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string content } && content == "×")
        {
            _viewModel.HideInspector();
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var nameBox = new TextBox
        {
            Header = isChinese ? "内容项名称" : "Content item name",
            PlaceholderText = isChinese ? "例如：附加截图" : "For example: Extra Screenshots",
        };
        var pathBox = new TextBox
        {
            Header = isChinese ? "路径模板" : "Path template",
            PlaceholderText = "%DOCUMENTS%/MyGame/Screenshots",
        };
        var browseFolderButton = new Button
        {
            Content = isChinese ? "选择文件夹" : "Browse Folder",
            Style = (Style)Application.Current.Resources["SecondaryToolbarButtonStyle"],
        };
        browseFolderButton.Click += async (_, _) =>
        {
            var selected = await PathPickerService.PickFolderAsync(App.Current.MainWindow);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                pathBox.Text = selected;
            }
        };
        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(nameBox);
        panel.Children.Add(pathBox);
        panel.Children.Add(browseFolderButton);

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isChinese ? "添加内容项" : "Add Content Item",
            Content = panel,
            PrimaryButtonText = isChinese ? "添加" : "Add",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(pathBox.Text))
        {
            return;
        }

        var error = await _viewModel.AddCustomContentItemAsync(nameBox.Text, pathBox.Text);
        await ShowResultAsync(
            error,
            isChinese ? "内容项已添加" : "Content item added",
            isChinese ? "新的内容项已经加入当前游戏定义。" : "The new content item has been added to the current game definition.");
    }

    private async void OpenActivityInfoButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowResultAsync(
            null,
            _viewModel.IsChinese ? "最近活动" : "Recent activity",
            _viewModel.IsChinese ? "下方左侧面板展示该游戏的最近同步活动。" : "The lower-left panel shows recent sync activity for this game.");
    }

    private async void OpenSnapshotInfoButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowResultAsync(
            null,
            _viewModel.IsChinese ? "快照列表" : "Snapshots",
            _viewModel.IsChinese ? "下方右侧面板展示该游戏可恢复的快照。" : "The lower-right panel shows recoverable snapshots for this game.");
    }
}
