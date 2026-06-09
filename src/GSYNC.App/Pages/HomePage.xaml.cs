using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class HomePage : Page
{
    private readonly LibraryPageViewModel _viewModel;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = App.GetService<LibraryPageViewModel>();
        DataContext = _viewModel;
        ApplyLocalizedStaticText();

        LibraryTable.Columns =
        [
            new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "游戏" : "Game", BindingPath = nameof(LibraryGameRow.Name), Width = 210, MinWidth = 140, IsBold = true, IsFillColumn = true },
            new ResizableTableColumn { Key = "source", Header = _viewModel.IsChinese ? "来源" : "Source", BindingPath = nameof(LibraryGameRow.Source), Width = 96, MinWidth = 80 },
            new ResizableTableColumn { Key = "itemCount", Header = _viewModel.IsChinese ? "条目" : "Items", BindingPath = nameof(LibraryGameRow.ItemCount), Width = 72, MinWidth = 56 },
            new ResizableTableColumn { Key = "localStatus", Header = _viewModel.IsChinese ? "本地" : "Local", BindingPath = nameof(LibraryGameRow.LocalStatus), Width = 96, MinWidth = 80, CellTemplate = (DataTemplate)Resources["LibraryStatusCellTemplate"] },
            new ResizableTableColumn { Key = "remoteStatus", Header = _viewModel.IsChinese ? "远端" : "Remote", BindingPath = nameof(LibraryGameRow.RemoteStatus), Width = 96, MinWidth = 80, CellTemplate = (DataTemplate)Resources["LibraryRemoteStatusCellTemplate"] },
            new ResizableTableColumn { Key = "lastSync", Header = _viewModel.IsChinese ? "上次同步" : "Last Sync", BindingPath = nameof(LibraryGameRow.LastSync), Width = 112, MinWidth = 96 },
            new ResizableTableColumn { Key = "target", Header = _viewModel.IsChinese ? "目标" : "Target", BindingPath = nameof(LibraryGameRow.Target), Width = 120, MinWidth = 100 },
            new ResizableTableColumn { Key = "action", Header = _viewModel.IsChinese ? "操作" : "Action", BindingPath = nameof(LibraryGameRow.Name), Width = 120, MinWidth = 120, CellTemplate = (DataTemplate)Resources["LibraryActionCellTemplate"] },
        ];
    }

    private void ApplyLocalizedStaticText()
    {
        PageHeader.Title = _viewModel.PageTitle;
        PageHeader.Subtitle = _viewModel.PageSubtitle;
        ToolbarFilters.SearchPlaceholder = _viewModel.SearchPlaceholder;
        ToolbarFilters.FilterPlaceholder = _viewModel.StatusFilterPlaceholder;
        AddGameButton.Content = _viewModel.AddGameText;
        SyncNowButton.Content = _viewModel.SyncNowText;
        LibraryTable.Title = _viewModel.TableTitle;
        LibraryTable.Subtitle = _viewModel.TableSubtitle;
        LibraryTable.FooterText = _viewModel.TableFooterText;
        OverviewSheet.Title = _viewModel.OverviewTitle;
        ActivityPanel.Title = _viewModel.ActivityTitle;
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
