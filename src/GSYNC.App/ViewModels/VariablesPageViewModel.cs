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

    public VariablesPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("变量", "Variables");
        PageSubtitle = Pick("精炼后的变量工作区，集中提供路径模板测试与显式覆盖优先级。", "Static refined variable workspace with path template testing and explicit override precedence.");
        SearchPlaceholder = Pick("搜索变量", "Search variables");
        FilterPlaceholder = Pick("范围", "Scope");
        AddVariableText = Pick("添加变量", "Add Variable");
        TestTemplateText = Pick("测试模板", "Test Template");
        RefreshText = Pick("刷新", "Refresh");
        TableTitle = Pick("已解析变量", "Resolved variables");
        TableSubtitle = Pick("在测试路径替换时，名称、解析值与类型始终保持可见。", "Name, resolved value, and type remain visible while testing path substitutions.");
        TableFooterText = Pick("覆盖优先级始终明确：custom > game > source > system。", "Override precedence remains explicit: custom > game > source > system.");
        PathTemplateTesterText = Pick("路径模板测试器", "Path Template Tester");
        InputTemplateText = Pick("输入模板字符串：", "Input Template String:");
        ResolvedOutputText = Pick("解析输出：", "Resolved Output:");
        OverridePrecedenceTitle = Pick("覆盖优先级", "Override Precedence");
        SaveChangesText = Pick("保存更改", "Save Changes");
        ResetText = Pick("重置", "Reset");

        Filters = [Pick("全部范围", "All scopes"), Pick("系统", "System"), Pick("来源", "Source"), Pick("游戏", "Game"), Pick("自定义", "Custom")];
        _selectedScopeFilter = Filters[0];

        SelectedVariableName = "%CUSTOM_SAVE_ROOT%";
        TemplateInput = "%CUSTOM_SAVE_ROOT%/save01.dat";
        TemplateOutput = "D:\\CloudSync\\GameData\\save01.dat";
        OverridePrecedence = Pick("自定义 > 游戏 > 来源 > 系统", "Custom > Game > Source > System");

        Variables =
        [
            new("%APPDATA%", "C:\\Users\\Admin\\AppData\\Roaming", Pick("系统", "System")),
            new("%USERPROFILE%", "C:\\Users\\Admin", Pick("系统", "System")),
            new("%DOCUMENTS%", "C:\\Users\\Admin\\Documents", Pick("系统", "System")),
            new("%STEAM_ID%", "76561198000000000", Pick("来源", "Source")),
            new("%EPIC_USER%", "ec_9921_x2", Pick("来源", "Source")),
            new("%GAME_ID%", "elden_ring_01", Pick("游戏", "Game")),
            new("%SAVE_DIR%", "E:\\Games\\Saves", Pick("游戏", "Game")),
            new("%CUSTOM_SAVE_ROOT%", "D:\\CloudSync\\GameData", Pick("自定义", "Custom")),
        ];

        VariableProperties =
        [
            new(Pick("变量名", "Variable Name"), "%CUSTOM_SAVE_ROOT%"),
            new(Pick("解析路径", "Resolved Path"), "D:\\CloudSync\\GameData"),
            new(Pick("描述", "Description"), Pick("用户定义的高优先级游戏便携存档根目录", "User-defined portable save root for high-priority games")),
            new(Pick("最近修改", "Last Modified"), Pick("2 小时前", "2h ago")),
        ];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string FilterPlaceholder { get; }
    public string AddVariableText { get; }
    public string TestTemplateText { get; }
    public string RefreshText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string PathTemplateTesterText { get; }
    public string InputTemplateText { get; }
    public string ResolvedOutputText { get; }
    public string OverridePrecedenceTitle { get; }
    public string SaveChangesText { get; }
    public string ResetText { get; }
    public bool IsChinese => _isChinese;
    public string SelectedVariableName { get; }
    public string TemplateInput { get; }
    public string TemplateOutput { get; }
    public string OverridePrecedence { get; }
    public IReadOnlyList<VariableRow> Variables { get; }
    public IReadOnlyList<UiPropertyRow> VariableProperties { get; }
    public IReadOnlyList<string> Filters { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record VariableRow(string Name, string Value, string Type);
