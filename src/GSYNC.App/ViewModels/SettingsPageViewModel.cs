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
    private readonly SyncTargetStore _syncTargetStore;
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
    private UiOption? _selectedRefreshManifestOption;

    [ObservableProperty]
    private string _manifestSourceUrlText = string.Empty;

    [ObservableProperty]
    private UiTargetOption? _selectedDefaultTarget;

    [ObservableProperty]
    private IReadOnlyList<UiSettingOption> _groups = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _diagnostics = [];

    [ObservableProperty]
    private IReadOnlyList<UiPreviewRow> _previewRows = [];

    [ObservableProperty]
    private IReadOnlyList<UiTargetOption> _availableTargets = [];

    [ObservableProperty]
    private IReadOnlyList<UiGameMetaRow> _gameEntries = [];

    public SettingsPageViewModel(
        ILocalizationService localizationService,
        UiSettingsStore uiSettingsStore,
        IAppPathService appPathService,
        SyncTargetStore syncTargetStore,
        IGameInstanceRepository gameInstanceRepository)
    {
        _localizationService = localizationService;
        _uiSettingsStore = uiSettingsStore;
        _appPathService = appPathService;
        _syncTargetStore = syncTargetStore;
        _gameInstanceRepository = gameInstanceRepository;
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("设置", "Settings");
        PageSubtitle = Pick("管理语言、主题、同步行为与日志配置", "Manage language, theme, sync behavior, and logging");
        GroupNavTitle = Pick("设置分组", "Settings Groups");
        GroupNavSubtitle = Pick("选择分组查看对应设置", "Select a group to view its settings");
        GroupNavFooter = Pick("同步目标的完整管理在「同步目标」页面。", "Full sync target management is available on the Sync Targets page.");
        AppearanceTitle = Pick("外观", "Appearance");
        AppearanceSubtitle = Pick("语言、主题模式与界面密度", "Language, theme mode, and interface density");
        PreviewHeader = Pick("同步目标概览", "Sync Targets");
        PreviewSubtitle = Pick("当前已配置的同步目标一览，点击「管理」跳转完整配置页面。", "Overview of configured sync targets. Click Manage to open the full Sync Targets page.");
        ThemeBehaviorTitle = Pick("外观与界面", "Appearance and interface");
        ThemeBehaviorSubtitle = Pick("修改后点击「保存设置」即时生效并持久化到配置文件。", "Changes take effect when you click Save Settings.");
        BehaviorTitle = Pick("行为控制", "Behavior Controls");
        BehaviorSubtitle = Pick("控制下载前自动快照、启动时刷新清单，以及默认同步目标。", "Control pre-download snapshot, manifest refresh on startup, and the default sync target.");
        LoggingTitle = Pick("日志控制", "Logging Controls");
        LoggingSubtitle = Pick("控制日志写入级别；日志文件保留最近 14 天。", "Control the log write level. Log files are retained for 14 days.");
        DiagnosticsTitle = Pick("运行时路径", "Runtime Paths");
        DiagnosticsSubtitle = Pick("当前数据库、日志目录与快照目录的实际路径。", "Actual paths for the current database, log directory, and snapshots directory.");
        PreviewSectionTitle = Pick("已配置目标", "Configured Targets");
        PreviewSectionSubtitle = Pick("完整编辑（添加、删除、测试连通性）在「同步目标」页面进行。", "Full editing (add, remove, test connectivity) is available on the Sync Targets page.");
        LanguageLabel = Pick("界面语言", "Interface language");
        ThemeModeLabel = Pick("主题模式", "Theme mode");
        DensityLabel = Pick("界面密度", "Interface density");
        DefaultTargetLabel = Pick("默认同步目标", "Default sync target");
        AutoSnapshotLabel = Pick("下载前自动快照", "Snapshot before download");
        RefreshManifestLabel = Pick("启动时刷新社区清单", "Refresh community manifest on startup");
        ManifestSourceUrlLabel = Pick("社区清单来源 URL", "Community manifest source URL");
        ManifestSourceUrlPlaceholder = Pick("留空则使用内置默认地址", "Leave blank to use the built-in default URL");
        LogLevelLabel = Pick("日志级别", "Log level");
        RestoreDefaultsText = Pick("恢复默认", "Restore Defaults");
        SaveSettingsText = Pick("保存设置", "Save Settings");
        AddNewTargetText = Pick("打开同步目标页", "Open Sync Targets");
        OpenLogsDirText = Pick("打开日志目录", "Open Logs Directory");
        OpenDataDirText = Pick("打开数据目录", "Open Data Directory");
        GamesTitle = Pick("游戏库管理", "Game Library");
        GamesSubtitle = Pick("手动编辑各游戏的显示名称、安装目录与专属变量。", "Manually edit each game's display name, install directory, and per-game variables.");

        SupportedLanguages = localizationService.SupportedLanguages;
        ThemeOptions =
        [
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
        PreviewRows = [];
        AvailableTargets = [];
        GameEntries = [];
        Load();
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string GroupNavTitle { get; }
    public string GroupNavSubtitle { get; }
    public string GroupNavFooter { get; }
    public string AppearanceTitle { get; }
    public string AppearanceSubtitle { get; }
    public string PreviewHeader { get; }
    public string PreviewSubtitle { get; }
    public string ThemeBehaviorTitle { get; }
    public string ThemeBehaviorSubtitle { get; }
    public string BehaviorTitle { get; }
    public string BehaviorSubtitle { get; }
    public string LoggingTitle { get; }
    public string LoggingSubtitle { get; }
    public string DiagnosticsTitle { get; }
    public string DiagnosticsSubtitle { get; }
    public string PreviewSectionTitle { get; }
    public string PreviewSectionSubtitle { get; }
    public string LanguageLabel { get; }
    public string ThemeModeLabel { get; }
    public string DensityLabel { get; }
    public string DefaultTargetLabel { get; }
    public string AutoSnapshotLabel { get; }
    public string RefreshManifestLabel { get; }
    public string ManifestSourceUrlLabel { get; }
    public string ManifestSourceUrlPlaceholder { get; }
    public string LogLevelLabel { get; }
    public string RestoreDefaultsText { get; }
    public string SaveSettingsText { get; }
    public string AddNewTargetText { get; }
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

    public async Task<GameInstance?> GetGameInstanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _gameInstanceRepository.GetAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load game instance {Id}.", id);
            return null;
        }
    }

    public async Task UpdateGameInstanceAsync(
        Guid id,
        string displayName,
        string? installDirectory,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var existing = await _gameInstanceRepository.GetAsync(id, cancellationToken);
        if (existing is null) return;

        var updated = new GameInstance
        {
            Id = existing.Id,
            GameId = existing.GameId,
            DisplayName = displayName.Trim(),
            SourceProviderId = existing.SourceProviderId,
            InstallDirectory = string.IsNullOrWhiteSpace(installDirectory) ? null : installDirectory.Trim(),
            PlatformInstanceId = existing.PlatformInstanceId,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Variables = variables,
        };

        await _gameInstanceRepository.UpsertAsync(updated, cancellationToken);
        await LoadGamesAsync(cancellationToken);
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
            ThemeMode = SelectedThemeMode?.Key ?? AppUiSettings.ThemeDark,
            DensityMode = SelectedDensityMode?.Key ?? AppUiSettings.DensityCompact,
            LogLevel = SelectedLogLevel?.Key ?? AppUiSettings.DefaultLogLevel,
            AutoSnapshotBeforeDownload = string.Equals(SelectedAutoSnapshotOption?.Key, "on", StringComparison.OrdinalIgnoreCase),
            RefreshManifestOnStartup = string.Equals(SelectedRefreshManifestOption?.Key, "on", StringComparison.OrdinalIgnoreCase),
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

        if (SelectedDefaultTarget is { } target)
        {
            _syncTargetStore.SetDefault(target.Id);
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
        SelectedRefreshManifestOption = ToggleOptions.First(option => option.Key == (settings.RefreshManifestOnStartup ? "on" : "off"));
        ManifestSourceUrlText = settings.ManifestSourceUrl ?? string.Empty;

        RefreshTargetOptions();
        var defaultTargetId = _syncTargetStore.GetDefault()?.Id;
        SelectedDefaultTarget = AvailableTargets.FirstOrDefault(option => option.Id == defaultTargetId) ?? AvailableTargets.FirstOrDefault();
        ThemeName = SelectedThemeMode?.Label ?? string.Empty;
    }

    private void RefreshTargetOptions()
    {
        AvailableTargets = _syncTargetStore.List()
            .Select(target => new UiTargetOption(target.Id, $"{target.Name} · {DescribeProvider(target.ProviderId)}"))
            .ToArray();
    }

    private void RefreshDerivedCollections()
    {
        RefreshTargetOptions();
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

        var defaultTargetId = SelectedDefaultTarget?.Id ?? _syncTargetStore.GetDefault()?.Id;
        var targets = _syncTargetStore.List();
        PreviewRows = targets
            .Select(target => new UiPreviewRow(
                target.Name,
                target.ProviderId == "webdav"
                    ? target.Settings.GetValueOrDefault("baseUrl", "--")
                    : target.Settings.GetValueOrDefault("rootPath", "--"),
                target.Id == defaultTargetId ? Pick("默认目标", "Default") : Pick("已配置", "Configured"),
                target.Id == defaultTargetId ? "synced" : "ready",
                target.ProviderId == "webdav"
                    ? Pick("WebDAV 远端目标；如需编辑认证或测试连通性，请前往同步目标页。", "WebDAV remote target; use Sync Targets to edit credentials or test connectivity.")
                    : Pick("本地文件夹目标；可直接打开目录，完整编辑仍在同步目标页。", "Local-folder target; open the directory directly or use Sync Targets for full editing."),
                Pick("打开", "Open"),
                Pick("管理", "Manage")))
            .ToArray();

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
                BehaviorTitle,
                Pick("快照、清单刷新与默认目标", "Snapshot, manifest refresh, and default target"),
                "B",
                SelectedAutoSnapshotOption?.Label ?? ToggleOptions[0].Label,
                string.Equals(SelectedAutoSnapshotOption?.Key, "on", StringComparison.OrdinalIgnoreCase) ? "ready" : "disabled",
                "behavior"),
            new UiSettingOption(
                LoggingTitle,
                Pick("日志级别与日志目录路径", "Log level and log directory"),
                "L",
                SelectedLogLevel?.Label ?? AppUiSettings.DefaultLogLevel,
                "ready",
                "logging"),
            new UiSettingOption(
                Pick("同步目标", "Sync Targets"),
                Pick("查看默认目标与已配置目标列表", "Review the default target and all configured targets"),
                "T",
                Pick($"{targets.Count} 个目标", $"{targets.Count} {(targets.Count == 1 ? "target" : "targets")}"),
                targets.Count > 0 ? "ready" : "disabled",
                "targets"),
            new UiSettingOption(
                GamesTitle,
                Pick("手动编辑游戏的名称、路径与变量", "Manually edit game names, paths, and variables"),
                "G",
                Pick($"{GameEntries.Count} 个游戏", $"{GameEntries.Count} game{(GameEntries.Count == 1 ? "" : "s")}"),
                GameEntries.Count > 0 ? "ready" : "disabled",
                "games"),
        ];
    }

    private string FormatSource(string sourceProviderId) => sourceProviderId switch
    {
        "steam" => "Steam",
        "epic" => "Epic",
        "custom" => Pick("自定义", "Custom"),
        _ => sourceProviderId,
    };

    private string DescribeProvider(string providerId)
    {
        return providerId.Equals("webdav", StringComparison.OrdinalIgnoreCase)
            ? "WebDAV"
            : Pick("本地文件夹", "Local Folder");
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}
