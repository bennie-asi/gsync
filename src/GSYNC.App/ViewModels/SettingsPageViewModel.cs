using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _themeName = "Dark";
}
