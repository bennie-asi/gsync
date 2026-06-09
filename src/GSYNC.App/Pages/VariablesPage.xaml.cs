using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class VariablesPage : Page
{
    public VariablesPage()
    {
        InitializeComponent();
        DataContext = new VariablesPageViewModel();
    }
}
