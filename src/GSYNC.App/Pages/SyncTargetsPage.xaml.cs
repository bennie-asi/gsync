using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class SyncTargetsPage : Page
{
    private SyncTargetsPageViewModel? _viewModel;

    public SyncTargetsPage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            Log.Information("Initializing SyncTargetsPage.");
            _viewModel = App.GetService<SyncTargetsPageViewModel>();
            DataContext = _viewModel;

            TargetsTable.Columns =
            [
                new ResizableTableColumn { Key = "definitionIcon", Header = _viewModel.IsChinese ? "定义" : "Def", BindingPath = nameof(SyncTargetRow.DefinitionIcon), Width = 56, MinWidth = 40 },
                new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "名称" : "Name", BindingPath = nameof(SyncTargetRow.Name), Width = 180, MinWidth = 120, IsBold = true },
                new ResizableTableColumn { Key = "detail", Header = _viewModel.IsChinese ? "详情" : "Detail", BindingPath = nameof(SyncTargetRow.Detail), Width = 260, MinWidth = 180, IsFillColumn = true },
                new ResizableTableColumn { Key = "status", Header = _viewModel.IsChinese ? "状态" : "Status", BindingPath = nameof(SyncTargetRow.Status), Width = 120, MinWidth = 96 },
            ];

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            Log.Information("SyncTargetsPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "SyncTargetsPage initialization failed.");
            MainContentRoot.Visibility = Visibility.Collapsed;
            InitializationErrorPanel.Visibility = Visibility.Visible;
            InitializationErrorMessage.Text = exception.Message;
        }
    }

    private void RetryInitialization_Click(object sender, RoutedEventArgs e)
    {
        TryInitializePage();
    }
}
