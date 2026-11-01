using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly UiSettingsStore _uiSettingsStore;
    private readonly IAppPathService _appPathService;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly bool _isChinese;

    private AppUiSettings _settings = new();

    [ObservableProperty]
    private string _themeName = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private UiOption? _selectedThemeMode;

    [ObservableProperty]
    private UiOption? _selectedDensityMode;

    [ObservableProperty]
    private UiOption? _selectedLogLevel;

    [ObservableProperty]
    private UiOption? _selectedAutoSnapshotOption;

    [ObservableProperty]
    private UiOption? _selectedAutoPullOption;

    [ObservableProperty]
    private UiOption? _selectedRefreshManifestOption;

    [ObservableProperty]
    private bool _autoCheckUpdates;

    [ObservableProperty]
    private bool _autoInstallUpdates;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _startupInBackground;

    [ObservableProperty]
    private string _manifestSourceUrlText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UiSettingOption> _groups = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _diagnostics = [];

    [ObservableProperty]
    private IReadOnlyList<UiGameMetaRow> _gameEntries = [];

    public SettingsPageViewModel(
        ILocalizationService localizationService,
        UiSettingsStore uiSettingsStore,
        IAppPathService appPathService,
        IGameInstanceRepository gameInstanceRepository)
    {
        _localizationService = localizationService;
        _uiSettingsStore = uiSettingsStore;
        _appPathService = appPathService;
        _gameInstanceRepository = gameInstanceRepository;
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("设置", "Settings");
        PageSubtitle = Pick("管理语言、主题、同步行为与日志配置", "Manage language, theme, sync behavior, and logging");
        GroupNavTitle = Pick("设置分组", "Settings Groups");
        GroupNavSubtitle = Pick("选择分组查看对应设置", "Select a group to view its settings");
        AppearanceTitle = Pick("外观", "Appearance");
        AppearanceSubtitle = Pick("语言、主题模式与界面密度", "Language, theme mode, and interface density");
        ThemeBehaviorTitle = Pick("外观与界面", "Appearance and interface");
        ThemeBehaviorSubtitle = Pick("修改后点击「保存设置」即时生效并持久化到配置文件。", "Changes take effect when you click Save Settings.");
        BehaviorTitle = Pick("行为控制", "Behavior Controls");
        BehaviorSubtitle = Pick("控制自动拉取、下载快照和启动时清单刷新。", "Control automatic pull, download snapshots, and manifest refresh on startup.");
        LoggingTitle = Pick("日志控制", "Logging Controls");
        LoggingSubtitle = Pick("控制日志写入级别；日志文件保留最近 14 天。", "Control the log write level. Log files are retained for 14 days.");
        DiagnosticsTitle = Pick("运行时路径", "Runtime Paths");
        DiagnosticsSubtitle = Pick("当前数据库、日志目录与快照目录的实际路径。", "Actual paths for the current database, log directory, and snapshots directory.");
        LanguageLabel = Pick("界面语言", "Interface language");
        ThemeModeLabel = Pick("主题模式", "Theme mode");
        DensityLabel = Pick("界面密度", "Interface density");
        AutoSnapshotLabel = Pick("下载前自动快照", "Snapshot before download");
        RefreshManifestLabel = Pick("启动时刷新社区清单", "Refresh community manifest on startup");
        ManifestSourceUrlLabel = Pick("社区清单来源 URL", "Community manifest source URL");
        ManifestSourceUrlPlaceholder = Pick("留空则使用内置默认地址", "Leave blank to use the built-in default URL");
        LogLevelLabel = Pick("日志级别", "Log level");
        RestoreDefaultsText = Pick("恢复默认", "Restore Defaults");
        SaveSettingsText = Pick("保存设置", "Save Settings");
        OpenLogsDirText = Pick("打开日志目录", "Open Logs Directory");
        OpenDataDirText = Pick("打开数据目录", "Open Data Directory");
        GamesTitle = Pick("游戏库管理", "Game Library");
        GamesSubtitle = Pick("手动编辑各游戏的显示名称、安装目录与专属变量。", "Manually edit each game's display name, install directory, and per-game variables.");

        SupportedLanguages = localizationService.SupportedLanguages;
        ThemeOptions =
        [
            new UiOption(AppUiSettings.ThemeSystem, Pick("跟随系统", "Follow system")),
            new UiOption(AppUiSettings.ThemeDark, Pick("深色", "Dark")),
            new UiOption(AppUiSettings.ThemeLight, Pick("浅色", "Light")),
        ];
        DensityOptions =
        [
            new UiOption(AppUiSettings.DensityCompact, Pick("紧凑", "Compact")),
            new UiOption(AppUiSettings.DensityComfortable, Pick("标准", "Comfortable")),
        ];
        ToggleOptions =
        [
            new UiOption("on", Pick("启用", "Enabled")),
            new UiOption("off", Pick("停用", "Disabled")),
        ];
        LogLevelOptions =
        [
            new UiOption("Error", "Error"),
            new UiOption("Warning", "Warning"),
            new UiOption("Information", "Information"),
            new UiOption("Debug", "Debug"),
        ];

        ThemeName = string.Empty;
        Groups = [];
        Diagnostics = [];
        GameEntries = [];
        Load();
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string GroupNavTitle { get; }
    public string GroupNavSubtitle { get; }
    public string AppearanceTitle { get; }
    public string AppearanceSubtitle { get; }
    public string ThemeBehaviorTitle { get; }
    public string ThemeBehaviorSubtitle { get; }
    public string BehaviorTitle { get; }
    public string BehaviorSubtitle { get; }
    public string LoggingTitle { get; }
    public string LoggingSubtitle { get; }
    public string DiagnosticsTitle { get; }
    public string DiagnosticsSubtitle { get; }
    public string LanguageLabel { get; }
    public string ThemeModeLabel { get; }
    public string DensityLabel { get; }
    public string AutoSnapshotLabel { get; }
    public string AutoPullLabel => Pick("安全自动拉取", "Safe automatic pull");
    public string OnText => Pick("开", "On");
    public string OffText => Pick("关", "Off");
    public bool AutoPullEnabled
    {
        get => SelectedAutoPullOption?.Key == "on";
        set => SelectedAutoPullOption = ToggleOptions.First(option => option.Key == (value ? "on" : "off"));
    }
    public bool AutoSnapshotEnabled
    {
        get => SelectedAutoSnapshotOption?.Key == "on";
        set => SelectedAutoSnapshotOption = ToggleOptions.First(option => option.Key == (value ? "on" : "off"));
    }
    public bool RefreshManifestEnabled
    {
        get => SelectedRefreshManifestOption?.Key == "on";
        set => SelectedRefreshManifestOption = ToggleOptions.First(option => option.Key == (value ? "on" : "off"));
    }

    partial void OnSelectedAutoPullOptionChanged(UiOption? value) => OnPropertyChanged(nameof(AutoPullEnabled));
    partial void OnSelectedAutoSnapshotOptionChanged(UiOption? value) => OnPropertyChanged(nameof(AutoSnapshotEnabled));
    partial void OnSelectedRefreshManifestOptionChanged(UiOption? value) => OnPropertyChanged(nameof(RefreshManifestEnabled));

    public string AutoPullDescription => Pick(
        "每分钟检查远端；仅在本地与已确认版本一致时拉取，并创建快照。请先关闭游戏。",
        "Checks the target every minute and creates a snapshot before pulling changes when local files match the confirmed version. Close the game first.");
    public string AutoPullIdentityNote => Pick(
        "两台电脑需使用相同的游戏 ID、远端命名空间和内容项；不同来源的同名游戏不一定对应同一存档。首次关联、配置变更或双端改动时，请到游戏详情比较并确认基线。",
        "Both computers must use the same game ID, remote namespace, and content items; games with the same name from different sources may not share saves. On first connection, configuration changes, or changes on both sides, compare and confirm the baseline in game details.");
    public string RefreshManifestLabel { get; }
    public string ManifestSourceUrlLabel { get; }
    public string ManifestSourceUrlPlaceholder { get; }
    public string LogLevelLabel { get; }
    public string RestoreDefaultsText { get; }
    public string SaveSettingsText { get; }
    public string CancelText => Pick("取消", "Cancel");
    public string OpenLogsDirText { get; private set; } = string.Empty;
    public string OpenDataDirText { get; private set; } = string.Empty;
    public string GamesTitle { get; }
    public string GamesSubtitle { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<LanguageOption> SupportedLanguages { get; }
    public IReadOnlyList<UiOption> ThemeOptions { get; }
    public IReadOnlyList<UiOption> DensityOptions { get; }
    public IReadOnlyList<UiOption> ToggleOptions { get; }
    public IReadOnlyList<UiOption> LogLevelOptions { get; }

    public string GetLogsDirectory() => _appPathService.GetLogsDirectory();
    public string GetDataDirectory() => _appPathService.GetAppDataRoot();

    public async Task LoadGamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            var editText = Pick("编辑", "Edit");
            GameEntries = instances
                .Select(inst => new UiGameMetaRow(
                    inst.Id,
                    inst.DisplayName,
                    inst.GameId,
                    FormatSource(inst.SourceProviderId),
                    inst.InstallDirectory ?? string.Empty,
                    inst.Variables.Count,
                    editText))
                .ToArray();
            RefreshDerivedCollections();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load game instances for settings page.");
        }
    }

    public void Load()
    {
        _settings = _uiSettingsStore.Load();
        ApplySettingsToSelections(_settings);
        RefreshDerivedCollections();
    }

    public void RestoreDefaults()
    {
        _settings = new AppUiSettings();
        ApplySettingsToSelections(_settings);
        RefreshDerivedCollections();
    }

    public AppUiSettings BuildSettings()
    {
        return new AppUiSettings
        {
            LanguageTag = SelectedLanguage?.Tag ?? AppUiSettings.DefaultLanguageTag,
            ThemeMode = SelectedThemeMode?.Key ?? AppUiSettings.ThemeSystem,
            DensityMode = SelectedDensityMode?.Key ?? AppUiSettings.DensityCompact,
            LogLevel = SelectedLogLevel?.Key ?? AppUiSettings.DefaultLogLevel,
            AutoSnapshotBeforeDownload = string.Equals(SelectedAutoSnapshotOption?.Key, "on", StringComparison.OrdinalIgnoreCase),
            AutoPullEnabled = string.Equals(SelectedAutoPullOption?.Key, "on", StringComparison.OrdinalIgnoreCase),
            RefreshManifestOnStartup = string.Equals(SelectedRefreshManifestOption?.Key, "on", StringComparison.OrdinalIgnoreCase),
            AutoCheckUpdates = AutoCheckUpdates,
            AutoInstallUpdates = AutoInstallUpdates,
            CloseToTray = CloseToTray,
            StartupInBackground = StartupInBackground,
            ManifestSourceUrl = string.IsNullOrWhiteSpace(ManifestSourceUrlText) ? null : ManifestSourceUrlText.Trim(),
        };
    }

    public void SaveSettings()
    {
        var settings = BuildSettings();
        _uiSettingsStore.Save(settings);

        if (!string.Equals(_localizationService.CurrentLanguageTag, settings.LanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            _localizationService.SetLanguage(settings.LanguageTag);
        }

        _settings = settings;
        RefreshDerivedCollections();
    }

    private void ApplySettingsToSelections(AppUiSettings settings)
    {
        SelectedLanguage = SupportedLanguages.FirstOrDefault(option => option.Tag == settings.LanguageTag) ?? SupportedLanguages[0];
        SelectedThemeMode = ThemeOptions.FirstOrDefault(option => string.Equals(option.Key, settings.ThemeMode, StringComparison.OrdinalIgnoreCase)) ?? ThemeOptions[0];
        SelectedDensityMode = DensityOptions.FirstOrDefault(option => string.Equals(option.Key, settings.DensityMode, StringComparison.OrdinalIgnoreCase)) ?? DensityOptions[0];
        SelectedLogLevel = LogLevelOptions.FirstOrDefault(option => string.Equals(option.Key, settings.LogLevel, StringComparison.OrdinalIgnoreCase)) ?? LogLevelOptions[2];
        SelectedAutoSnapshotOption = ToggleOptions.First(option => option.Key == (settings.AutoSnapshotBeforeDownload ? "on" : "off"));
        SelectedAutoPullOption = ToggleOptions.First(option => option.Key == (settings.AutoPullEnabled ? "on" : "off"));
        SelectedRefreshManifestOption = ToggleOptions.First(option => option.Key == (settings.RefreshManifestOnStartup ? "on" : "off"));
        AutoCheckUpdates = settings.AutoCheckUpdates;
        AutoInstallUpdates = settings.AutoInstallUpdates;
        CloseToTray = settings.CloseToTray;
        StartupInBackground = settings.StartupInBackground;
        ManifestSourceUrlText = settings.ManifestSourceUrl ?? string.Empty;

        ThemeName = SelectedThemeMode?.Label ?? string.Empty;
    }

    private void RefreshDerivedCollections()
    {
        ThemeName = SelectedThemeMode?.Label ?? string.Empty;

        Diagnostics =
        [
            new UiPropertyRow(Pick("当前主题", "Current theme"), SelectedThemeMode?.Label ?? "--"),
            new UiPropertyRow(Pick("当前密度", "Current density"), SelectedDensityMode?.Label ?? "--"),
            new UiPropertyRow(Pick("数据目录", "Data directory"), _appPathService.GetAppDataRoot()),
            new UiPropertyRow(Pick("数据库", "Database"), _appPathService.GetDatabasePath()),
            new UiPropertyRow(Pick("日志目录", "Logs directory"), _appPathService.GetLogsDirectory()),
            new UiPropertyRow(Pick("快照目录", "Snapshots directory"), _appPathService.GetSnapshotsDirectory()),
        ];

        Groups =
        [
            new UiSettingOption(
                AppearanceTitle,
                Pick("语言、主题模式与界面密度", "Language, theme mode, and density"),
                "A",
                SelectedThemeMode?.Label ?? ThemeName,
                "synced",
                "appearance"),
            new UiSettingOption(
                Pick("启动", "Startup"), Pick("Windows 登录启动", "Start when signing in to Windows"),
                "S", "", "ready", "startup"),
            new UiSettingOption(
                Pick("关于与更新", "About and updates"), Pick("版本与 GitHub 更新", "Version and GitHub updates"),
                "U", "", "ready", "updates"),
            new UiSettingOption(
                BehaviorTitle,
                Pick("自动拉取、快照与清单刷新", "Automatic pull, snapshots, and manifest refresh"),
                "B",
                string.Equals(SelectedAutoPullOption?.Key, "on", StringComparison.OrdinalIgnoreCase) ? Pick("自动拉取开启", "Auto pull on") : Pick("自动拉取关闭", "Auto pull off"),
                string.Equals(SelectedAutoPullOption?.Key, "on", StringComparison.OrdinalIgnoreCase) ? "ready" : "disabled",
                "behavior"),
            new UiSettingOption(
                LoggingTitle,
                Pick("日志级别与日志目录路径", "Log level and log directory"),
                "L",
                SelectedLogLevel?.Label ?? AppUiSettings.DefaultLogLevel,
                "ready",
                "logging"),
        ];
    }

    private string FormatSource(string sourceProviderId) => sourceProviderId switch
    {
        "steam" => "Steam",
        "epic" => "Epic",
        "custom" => Pick("自定义", "Custom"),
        _ => sourceProviderId,
    };

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}
