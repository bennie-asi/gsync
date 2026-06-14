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
            RefreshButton.Click -= RefreshButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            SyncNowButton.Click -= SyncNowButton_Click;
            SyncNowButton.Click += SyncNowButton_Click;
            LibraryTable.RowDoubleInvoked -= LibraryTable_RowDoubleInvoked;
            LibraryTable.RowDoubleInvoked += LibraryTable_RowDoubleInvoked;
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
            Frame?.Navigate(typeof(GameDetailsPage), row.InstanceId.ToString("D"));
            return;
        }

        Frame?.Navigate(typeof(GameDetailsPage));
    }

    private void LibraryTable_RowDoubleInvoked(object? sender, object item)
    {
        if (item is LibraryGameRow row)
        {
            Frame?.Navigate(typeof(GameDetailsPage), row.InstanceId.ToString("D"));
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.LoadAsync();
    }

    private async void SortButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleSort();
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _viewModel?.IsChinese == true ? "排序已切换" : "Sort toggled",
            Content = _viewModel?.IsChinese == true
                ? "当前已在名称升序和降序之间切换。"
                : "The current sort has been toggled between ascending and descending by name.",
            CloseButtonText = _viewModel?.IsChinese == true ? "关闭" : "Close",
        });
        await dialog.ShowAsync();
    }

    private async void SyncNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.SyncAllAsync();
        await _viewModel.LoadAsync();
    }

    private async void ShowRowActions_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { DataContext: LibraryGameRow row })
        {
            return;
        }

        var isChinese = _viewModel.IsChinese;
        var dialog = DialogStyler.Apply(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = row.Name,
            Content = isChinese ? "选择要执行的操作。" : "Choose an action.",
            PrimaryButtonText = isChinese ? "打开详情" : "Open Details",
            SecondaryButtonText = isChinese ? "排队上传" : "Queue Upload",
            CloseButtonText = isChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Frame?.Navigate(typeof(GameDetailsPage), row.InstanceId.ToString("D"));
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            await _viewModel.QueueSyncForGameAsync(row.InstanceId, SyncDirection.Upload, row.Name);
        }
    }
}
