using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private string _selectedPageKey = "library";

    public MainWindowViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public string WindowTitle => _localizationService.GetString("App.WindowTitle");

    public string CurrentSectionSubtitle => SelectedPageKey switch
    {
        "library" => _localizationService.GetString("MainWindow.Subtitle.Library"),
        "wizard" => _localizationService.GetString("MainWindow.Subtitle.Wizard"),
        "conflict" => _localizationService.GetString("MainWindow.Subtitle.Conflict"),
        "targets" => _localizationService.GetString("MainWindow.Subtitle.Targets"),
        "variables" => _localizationService.GetString("MainWindow.Subtitle.Variables"),
        "history" => _localizationService.GetString("MainWindow.Subtitle.History"),
        "settings" => _localizationService.GetString("MainWindow.Subtitle.Settings"),
        _ => _localizationService.GetString("MainWindow.Subtitle.Default"),
    };

    public string GlobalStatusText => SelectedPageKey switch
    {
        "wizard" => _localizationService.GetString("MainWindow.Status.Wizard"),
        "conflict" => _localizationService.GetString("MainWindow.Status.Conflict"),
        "history" => _localizationService.GetString("MainWindow.Status.History"),
        "targets" => _localizationService.GetString("MainWindow.Status.Targets"),
        "variables" => _localizationService.GetString("MainWindow.Status.Variables"),
        _ => _localizationService.GetString("MainWindow.Status.Ready"),
    };

    partial void OnSelectedPageKeyChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(GlobalStatusText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(GlobalStatusText));
    }

    public Type ResolvePageType()
    {
        return SelectedPageKey switch
        {
            "library" => typeof(Pages.HomePage),
            "wizard" => typeof(Pages.AddGameWizardPage),
            "conflict" => typeof(Pages.ConflictResolutionPage),
            "targets" => typeof(Pages.SyncTargetsPage),
            "variables" => typeof(Pages.VariablesPage),
            "history" => typeof(Pages.HistoryPage),
            "settings" => typeof(Pages.SettingsPage),
            _ => typeof(Pages.HomePage),
        };
    }
}
