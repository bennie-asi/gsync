using GSYNC.App.Infrastructure;
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
            if (string.Equals(Environment.GetEnvironmentVariable("GSYNC_FAIL_PAGE_INIT")?.Trim(), "queue", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Forced queue page initialization failure for diagnostics.");
            _viewModel = App.GetService<QueuePageViewModel>();
            DataContext = _viewModel;
            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            _viewModel.Refresh();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Queue page initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
    }

    private void GoToLibraryButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(HomePage));
    private void GoToHistoryButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(HistoryPage));

    private async void CancelJob_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || (sender as FrameworkElement)?.DataContext is not QueueRow { IsActive: true } row) return;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel.IsChinese ? "取消运行中的任务" : "Cancel running job",
            Content = _viewModel.IsChinese ? $"确认取消“{row.DisplayName}”吗？" : $"Cancel \"{row.DisplayName}\"?",
            PrimaryButtonText = _viewModel.IsChinese ? "确认取消" : "Cancel job",
            CloseButtonText = _viewModel.IsChinese ? "继续运行" : "Keep running",
            DefaultButton = ContentDialogButton.Close,
        });
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _viewModel?.Cancel(row);
    }

    private void RemoveJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is QueueRow { IsActive: false } row) _viewModel?.Remove(row);
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e) => TryInitializePage();

    private void QueuePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
