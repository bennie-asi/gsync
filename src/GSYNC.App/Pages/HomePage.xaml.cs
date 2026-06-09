using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class HomePage : Page
{
    private readonly LibraryPageViewModel _viewModel = new();

    public HomePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OpenGameDetails_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(GameDetailsPage));
    }
}
