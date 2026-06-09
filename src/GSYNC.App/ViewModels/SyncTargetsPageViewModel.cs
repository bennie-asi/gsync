using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class SyncTargetsPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _showFailureState;
}
