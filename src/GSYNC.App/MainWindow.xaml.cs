using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GSYNC.App;

public partial class MainWindow : Window
{
    private readonly ILocalizationService _localizationService;
    private readonly MainWindowViewModel _viewModel;
    private bool _isNavigating;

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
        ContentFrame.NavigationFailed += ContentFrame_OnNavigationFailed;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;

        ApplyLocalizedShellText();

        Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        BeginStartupNavigation();
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
        _isNavigating = false;
        Log.Information("Navigation completed to {PageType}.", e.SourcePageType?.Name ?? "<unknown>");

        if (e.SourcePageType != typeof(Pages.PageLoadErrorPage))
        {
            _viewModel.ClearStartupOverride();
        }

        var resolvedPageKey = _viewModel.ResolvePageKey(e.SourcePageType);
        if (!string.Equals(_viewModel.SelectedPageKey, resolvedPageKey, StringComparison.Ordinal))
        {
            _viewModel.SelectedPageKey = resolvedPageKey;
        }
    }

    private void ContentFrame_OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        _isNavigating = false;
        var failedPageType = e.SourcePageType?.Name ?? "<unknown>";
        Log.Error(e.Exception, "Navigation failed for {PageType}.", failedPageType);
        e.Handled = true;

        ShowPageLoadError(
            _viewModel.SelectedPageKey,
            $"{failedPageType} 无法打开",
            "页面导航已被安全降级，应用主壳层和导航功能仍然可用。",
            e.Exception?.Message ?? "未提供异常详细信息。",
            "navigation");
    }

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalizedShellText();
    }

    private void ApplyLocalizedShellText()
    {
        NavRailControl.ApplyLocalization(_localizationService);
    }

    private void BeginStartupNavigation()
    {
        Log.Information("Main window activated; beginning staged startup navigation.");
        NavigateToCurrentPage();
        _ = App.Current.RunDeferredStartupAsync();
    }

    private void NavigateToCurrentPage()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        var pageType = _viewModel.ResolvePageType();
        Log.Information("Navigating to {PageType} for page key {PageKey}.", pageType.Name, _viewModel.SelectedPageKey);

        try
        {
            ContentFrame.Navigate(pageType);
        }
        catch (Exception exception)
        {
            _isNavigating = false;
            Log.Error(exception, "Navigation threw during page creation for {PageType}.", pageType.Name);
            ShowPageLoadError(
                _viewModel.SelectedPageKey,
                $"{pageType.Name} 初始化失败",
                "页面初始化在构造或同步加载阶段发生异常，应用已保留主窗口壳层。",
                exception.Message,
                "initialization");
        }
    }

    private void ShowPageLoadError(string requestedPageKey, string title, string message, string detail, string failureStage)
    {
        Log.Warning("Showing page load error for {PageKey} at stage {FailureStage}: {Title}", requestedPageKey, failureStage, title);
        _viewModel.ReportStartupDegraded($"{title} · 已切换到安全降级视图");

        ContentFrame.Navigate(
            typeof(Pages.PageLoadErrorPage),
            new Pages.PageLoadErrorContext(
                requestedPageKey,
                title,
                message,
                $"阶段: {failureStage}\n详情: {detail}",
                "返回 Library",
                "重试页面"));
    }
}
