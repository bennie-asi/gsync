using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class SyncTargetsPage : Page
{
    public SyncTargetsPage()
    {
        InitializeComponent();
        DataContext = new SyncTargetsPageViewModel();
    }
}
