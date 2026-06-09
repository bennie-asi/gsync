using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class HistoryPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isEmptyState;
}
