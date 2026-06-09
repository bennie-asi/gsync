using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class GameDetailsPage : Page
{
    public GameDetailsPage()
    {
        InitializeComponent();
        DataContext = new GameDetailsViewModel();
    }
}
