using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GSYNC.App.Pages;

public sealed partial class PageLoadErrorPage : Page
{
    private string _requestedPageKey = "library";

    public PageLoadErrorPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is PageLoadErrorContext context)
        {
            _requestedPageKey = context.RequestedPageKey;
            ErrorTitleText.Text = context.Title;
            ErrorMessageText.Text = context.Message;
            ErrorDetailText.Text = context.Detail;
            ReturnButton.Content = context.ReturnButtonText;
            RetryButton.Content = context.RetryButtonText;
        }
    }

    private void ReturnButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var targetPage = _requestedPageKey == "library"
            ? typeof(HomePage)
            : typeof(HomePage);

        Frame?.Navigate(targetPage);
    }

    private void RetryButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var targetPage = _requestedPageKey switch
        {
            "settings" => typeof(SettingsPage),
            "targets" => typeof(SyncTargetsPage),
            "variables" => typeof(VariablesPage),
            "history" => typeof(HistoryPage),
            _ => typeof(HomePage),
        };

        Frame?.Navigate(targetPage);
    }
}

public sealed record PageLoadErrorContext(
    string RequestedPageKey,
    string Title,
    string Message,
    string Detail,
    string ReturnButtonText,
    string RetryButtonText);
