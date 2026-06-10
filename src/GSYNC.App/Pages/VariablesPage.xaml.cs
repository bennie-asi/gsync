using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GSYNC.App.Pages;

public sealed partial class VariablesPage : Page
{
    private VariablesPageViewModel? _viewModel;

    public VariablesPage()
    {
        InitializeComponent();
        TryInitializePage();
    }

    private void TryInitializePage()
    {
        try
        {
            Log.Information("Initializing VariablesPage.");
            _viewModel = App.GetService<VariablesPageViewModel>();
            DataContext = _viewModel;

            VariablesTable.Columns =
            [
                new ResizableTableColumn { Key = "name", Header = _viewModel.IsChinese ? "名称" : "Name", BindingPath = nameof(VariableRow.Name), Width = 180, MinWidth = 140, IsBold = true },
                new ResizableTableColumn { Key = "value", Header = _viewModel.IsChinese ? "值（解析后）" : "Value (Resolved)", BindingPath = nameof(VariableRow.Value), Width = 320, MinWidth = 220, IsFillColumn = true },
                new ResizableTableColumn { Key = "type", Header = _viewModel.IsChinese ? "类型" : "Type", BindingPath = nameof(VariableRow.Type), Width = 110, MinWidth = 90 },
            ];

            MainContentRoot.Visibility = Visibility.Visible;
            InitializationErrorPanel.Visibility = Visibility.Collapsed;
            Log.Information("VariablesPage initialized successfully.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "VariablesPage initialization failed.");
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
