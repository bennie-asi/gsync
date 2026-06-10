using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

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
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string title && !string.IsNullOrWhiteSpace(title))
        {
            _viewModel.Title = title;
        }
    }

    private void OpenConflictResolution_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(ConflictResolutionPage));
    }
}
