using GSYNC.App.Infrastructure;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class QueuePage : Page
{
    private QueuePageViewModel? _viewModel;

    public QueuePage()
    {
        InitializeComponent();
        Unloaded += QueuePage_Unloaded;
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            ThrowIfInitializationForcedToFail("queue");
            Log.Information("Initializing QueuePage.");
            _viewModel = App.GetService<QueuePageViewModel>();
            DataContext = _viewModel;

            QueueTable.Columns =
            [
                new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "任务" : "Job", BindingPath = nameof(QueueRow.DisplayName), Width = 220, MinWidth = 160, IsBold = true, IsFillColumn = true },
                new ResizableTableColumn { Key = "direction", Header = _viewModel.IsChinese ? "方向" : "Direction", BindingPath = nameof(QueueRow.Direction), Width = 110, MinWidth = 92 },
                new ResizableTableColumn { Key = "status", Header = _viewModel.IsChinese ? "状态" : "Status", BindingPath = nameof(QueueRow.Status), Width = 110, MinWidth = 92, CellTemplate = (DataTemplate)Resources["QueueStatusTemplate"] },
                new ResizableTableColumn { Key = "enqueued", Header = _viewModel.IsChinese ? "入队时间" : "Enqueued", BindingPath = nameof(QueueRow.EnqueuedAt), Width = 112, MinWidth = 92 },
                new ResizableTableColumn { Key = "progress", Header = _viewModel.IsChinese ? "进度" : "Progress", BindingPath = nameof(QueueRow.Progress), Width = 100, MinWidth = 82 },
            ];

            QueueTable.RowInvoked -= QueueTable_RowInvoked;
            QueueTable.RowInvoked += QueueTable_RowInvoked;

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            _viewModel.Refresh();
            Log.Information("QueuePage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "QueuePage initialization failed.");
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

    private void QueueTable_RowInvoked(object? sender, object item)
    {
        if (_viewModel is not null && item is QueueRow row)
        {
            _viewModel.SelectRow(row);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Refresh();
    }

    private void GoToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(HomePage));
    }

    private async void CancelSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedJob is not { IsActive: true } row)
        {
            return;
        }

        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel.IsChinese ? "取消活跃任务" : "Cancel Active Job",
            Content = _viewModel.IsChinese
                ? $"确认取消正在运行的任务“{row.DisplayName}”吗？"
                : $"Cancel the currently running job \"{row.DisplayName}\"?",
            PrimaryButtonText = _viewModel.IsChinese ? "确认取消" : "Yes, Cancel",
            CloseButtonText = _viewModel.IsChinese ? "继续运行" : "Keep Running",
            DefaultButton = ContentDialogButton.Close,
        });

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.CancelSelectedCommand.Execute(row);
        }
    }

    private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedJob is { IsActive: false } row)
        {
            _viewModel.RemoveSelectedCommand.Execute(row);
        }
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }

    private void QueuePage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        QueueTable.RowInvoked -= QueueTable_RowInvoked;
        _viewModel.Dispose();
        _viewModel = null;
    }
}
