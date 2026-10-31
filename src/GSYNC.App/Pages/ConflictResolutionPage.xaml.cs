using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using GSYNC.App.Infrastructure;
using GSYNC.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GSYNC.App.Pages;

public sealed partial class ConflictResolutionPage : Page
{
    private readonly ConflictResolutionViewModel _viewModel;

    public ConflictResolutionPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<ConflictResolutionViewModel>();
        DataContext = _viewModel;

        ConflictFilesTable.Columns =
        [
            new ResizableTableColumn { Key = "filename", Header = _viewModel.IsChinese ? "文件名" : "Filename", BindingPath = nameof(ConflictFileRow.Filename), Width = 240, MinWidth = 180, IsFillColumn = true, CellTemplate = (DataTemplate)Resources["ConflictFilenameTemplate"] },
            new ResizableTableColumn { Key = "localStatus", Header = _viewModel.IsChinese ? "本地状态" : "Local Status", BindingPath = nameof(ConflictFileRow.LocalStatus), Width = 170, MinWidth = 120, IsFillColumn = true },
            new ResizableTableColumn { Key = "remoteStatus", Header = _viewModel.IsChinese ? "远端状态" : "Remote Status", BindingPath = nameof(ConflictFileRow.RemoteStatus), Width = 170, MinWidth = 120, IsFillColumn = true },
            new ResizableTableColumn { Key = "action", Header = _viewModel.IsChinese ? "决策" : "Decision", BindingPath = nameof(ConflictFileRow.ActionText), Width = 150, MinWidth = 120, CellTemplate = (DataTemplate)Resources["ConflictActionTemplate"] },
        ];

    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConflictNavigationContext context)
        {
            _viewModel.LoadFrom(context);
        }
    }

    private void ConflictHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 820;
        Grid.SetRow(ConflictBulkActions, narrow ? 1 : 0);
        Grid.SetColumn(ConflictBulkActions, narrow ? 0 : 1);
        Grid.SetColumnSpan(ConflictBulkActions, narrow ? 2 : 1);
    }

    private void KeepLocalDecision_Click(object sender, RoutedEventArgs e) => SetDecision(sender, ConflictResolutionAction.KeepLocal);

    private void KeepRemoteDecision_Click(object sender, RoutedEventArgs e) => SetDecision(sender, ConflictResolutionAction.KeepRemote);

    private void RemoveRemoteDecision_Click(object sender, RoutedEventArgs e) => SetDecision(sender, ConflictResolutionAction.RemoveRemote);

    private void SkipDecision_Click(object sender, RoutedEventArgs e) => SetDecision(sender, ConflictResolutionAction.Skip);

    private void SelectRemoteRemovals_Click(object sender, RoutedEventArgs e) => _viewModel.SelectRemoteOnlyForRemoval();

    private void SetDecision(object sender, ConflictResolutionAction action)
    {
        if (sender is MenuFlyoutItem { Tag: string filename }) _viewModel.SetAction(filename, action);
    }

    private void UseLocalButton_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetAllActions(ConflictResolutionAction.KeepLocal);

    private void UseRemoteButton_Click(object sender, RoutedEventArgs e)
        => _viewModel.SetAllActions(ConflictResolutionAction.KeepRemote);

    private async void ApplyRemoteDecisionsButton_Click(object sender, RoutedEventArgs e)
    {
        await ResolveAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.InstanceId is { } id) Frame?.Navigate(typeof(GameDetailsPage), id.ToString("D"));
        else Frame?.Navigate(typeof(HomePage));
    }

    private async Task ResolveAsync()
    {
        var local = _viewModel.ConflictFiles.Count(row => row.Action == ConflictResolutionAction.KeepLocal);
        var remote = _viewModel.ConflictFiles.Count(row => row.Action == ConflictResolutionAction.KeepRemote);
        var remove = _viewModel.ConflictFiles.Count(row => row.Action == ConflictResolutionAction.RemoveRemote);
        var remaining = _viewModel.ConflictFiles.Count - local - remote - remove;
        if (local + remote + remove == 0)
        {
            var nothing = DialogStyler.Apply(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = _viewModel.IsChinese ? "没有要处理的文件" : "No files selected",
                Content = _viewModel.IsChinese ? "请先选择要保留的版本。" : "Choose a version to keep first.",
                CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
            });
            await nothing.ShowAsync();
            return;
        }
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "确认处理文件" : "Confirm file decisions",
            _viewModel.IsChinese
                ? $"保留本地：{local} 个，将写入同步目标。\n保留远端：{remote} 个，将写入本地。\n移除远端文件：{remove} 个，将先备份到本机快照。\n暂不处理：{remaining} 个。"
                : $"Keep local: {local} files will be written to the target.\nKeep remote: {remote} files will be written locally.\nRemove remote-only files: {remove}, backed up to a local snapshot first.\nUnchanged for now: {remaining} files.")) return;

        var error = await _viewModel.ResolveAsync();
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = error is null
                ? (_viewModel.IsChinese ? "任务已加入队列" : "Job queued")
                : (_viewModel.IsChinese ? "操作失败" : "Operation failed"),
            Content = error ?? (_viewModel.IsChinese
                ? "任务已加入队列；完成后的结果会显示在历史中。"
                : "The job is queued; its result will appear in History when complete."),
            PrimaryButtonText = error is null ? (_viewModel.IsChinese ? "查看队列" : "View queue") : string.Empty,
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        var result = await dialog.ShowAsync();
        if (error is null && result == ContentDialogResult.Primary)
        {
            Frame?.Navigate(typeof(QueuePage));
        }
        else if (error is null && _viewModel.InstanceId is { } id) Frame?.Navigate(typeof(GameDetailsPage), id.ToString("D"));
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
}
