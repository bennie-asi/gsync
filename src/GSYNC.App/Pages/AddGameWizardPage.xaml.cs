using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Pages;

public sealed partial class AddGameWizardPage : Page
{
    private readonly AddGameWizardViewModel _viewModel;

    public AddGameWizardPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<AddGameWizardViewModel>();
        DataContext = _viewModel;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(HomePage));
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoNext();
    }
}
