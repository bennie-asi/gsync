using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class VariablesPageViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private bool _showParseError;

    [ObservableProperty]
    private string _selectedScopeFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public VariablesPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("变量", "Variables");
        PageSubtitle = Pick("精炼后的变量工作区，集中提供路径模板测试与显式覆盖优先级。", "Static refined variable workspace with path template testing and explicit override precedence.");
        SearchPlaceholder = Pick("搜索变量", "Search variables");
        ScopeFilterPlaceholder = Pick("全部范围", "All scopes");
        AddVariableText = Pick("添加变量", "Add Variable");
        TestTemplateText = Pick("测试模板", "Test Template");
        RefreshText = Pick("刷新", "Refresh");
        VariablesSectionTitle = Pick("变量", "Variables");

        SelectedVariableName = "%CUSTOM_SAVE_ROOT%";
        VariableScopeText = Pick("用户定义", "User Defined");
        LastModifiedText = Pick("最近修改 2 小时前", "Last modified 2h ago");
        VariableNameLabel = Pick("变量名", "Variable Name");
        ResolvedPathLabel = Pick("基础解析路径", "Base Resolved Path");
        DescriptionLabel = Pick("描述", "Description");
        DescriptionValue = Pick("用户定义的高优先级便携存档根目录。", "User-defined portable save root for high-priority games.");

        PathTemplateTesterText = Pick("路径模板测试器", "Path Template Tester");
        InputTemplateText = Pick("输入模板字符串", "Input Template String");
        ResolvedOutputText = Pick("解析输出", "Resolved Output");
        OverridePrecedenceTitle = Pick("覆盖优先级", "Override Precedence");
        SaveChangesText = Pick("保存更改", "Save Changes");
        ResetText = Pick("重置覆盖", "Reset Override");
        DeleteVariableText = Pick("删除变量", "Delete Variable");
        ParseErrorTitle = Pick("解析错误", "Parse Error");
        ParseErrorMessage = Pick("检测到未闭合的模板标签，请检查变量或模板字符串语法。", "Unclosed template tag detected. Check the variable or template syntax.");
        ParseErrorCode = "TEMPLATE_UNCLOSED_TAG";
        ErrorStatusText = Pick("未解析", "UNRESOLVED");
        ErrorBadgeText = Pick("失败", "FAILED");

        Filters =
        [
            Pick("全部范围", "All scopes"),
            Pick("系统", "System"),
            Pick("来源", "Source"),
            Pick("游戏", "Game"),
            Pick("自定义", "Custom"),
        ];
        _selectedScopeFilter = Filters[0];
        _showParseError = true;

        Variables =
        [
            new("%APPDATA%", "C:\\Users\\Admin\\AppData\\Roaming", "SYS", Pick("系统", "System"), "ready", false),
            new("%LOCALAPPDATA%", "C:\\Users\\Admin\\AppData\\Local", "SYS", Pick("系统", "System"), "ready", false),
            new("%STEAM_ROOT%", "D:\\SteamLibrary", "SRC", Pick("来源", "Source"), "remote newer", false),
            new("%GOG_ROOT%", "E:\\GOG Games", "SRC", Pick("来源", "Source"), "remote newer", false),
            new("%SAVE_ID%", "elden_ring_01", "GAME", Pick("游戏", "Game"), "synced", false),
            new("%CUSTOM_SAVE_ROOT%", "D:\\CloudSync\\GameData", "USR", Pick("自定义", "Custom"), "conflict", true),
            new("%BACKUP_PATH%", "D:\\CloudSync\\Backup", "USR", Pick("自定义", "Custom"), "ready", false),
        ];

        VariableProperties =
        [
            new(Pick("变量名", "Variable Name"), "%CUSTOM_SAVE_ROOT%"),
            new(Pick("解析路径", "Resolved Path"), "D:\\CloudSync\\GameData"),
            new(Pick("描述", "Description"), DescriptionValue),
            new(Pick("最近修改", "Last Modified"), Pick("2 小时前", "2h ago")),
        ];

        TemplateInput = "%CUSTOM_SAVE_ROOT%\\save01.dat{{";
        TemplateOutput = "UNRESOLVED";
        OverridePrecedence = Pick("自定义 > 游戏 > 来源 > 系统", "Custom > Game > Source > System");
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string ScopeFilterPlaceholder { get; }
    public string AddVariableText { get; }
    public string TestTemplateText { get; }
    public string RefreshText { get; }
    public string VariablesSectionTitle { get; }
    public string SelectedVariableName { get; }
    public string VariableScopeText { get; }
    public string LastModifiedText { get; }
    public string VariableNameLabel { get; }
    public string ResolvedPathLabel { get; }
    public string DescriptionLabel { get; }
    public string DescriptionValue { get; }
    public string PathTemplateTesterText { get; }
    public string InputTemplateText { get; }
    public string ResolvedOutputText { get; }
    public string OverridePrecedenceTitle { get; }
    public string SaveChangesText { get; }
    public string ResetText { get; }
    public string DeleteVariableText { get; }
    public string ParseErrorTitle { get; }
    public string ParseErrorMessage { get; }
    public string ParseErrorCode { get; }
    public string ErrorStatusText { get; }
    public string ErrorBadgeText { get; }
    public bool IsChinese => _isChinese;
    public string TemplateInput { get; }
    public string TemplateOutput { get; }
    public string OverridePrecedence { get; }
    public IReadOnlyList<VariableRow> Variables { get; }
    public IReadOnlyList<UiPropertyRow> VariableProperties { get; }
    public IReadOnlyList<string> Filters { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record VariableRow(
    string Name,
    string Value,
    string TypeCode,
    string ScopeLabel,
    string StatusVariant,
    bool IsSelected);
