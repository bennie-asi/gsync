using GSYNC.App.ViewModels;
using GSYNC.App.Primitives;
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

            LibraryTable.Columns =
            [
                new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "游戏" : "Game", BindingPath = nameof(LibraryGameRow.Name), Width = 240, MinWidth = 160, IsBold = true, IsFillColumn = true },
                new ResizableTableColumn { Key = "source", Header = _viewModel.IsChinese ? "来源" : "Source", BindingPath = nameof(LibraryGameRow.Source), Width = 98, MinWidth = 82 },
                new ResizableTableColumn { Key = "itemCount", Header = _viewModel.IsChinese ? "条目" : "Items", BindingPath = nameof(LibraryGameRow.ItemCount), Width = 68, MinWidth = 56 },
                new ResizableTableColumn { Key = "localStatus", Header = _viewModel.IsChinese ? "本地" : "Local", BindingPath = nameof(LibraryGameRow.LocalStatus), Width = 104, MinWidth = 84, CellTemplate = (DataTemplate)Resources["LibraryStatusCellTemplate"] },
                new ResizableTableColumn { Key = "remoteStatus", Header = _viewModel.IsChinese ? "远端" : "Remote", BindingPath = nameof(LibraryGameRow.RemoteStatus), Width = 104, MinWidth = 84, CellTemplate = (DataTemplate)Resources["LibraryRemoteStatusCellTemplate"] },
                new ResizableTableColumn { Key = "lastSync", Header = _viewModel.IsChinese ? "上次同步" : "Last Sync", BindingPath = nameof(LibraryGameRow.LastSync), Width = 104, MinWidth = 92 },
                new ResizableTableColumn { Key = "target", Header = _viewModel.IsChinese ? "目标" : "Target", BindingPath = nameof(LibraryGameRow.Target), Width = 122, MinWidth = 100 },
                new ResizableTableColumn { Key = "action", Header = _viewModel.IsChinese ? "操作" : "Action", BindingPath = nameof(LibraryGameRow.Name), Width = 164, MinWidth = 152, CellTemplate = (DataTemplate)Resources["LibraryActionCellTemplate"] },
            ];

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
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
        LibraryTable.Title = _viewModel.TableTitle;
        LibraryTable.Subtitle = _viewModel.TableSubtitle;
        LibraryTable.FooterText = _viewModel.TableFooterText;
        OverviewSheet.Title = _viewModel.OverviewTitle;
        OverviewSubtitleTextBlock.Text = _viewModel.OverviewSubtitle;
        ActivitySheet.Title = _viewModel.ActivityTitle;
        ActivitySubtitleTextBlock.Text = _viewModel.ActivitySubtitle;
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

    private void OpenGameDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LibraryGameRow row })
        {
            Frame?.Navigate(typeof(GameDetailsPage), row.Name);
            return;
        }

        Frame?.Navigate(typeof(GameDetailsPage));
    }
}
