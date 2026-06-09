using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class VariablesPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _showParseError;
}
