using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly HistoryPageViewModel _viewModel;

    public HistoryPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<HistoryPageViewModel>();
        DataContext = _viewModel;

        HistoryTable.Columns =
        [
            new ResizableTableColumn { Key = "time", Header = _viewModel.IsChinese ? "时间" : "Time", BindingPath = nameof(HistoryRow.Time), Width = 84, MinWidth = 70 },
            new ResizableTableColumn { Key = "game", Header = _viewModel.IsChinese ? "游戏" : "Game", BindingPath = nameof(HistoryRow.Game), Width = 180, MinWidth = 140, IsBold = true, IsFillColumn = true },
            new ResizableTableColumn { Key = "direction", Header = _viewModel.IsChinese ? "方向" : "Direction", BindingPath = nameof(HistoryRow.Direction), Width = 130, MinWidth = 96 },
            new ResizableTableColumn { Key = "result", Header = _viewModel.IsChinese ? "结果" : "Result", BindingPath = nameof(HistoryRow.Result), Width = 100, MinWidth = 90 },
            new ResizableTableColumn { Key = "target", Header = _viewModel.IsChinese ? "目标" : "Target", BindingPath = nameof(HistoryRow.Target), Width = 180, MinWidth = 120, IsFillColumn = true },
            new ResizableTableColumn { Key = "device", Header = _viewModel.IsChinese ? "设备" : "Device", BindingPath = nameof(HistoryRow.Device), Width = 140, MinWidth = 110 },
            new ResizableTableColumn { Key = "size", Header = _viewModel.IsChinese ? "大小" : "Size", BindingPath = nameof(HistoryRow.Size), Width = 80, MinWidth = 70 },
        ];
    }
}
