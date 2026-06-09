using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedPageKey = "library";

    public Type ResolvePageType()
    {
        return SelectedPageKey switch
        {
            "library" => typeof(Pages.HomePage),
            "targets" => typeof(Pages.SyncTargetsPage),
            "variables" => typeof(Pages.VariablesPage),
            "history" => typeof(Pages.HistoryPage),
            "settings" => typeof(Pages.SettingsPage),
            _ => typeof(Pages.HomePage),
        };
    }
}
