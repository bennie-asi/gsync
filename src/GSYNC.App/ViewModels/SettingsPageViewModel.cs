using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Data;

namespace GSYNC.App.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly bool _isChinese;

    [ObservableProperty]
    private string _themeName;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public SettingsPageViewModel(
        ILocalizationService localizationService,
        IAppPathService appPathService,
        SyncTargetStore syncTargetStore)
    {
        _localizationService = localizationService;
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _themeName = Pick("深色", "Dark");
        PageTitle = Pick("设置", "Settings");
        PageSubtitle = Pick("设置分组、中心编辑区与右侧辅助检查区保持为精炼后的桌面工具布局。", "Settings groups, center editing, and the right-hand inspection area stay in the refined desktop utility layout.");
        GroupNavTitle = Pick("设置分组", "Settings Groups");
        GroupNavSubtitle = Pick("左侧保持紧凑导航语义，而不是数据表格式分栏。", "The left side stays a compact group navigator instead of a data-grid style list.");
        GroupNavFooter = Pick("外观预览、运行检查与目标入口保持同页可见。", "Preview, runtime checks, and target entry points remain visible on the same screen.");
        AppearanceTitle = Pick("外观", "Appearance");
        AppearanceSubtitle = Pick("主题模式、强调色、布局密度与系统行为集中在主编辑区。", "Theme mode, accent, density, and system behavior stay grouped in the main editor.");
        PreviewHeader = Pick("辅助检查", "Assist Panel");
        PreviewSubtitle = Pick("右侧辅助区用于检查目标健康状态、可见性与当前设计令牌。", "The right-side assist area verifies target health, visibility, and current design tokens.");
        ThemeBehaviorTitle = Pick("主题与系统行为", "Theme and system behavior");
        ThemeBehaviorSubtitle = Pick("优先展示语言、主题、强调色和紧凑度，再延伸到系统行为。", "Lead with language, theme, accent, and density before the broader system behavior controls.");
        DiagnosticsTitle = Pick("运行时检查", "Runtime checks");
        DiagnosticsSubtitle = Pick("保留壳层复用、故障降级与主题令牌检查结果。", "Keep shell reuse, failure degradation, and theme-token checks visible.");
        PreviewSectionTitle = Pick("目标与预览", "Targets and Preview");
        PreviewSectionSubtitle = Pick("在不离开设置页的前提下查看当前目标状态与打开入口。", "Review target status and open-entry actions without leaving Settings.");
        LanguageLabel = Pick("界面语言", "Interface language");
        SupportedLanguages = localizationService.SupportedLanguages;
        SelectedLanguage = SupportedLanguages.FirstOrDefault(option => option.Tag == localizationService.CurrentLanguageTag) ?? SupportedLanguages[0];

        Groups =
        [
            new(Pick("外观", "Appearance"), Pick("主题模式、强调色与布局密度", "Theme mode, accent, and layout density"), "A1", Pick("当前", "Current"), "synced"),
            new(Pick("行为", "Behavior"), Pick("启动检查、退出警告与保留策略", "Startup checks, exit warnings, and retention policy"), "B2", Pick("已启用", "Enabled"), "ready"),
            new(Pick("日志", "Logging"), Pick("控制台级别、文件保留与诊断输出", "Console level, file retention, and diagnostics"), "L3", Pick("已启用", "Enabled"), "ready"),
            new(Pick("同步目标", "Sync Targets"), Pick("目标可见性、默认目标与恢复入口", "Target visibility, primary target, and recovery entry points"), "T4", Pick("已配置", "Configured"), "ready"),
        ];

        Details =
        [
            new(Pick("主题", "Theme"), _themeName),
            new(Pick("语言", "Language"), SelectedLanguage.DisplayName),
            new(Pick("密度", "Density"), Pick("紧凑", "Compact")),
            new(Pick("下载前自动快照", "Snapshot before download"), Pick("已启用", "Enabled")),
            new(Pick("同步队列后台处理", "Background sync queue"), Pick("已启用", "Enabled")),
            new(Pick("冲突策略", "Conflict policy"), Pick("覆盖前提示", "Prompt before overwrite")),
        ];

        Diagnostics =
        [
            new(Pick("当前设计系统", "Current design system"), "GSYNC Desktop Dark"),
            new(Pick("数据目录", "Data directory"), appPathService.GetAppDataRoot()),
            new(Pick("数据库", "Database"), appPathService.GetDatabasePath()),
            new(Pick("日志目录", "Logs directory"), appPathService.GetLogsDirectory()),
            new(Pick("快照目录", "Snapshots directory"), appPathService.GetSnapshotsDirectory()),
        ];

        var defaultTargetId = syncTargetStore.GetDefault()?.Id;
        PreviewRows = syncTargetStore.List()
            .Select(target => new UiPreviewRow(
                target.Name,
                target.ProviderId == "webdav"
                    ? target.Settings.GetValueOrDefault("baseUrl", "--")
                    : target.Settings.GetValueOrDefault("rootPath", "--"),
                target.Id == defaultTargetId ? Pick("默认目标", "Default") : Pick("已配置", "Configured"),
                target.Id == defaultTargetId ? "synced" : "ready",
                target.ProviderId == "webdav"
                    ? Pick("WebDAV 远端目标，连接状态可在同步目标页测试。", "WebDAV remote target. Test connectivity from the Sync Targets page.")
                    : Pick("本地文件夹目标，可直接在资源管理器中查看。", "Local folder target, browsable in Explorer."),
                Pick("打开", "Open"),
                Pick("文件夹", "Folder")))
            .ToArray();
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
    public string DiagnosticsTitle { get; }
    public string DiagnosticsSubtitle { get; }
    public string PreviewSectionTitle { get; }
    public string PreviewSectionSubtitle { get; }
    public string LanguageLabel { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<LanguageOption> SupportedLanguages { get; }
    public IReadOnlyList<UiSettingOption> Groups { get; }
    public IReadOnlyList<UiPropertyRow> Details { get; }
    public IReadOnlyList<UiPropertyRow> Diagnostics { get; }
    public IReadOnlyList<UiPreviewRow> PreviewRows { get; }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null)
        {
            return;
        }

        _localizationService.SetLanguage(value.Tag);
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}
