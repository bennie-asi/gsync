using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class SyncTargetsPage : Page
{
    private readonly SyncTargetsPageViewModel _viewModel;

    public SyncTargetsPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<SyncTargetsPageViewModel>();
        DataContext = _viewModel;

        TargetsTable.Columns =
        [
            new ResizableTableColumn { Key = "definitionIcon", Header = _viewModel.IsChinese ? "定义" : "Def", BindingPath = nameof(SyncTargetRow.DefinitionIcon), Width = 56, MinWidth = 40 },
            new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "名称" : "Name", BindingPath = nameof(SyncTargetRow.Name), Width = 180, MinWidth = 120, IsBold = true },
            new ResizableTableColumn { Key = "detail", Header = _viewModel.IsChinese ? "详情" : "Detail", BindingPath = nameof(SyncTargetRow.Detail), Width = 260, MinWidth = 180, IsFillColumn = true },
            new ResizableTableColumn { Key = "status", Header = _viewModel.IsChinese ? "状态" : "Status", BindingPath = nameof(SyncTargetRow.Status), Width = 120, MinWidth = 96 },
        ];
    }
}
