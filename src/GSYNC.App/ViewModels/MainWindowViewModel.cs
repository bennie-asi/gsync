using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly SyncTargetStore _syncTargetStore;
    private readonly IEnumerable<IStorageProvider> _storageProviders;
    private string? _startupStatusOverride;

    private readonly record struct TargetStatus(string Name, bool IsOnline, bool IsLocal);
    private IReadOnlyList<TargetStatus> _lastKnownStatuses = [];

    [ObservableProperty]
    private string _selectedPageKey = string.Empty;

    [ObservableProperty]
    private string _webDavStatusText = string.Empty;

    [ObservableProperty]
    private string _localStatusText = string.Empty;

    [ObservableProperty]
    private string _targetNameText = string.Empty;

    [ObservableProperty]
    private string _syncStatusText = string.Empty;

    public MainWindowViewModel(
        ILocalizationService localizationService,
        SyncTargetStore syncTargetStore,
        IEnumerable<IStorageProvider> storageProviders)
    {
        _localizationService = localizationService;
        _syncTargetStore = syncTargetStore;
        _storageProviders = storageProviders;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _syncTargetStore.Changed += OnSyncTargetsChanged;

        var isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        var checking = isChinese ? "检查中…" : "Checking…";
        WebDavStatusText = checking;
        LocalStatusText = string.Empty;
        TargetNameText = isChinese ? "---" : "---";
        SyncStatusText = checking;
        SelectedPageKey = GetInitialPageKey();
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
        "queue" => _localizationService.GetString("MainWindow.Subtitle.Queue"),
        "settings" => _localizationService.GetString("MainWindow.Subtitle.Settings"),
        _ => _localizationService.GetString("MainWindow.Subtitle.Default"),
    };

    public string GlobalStatusText => _startupStatusOverride ?? (SelectedPageKey switch
    {
        "wizard" => _localizationService.GetString("MainWindow.Status.Wizard"),
        "conflict" => _localizationService.GetString("MainWindow.Status.Conflict"),
        "history" => _localizationService.GetString("MainWindow.Status.History"),
        "targets" => _localizationService.GetString("MainWindow.Status.Targets"),
        "variables" => _localizationService.GetString("MainWindow.Status.Variables"),
        "queue" => _localizationService.GetString("MainWindow.Status.Queue"),
        _ => _localizationService.GetString("MainWindow.Status.Ready"),
    });

    public async Task RefreshTargetStatusAsync(CancellationToken cancellationToken = default)
    {
        var targets = _syncTargetStore.List();
        var statuses = new List<TargetStatus>(targets.Count);

        foreach (var target in targets)
        {
            var isLocal = string.Equals(target.ProviderId, "local-folder", StringComparison.OrdinalIgnoreCase);
            bool isOnline;
            if (isLocal)
            {
                var rootPath = target.Settings.GetValueOrDefault("rootPath", string.Empty);
                isOnline = !string.IsNullOrWhiteSpace(rootPath) && Directory.Exists(rootPath);
            }
            else
            {
                var provider = _storageProviders.FirstOrDefault(
                    p => string.Equals(p.ProviderId, target.ProviderId, StringComparison.OrdinalIgnoreCase));
                if (provider is null)
                {
                    isOnline = false;
                }
                else
                {
                    try
                    {
                        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        probeCts.CancelAfter(TimeSpan.FromSeconds(6));
                        var result = await provider.TestConnectionAsync(target.Settings, probeCts.Token);
                        isOnline = result.IsSuccess;
                    }
                    catch
                    {
                        isOnline = false;
                    }
                }
            }

            statuses.Add(new TargetStatus(target.Name, isOnline, isLocal));
        }

        _lastKnownStatuses = statuses;
        ApplyStatusToProperties();
    }

    partial void OnSelectedPageKeyChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(GlobalStatusText));
    }

    public void ReportStartupDegraded(string message)
    {
        _startupStatusOverride = message;
        OnPropertyChanged(nameof(GlobalStatusText));
    }

    public void ClearStartupOverride()
    {
        if (_startupStatusOverride is null)
        {
            return;
        }

        _startupStatusOverride = null;
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
            "queue" => typeof(Pages.QueuePage),
            "settings" => typeof(Pages.SettingsPage),
            _ => typeof(Pages.HomePage),
        };
    }

    public string ResolvePageKey(Type? pageType)
    {
        if (pageType == typeof(Pages.PageLoadErrorPage))
        {
            return SelectedPageKey;
        }

        if (pageType == typeof(Pages.AddGameWizardPage))
        {
            return "wizard";
        }

        if (pageType == typeof(Pages.ConflictResolutionPage))
        {
            return "conflict";
        }

        if (pageType == typeof(Pages.SyncTargetsPage))
        {
            return "targets";
        }

        if (pageType == typeof(Pages.VariablesPage))
        {
            return "variables";
        }

        if (pageType == typeof(Pages.HistoryPage))
        {
            return "history";
        }

        if (pageType == typeof(Pages.QueuePage))
        {
            return "queue";
        }

        if (pageType == typeof(Pages.SettingsPage))
        {
            return "settings";
        }

        return "library";
    }

    private void ApplyStatusToProperties()
    {
        var isChinese = _localizationService.CurrentLanguageTag == "zh-CN";
        var remoteTargets = _lastKnownStatuses.Where(s => !s.IsLocal).ToList();
        var localTargets = _lastKnownStatuses.Where(s => s.IsLocal).ToList();

        WebDavStatusText = BuildGroupStatus(remoteTargets, isChinese ? "远端" : "Remote", isChinese);
        LocalStatusText = BuildGroupStatus(localTargets, isChinese ? "本地" : "Local", isChinese);

        var defaultTarget = _syncTargetStore.GetDefault();
        TargetNameText = defaultTarget?.Name ?? (isChinese ? "无目标" : "No target");

        if (_lastKnownStatuses.Count == 0)
        {
            SyncStatusText = isChinese ? "无目标" : "No targets";
        }
        else if (_lastKnownStatuses.All(s => s.IsOnline))
        {
            SyncStatusText = isChinese ? "全部就绪" : "All ready";
        }
        else
        {
            var offlineCount = _lastKnownStatuses.Count(s => !s.IsOnline);
            SyncStatusText = isChinese ? $"{offlineCount} 个目标离线" : $"{offlineCount} target{(offlineCount == 1 ? "" : "s")} offline";
        }
    }

    private static string BuildGroupStatus(List<TargetStatus> targets, string groupLabel, bool isChinese)
    {
        if (targets.Count == 0)
        {
            return string.Empty;
        }

        if (targets.Count == 1)
        {
            var onlineText = isChinese
                ? (targets[0].IsOnline ? "在线" : "离线")
                : (targets[0].IsOnline ? "Online" : "Offline");
            return $"{targets[0].Name} · {onlineText}";
        }

        var onlineCount = targets.Count(s => s.IsOnline);
        return isChinese
            ? $"{groupLabel} {onlineCount}/{targets.Count} 在线"
            : $"{groupLabel} {onlineCount}/{targets.Count} online";
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(GlobalStatusText));
        ApplyStatusToProperties();
    }

    private void OnSyncTargetsChanged(object? sender, EventArgs e)
    {
        _ = RefreshTargetStatusAsync();
    }

    private static string GetInitialPageKey()
    {
        var configured = Environment.GetEnvironmentVariable("GSYNC_START_PAGE")?.Trim().ToLowerInvariant();
        return configured switch
        {
            "library" or "settings" or "history" or "queue" or "targets" or "variables" or "wizard" or "conflict" => configured,
            _ => "library",
        };
    }
}
