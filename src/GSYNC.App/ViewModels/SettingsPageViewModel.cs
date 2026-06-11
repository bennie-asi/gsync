using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly bool _isChinese;

    [ObservableProperty]
    private string _themeName;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public SettingsPageViewModel(ILocalizationService localizationService)
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
            new(Pick("日志", "Logging"), Pick("控制台级别、文件保留与诊断输出", "Console level, file retention, and diagnostics"), "L3", Pick("已收紧", "Refined"), "pending"),
            new(Pick("同步目标", "Sync Targets"), Pick("目标可见性、默认目标与恢复入口", "Target visibility, primary target, and recovery entry points"), "T4", Pick("需关注", "Needs Review"), "conflict"),
        ];

        Details =
        [
            new(Pick("主题", "Theme"), _themeName),
            new(Pick("语言", "Language"), SelectedLanguage.DisplayName),
            new(Pick("强调色", "Accent"), "#5B8CFF"),
            new(Pick("密度", "Density"), Pick("紧凑", "Compact")),
            new(Pick("启动时检查连通性", "Run startup connectivity check"), Pick("已启用", "Enabled")),
            new(Pick("有未同步变更时退出前警告", "Warn before exit with unsynced changes"), Pick("已启用", "Enabled")),
            new(Pick("保留历史快照", "Keep history snapshots"), Pick("90 天", "90 days")),
            new(Pick("控制台日志级别", "Console Log Level"), Pick("信息", "Information")),
        ];

        Diagnostics =
        [
            new(Pick("当前设计系统", "Current design system"), Pick("GSYNC Desktop Dark", "GSYNC Desktop Dark")),
            new(Pick("导航 rail", "Navigation rail"), Pick("固定紧凑栏位", "Fixed compact rail")),
            new(Pick("运行状态栏", "Runtime status bar"), Pick("已在 Library/管理页复用", "Reused across Library and management pages")),
            new(Pick("故障降级", "Failure degradation"), Pick("页面初始化异常时保留主壳层", "Retains shell on page initialization failure")),
        ];

        PreviewRows =
        [
            new("Elden Ring", "D:/Cloud/elden-ring", Pick("已同步", "Synced"), "synced", Pick("主目标已在线，最近一次写入通过验证。", "Primary target is online and the latest write passed verification."), Pick("打开", "Open"), Pick("文件夹", "Folder")),
            new("Cyberpunk 2077", "E:/Games/Saves/cyberpunk", Pick("时间戳不匹配", "Mismatched timestamp"), "pending", Pick("需要复查时钟来源与最后写入顺序。", "Review the clock source and the latest write ordering."), Pick("查看", "Open"), Pick("定位", "Folder")),
            new("Hades", "D:/Cloud/hades", Pick("冲突", "Conflict"), "conflict", Pick("建议先进入冲突处理，再回到设置确认默认目标。", "Resolve the conflict first, then revisit Settings to confirm the default target."), Pick("打开", "Open"), Pick("日志", "Logs")),
        ];
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
