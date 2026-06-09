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
        PreviewHeader = Pick("实时预览", "Realtime Preview");
        LanguageLabel = Pick("界面语言", "Interface language");
        SupportedLanguages = localizationService.SupportedLanguages;
        SelectedLanguage = SupportedLanguages.FirstOrDefault(option => option.Tag == localizationService.CurrentLanguageTag) ?? SupportedLanguages[0];

        Groups =
        [
            new(Pick("主题模式", "Theme Mode"), Pick("深色 · 浅色 · 跟随系统", "Dark · Light · System")),
            new(Pick("强调色", "Accent Color"), Pick("强调蓝搭配克制的桌面工具中性色", "Accent Blue with muted desktop utility neutrals")),
            new(Pick("布局密度", "Layout Density"), Pick("适用于密集管理视图的紧凑间距", "Compact spacing for dense management views")),
            new(Pick("图标风格", "Icon Style"), Pick("描边工具图标集", "Outlined utility icon set")),
            new(Pick("系统行为", "System Behavior"), Pick("启动检查、退出警告与历史保留", "Startup checks, exit warnings, and history retention")),
            new(Pick("控制台日志级别", "Console Log Level"), Pick("从 Error 到 Trace 的诊断面板", "Error to Trace diagnostic surface")),
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

        PreviewRows =
        [
            new("Elden Ring", "D:/Cloud/elden-ring", Pick("已同步", "Synced")),
            new("Cyberpunk 2077", "E:/Games/Saves/cyberpunk", Pick("时间戳不匹配", "Mismatched timestamp")),
            new("Hades", "D:/Cloud/hades", Pick("冲突", "Conflict")),
        ];
    }

    public string PreviewHeader { get; }
    public string LanguageLabel { get; }
    public IReadOnlyList<LanguageOption> SupportedLanguages { get; }
    public IReadOnlyList<UiSettingOption> Groups { get; }
    public IReadOnlyList<UiPropertyRow> Details { get; }
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
