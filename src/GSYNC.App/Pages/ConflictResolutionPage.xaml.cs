using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

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
        ];
    }
}
