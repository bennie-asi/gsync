using Windows.Globalization;

namespace GSYNC.App.Infrastructure.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly UiSettingsStore _settingsStore;
    private readonly AppUiSettings _settings;
    private readonly Dictionary<string, string> _zhCn = new(StringComparer.Ordinal)
    {
        ["App.WindowTitle"] = "GSYNC",
        ["MainWindow.Title"] = "GSYNC",
        ["MainWindow.Nav.Library"] = "游戏库",
        ["MainWindow.Nav.Targets"] = "同步目标",
        ["MainWindow.Nav.Variables"] = "变量",
        ["MainWindow.Nav.History"] = "历史",
        ["MainWindow.Nav.Settings"] = "设置",
        ["MainWindow.Subtitle.Library"] = "游戏库总览与快速同步操作",
        ["MainWindow.Subtitle.Wizard"] = "引导式六步添加游戏流程",
        ["MainWindow.Subtitle.Conflict"] = "在选择安全解决方案前检查本地与远端变更",
        ["MainWindow.Subtitle.Targets"] = "管理本地与远端同步目标绑定",
        ["MainWindow.Subtitle.Variables"] = "检查变量与模板解析结果",
        ["MainWindow.Subtitle.History"] = "查看同步历史与恢复点",
        ["MainWindow.Subtitle.Settings"] = "外观、行为与保留策略设置",
        ["MainWindow.Subtitle.Default"] = "游戏同步桌面工具",
        ["MainWindow.Status.Ready"] = "就绪 · 当前没有运行中的同步任务",
        ["MainWindow.Status.Wizard"] = "向导已就绪 · 尚未创建配置",
        ["MainWindow.Status.Conflict"] = "冲突待处理 · 请选择安全的解决路径",
        ["MainWindow.Status.History"] = "历史已就绪 · 当前没有恢复操作",
        ["MainWindow.Status.Targets"] = "目标已就绪 · 最近一次连通性检查通过",
        ["MainWindow.Status.Variables"] = "变量已就绪 · 模板解析正常",
        ["StatusBar.WebDavStatus"] = "WebDAV：Online",
        ["StatusBar.LocalStatus"] = "Local：Ready",
        ["StatusBar.TargetName"] = "WebDAV-Main",
        ["StatusBar.SyncStatus"] = "同步：就绪",
        ["StatusBar.Connection"] = "WebDAV 桌面主目标",
        ["StatusBar.Storage"] = "已同步 12.4 GB 占用",
        ["StatusBar.Log"] = "日志",
        ["StatusBar.Net"] = "网络",
        ["StatusBar.Idle"] = "空闲",
        ["Settings.Title"] = "设置",
        ["Settings.Subtitle"] = "外观与系统分组始终保持在紧凑桌面布局中可见。",
        ["Settings.AppearanceTitle"] = "外观",
        ["Settings.AppearanceSubtitle"] = "主题模式、强调色、密度、图标风格与系统行为保持为紧凑工具卡片分组。",
        ["Settings.ThemeBehaviorTitle"] = "主题与系统行为",
        ["Settings.Button.RestoreDefaults"] = "恢复默认",
        ["Settings.Button.ApplyTheme"] = "应用主题",
        ["Settings.Button.SaveSettings"] = "保存设置",
        ["Settings.Button.AddTarget"] = "添加新目标",
    };

    private readonly Dictionary<string, string> _enUs = new(StringComparer.Ordinal)
    {
        ["App.WindowTitle"] = "GSYNC",
        ["MainWindow.Title"] = "GSYNC",
        ["MainWindow.Nav.Library"] = "Library",
        ["MainWindow.Nav.Targets"] = "Targets",
        ["MainWindow.Nav.Variables"] = "Variables",
        ["MainWindow.Nav.History"] = "History",
        ["MainWindow.Nav.Settings"] = "Settings",
        ["MainWindow.Subtitle.Library"] = "Library overview and quick sync actions",
        ["MainWindow.Subtitle.Wizard"] = "Guided six-step game onboarding flow",
        ["MainWindow.Subtitle.Conflict"] = "Review local and remote changes before choosing a safe resolution",
        ["MainWindow.Subtitle.Targets"] = "Manage local and remote sync target bindings",
        ["MainWindow.Subtitle.Variables"] = "Review variables and template resolution",
        ["MainWindow.Subtitle.History"] = "Inspect sync history and restore points",
        ["MainWindow.Subtitle.Settings"] = "Appearance, behavior, and retention settings",
        ["MainWindow.Subtitle.Default"] = "Game synchronization desktop utility",
        ["MainWindow.Status.Ready"] = "Ready · no sync jobs running",
        ["MainWindow.Status.Wizard"] = "Wizard ready · no profile created yet",
        ["MainWindow.Status.Conflict"] = "Conflict review · choose a safe resolution path",
        ["MainWindow.Status.History"] = "History ready · no restore operations running",
        ["MainWindow.Status.Targets"] = "Targets ready · last connectivity check passed",
        ["MainWindow.Status.Variables"] = "Variables ready · template resolution healthy",
        ["StatusBar.WebDavStatus"] = "WebDAV: Online",
        ["StatusBar.LocalStatus"] = "Local: Ready",
        ["StatusBar.TargetName"] = "WebDAV-Main",
        ["StatusBar.SyncStatus"] = "Sync: Ready",
        ["StatusBar.Connection"] = "WebDAV Desktop-Main",
        ["StatusBar.Storage"] = "12.4 GB synced footprint",
        ["StatusBar.Log"] = "Log",
        ["StatusBar.Net"] = "Net",
        ["StatusBar.Idle"] = "Idle",
        ["Settings.Title"] = "Settings",
        ["Settings.Subtitle"] = "Appearance and system groups remain visible from a compact left navigation rail.",
        ["Settings.AppearanceTitle"] = "Appearance",
        ["Settings.AppearanceSubtitle"] = "Theme mode, accent color, density, icon style, and system behavior stay grouped as compact utility cards.",
        ["Settings.ThemeBehaviorTitle"] = "Theme and system behavior",
        ["Settings.Button.RestoreDefaults"] = "Restore Defaults",
        ["Settings.Button.ApplyTheme"] = "Apply Theme",
        ["Settings.Button.SaveSettings"] = "Save Settings",
        ["Settings.Button.AddTarget"] = "Add New Target",
    };

    public LocalizationService(UiSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settings = settingsStore.Load();
        CurrentLanguageTag = NormalizeLanguageTag(_settings.LanguageTag);
    }

    public string CurrentLanguageTag { get; private set; }

    public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English"),
    ];

    public event EventHandler? LanguageChanged;

    public string GetString(string key)
    {
        var dictionary = CurrentLanguageTag == "en-US" ? _enUs : _zhCn;
        return dictionary.TryGetValue(key, out var value) ? value : key;
    }

    public bool SetLanguage(string languageTag)
    {
        var normalizedLanguageTag = NormalizeLanguageTag(languageTag);
        if (string.Equals(CurrentLanguageTag, normalizedLanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        CurrentLanguageTag = normalizedLanguageTag;
        _settings.LanguageTag = normalizedLanguageTag;
        _settingsStore.Save(_settings);
        ApplicationLanguages.PrimaryLanguageOverride = normalizedLanguageTag;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static string NormalizeLanguageTag(string? languageTag)
    {
        return languageTag?.Trim() switch
        {
            "en" or "en-US" => "en-US",
            _ => AppUiSettings.DefaultLanguageTag,
        };
    }
}
