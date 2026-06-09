using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBarControl);
        _viewModel = App.GetService<MainWindowViewModel>();
        _viewModel.SelectedPageKey = "library";
        NavigateToCurrentPage();
    }

    private void NavigationView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        _viewModel.SelectedPageKey = args.IsSettingsSelected
            ? "settings"
            : args.SelectedItemContainer?.Tag as string ?? _viewModel.SelectedPageKey;

        NavigateToCurrentPage();
    }

    private void NavigateToCurrentPage()
    {
        ContentFrame.Navigate(_viewModel.ResolvePageType());
    }
}
