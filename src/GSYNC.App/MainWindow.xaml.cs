using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GSYNC.App;

public partial class MainWindow : Window
{
    private readonly ILocalizationService _localizationService;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBarControl);

        _localizationService = App.GetService<ILocalizationService>();
        _viewModel = App.GetService<MainWindowViewModel>();
        RootLayout.DataContext = _viewModel;

        NavRailControl.NavigationRequested += NavRailControl_OnNavigationRequested;
        ContentFrame.Navigated += ContentFrame_OnNavigated;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;

        ApplyLocalizedShellText();
        _viewModel.SelectedPageKey = "library";
        NavigateToCurrentPage();
    }

    private void NavRailControl_OnNavigationRequested(object? sender, NavigationRequestedEventArgs e)
    {
        if (string.Equals(_viewModel.SelectedPageKey, e.Key, StringComparison.Ordinal))
        {
            return;
        }

        _viewModel.SelectedPageKey = e.Key;
        NavigateToCurrentPage();
    }

    private void ContentFrame_OnNavigated(object sender, NavigationEventArgs e)
    {
        var resolvedPageKey = _viewModel.ResolvePageKey(e.SourcePageType);
        if (!string.Equals(_viewModel.SelectedPageKey, resolvedPageKey, StringComparison.Ordinal))
        {
            _viewModel.SelectedPageKey = resolvedPageKey;
        }
    }

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalizedShellText();
    }

    private void ApplyLocalizedShellText()
    {
        NavRailControl.ApplyLocalization(_localizationService);
    }

    private void NavigateToCurrentPage()
    {
        ContentFrame.Navigate(_viewModel.ResolvePageType());
    }
}
