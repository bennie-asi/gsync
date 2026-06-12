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
            new ResizableTableColumn { Key = "filename", Header = _viewModel.IsChinese ? "文件名" : "Filename", BindingPath = nameof(ConflictFileRow.Filename), Width = 240, MinWidth = 180, IsBold = true, IsFillColumn = true },
            new ResizableTableColumn { Key = "localStatus", Header = _viewModel.IsChinese ? "本地状态" : "Local Status", BindingPath = nameof(ConflictFileRow.LocalStatus), Width = 170, MinWidth = 120, IsFillColumn = true },
            new ResizableTableColumn { Key = "remoteStatus", Header = _viewModel.IsChinese ? "远端状态" : "Remote Status", BindingPath = nameof(ConflictFileRow.RemoteStatus), Width = 170, MinWidth = 120, IsFillColumn = true },
            new ResizableTableColumn { Key = "action", Header = _viewModel.IsChinese ? "决策" : "Decision", BindingPath = nameof(ConflictFileRow.ActionText), Width = 130, MinWidth = 100 },
        ];

        ConflictFilesTable.RowInvoked += ConflictFilesTable_RowInvoked;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConflictNavigationContext context)
        {
            _viewModel.LoadFrom(context);
        }
    }

    private void ConflictFilesTable_RowInvoked(object? sender, object item)
    {
        if (item is not ConflictFileRow row)
        {
            return;
        }

        var next = row.Action switch
        {
            ConflictResolutionAction.Undecided => ConflictResolutionAction.KeepLocal,
            ConflictResolutionAction.KeepLocal => ConflictResolutionAction.KeepRemote,
            ConflictResolutionAction.KeepRemote => ConflictResolutionAction.Skip,
            _ => ConflictResolutionAction.Undecided,
        };
        _viewModel.SetAction(row.Filename, next);
    }

    private async void UseLocalButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAllActions(ConflictResolutionAction.KeepLocal);
        await ResolveAsync();
    }

    private async void UseRemoteButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAllActions(ConflictResolutionAction.KeepRemote);
        await ResolveAsync();
    }

    private async void KeepBothButton_Click(object sender, RoutedEventArgs e)
    {
        await ResolveAsync();
    }

    private async void ApplyRemoteDecisionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync(
            _viewModel.IsChinese ? "应用远端覆盖决策" : "Apply remote overwrite decisions",
            _viewModel.IsChinese
                ? "仅已设置为“保留远端”的文件会下载覆盖本地版本，且在覆盖前会自动创建本地快照。是否继续？"
                : "Only files marked as keep-remote will be downloaded and overwrite the local version, with a local snapshot created first. Continue?"))
        {
            return;
        }

        await ResolveAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(HomePage));
        }
    }

    private async Task ResolveAsync()
    {
        var error = await _viewModel.ResolveAsync();
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = error is null
                ? (_viewModel.IsChinese ? "任务已加入队列" : "Job queued")
                : (_viewModel.IsChinese ? "操作失败" : "Operation failed"),
            Content = error ?? (_viewModel.IsChinese
                ? "冲突解决任务已开始处理，进度与结果可在历史页查看。"
                : "The conflict resolution job has been queued. Track progress and results on the History page."),
            CloseButtonText = _viewModel.IsChinese ? "关闭" : "Close",
        });
        await dialog.ShowAsync();

        if (error is null && Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
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
