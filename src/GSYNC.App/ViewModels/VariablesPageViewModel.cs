using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class VariablesPageViewModel : ObservableObject
{
    private static readonly Regex UnresolvedVariablePattern = new("%([A-Z0-9_]+)%", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly bool _isChinese;
    private readonly SystemVariableProvider _systemVariableProvider;
    private readonly PathResolver _pathResolver;
    private readonly UserVariablesStore _userVariablesStore;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;

    private IReadOnlyList<VariableRow> _allVariables = [];

    [ObservableProperty]
    private bool _showParseError;

    [ObservableProperty]
    private string _selectedScopeFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<VariableRow> _variables = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _variableProperties = [];

    [ObservableProperty]
    private string _selectedVariableName = string.Empty;

    [ObservableProperty]
    private string _variableScopeText = string.Empty;

    [ObservableProperty]
    private string _lastModifiedText = string.Empty;

    [ObservableProperty]
    private string _templateInput = string.Empty;

    [ObservableProperty]
    private string _templateOutput = string.Empty;

    [ObservableProperty]
    private string _parseErrorMessage = string.Empty;

    [ObservableProperty]
    private string _parseErrorCode = string.Empty;

    public VariablesPageViewModel(
        ILocalizationService localizationService,
        SystemVariableProvider systemVariableProvider,
        PathResolver pathResolver,
        UserVariablesStore userVariablesStore,
        IEnumerable<ISourceProvider> sourceProviders)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _systemVariableProvider = systemVariableProvider;
        _pathResolver = pathResolver;
        _userVariablesStore = userVariablesStore;
        _sourceProviders = sourceProviders;

        PageTitle = Pick("变量", "Variables");
        PageSubtitle = Pick("精炼后的变量工作区，集中提供路径模板测试与显式覆盖优先级。", "Static refined variable workspace with path template testing and explicit override precedence.");
        SearchPlaceholder = Pick("搜索变量", "Search variables");
        ScopeFilterPlaceholder = Pick("全部范围", "All scopes");
        AddVariableText = Pick("添加变量", "Add Variable");
        TestTemplateText = Pick("测试模板", "Test Template");
        RefreshText = Pick("刷新", "Refresh");
        VariablesSectionTitle = Pick("变量", "Variables");

        VariableNameLabel = Pick("变量名", "Variable Name");
        ResolvedPathLabel = Pick("基础解析路径", "Base Resolved Path");
        DescriptionLabel = Pick("描述", "Description");

        PathTemplateTesterText = Pick("路径模板测试器", "Path Template Tester");
        InputTemplateText = Pick("输入模板字符串", "Input Template String");
        ResolvedOutputText = Pick("解析输出", "Resolved Output");
        OverridePrecedenceTitle = Pick("覆盖优先级", "Override Precedence");
        SaveChangesText = Pick("编辑变量", "Edit Variable");
        ResetText = Pick("清空测试器", "Clear Tester");
        DeleteVariableText = Pick("删除变量", "Delete Variable");
        ParseErrorTitle = Pick("解析错误", "Parse Error");
        ErrorStatusText = Pick("未解析", "UNRESOLVED");
        ErrorBadgeText = Pick("失败", "FAILED");
        OverridePrecedence = Pick("自定义 > 实例 > 来源 > 系统", "Custom > Instance > Source > System");

        Filters =
        [
            Pick("全部范围", "All scopes"),
            Pick("系统", "System"),
            Pick("来源", "Source"),
            Pick("自定义", "Custom"),
        ];
        _selectedScopeFilter = Filters[0];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string ScopeFilterPlaceholder { get; }
    public string AddVariableText { get; }
    public string TestTemplateText { get; }
    public string RefreshText { get; }
    public string VariablesSectionTitle { get; }
    public string VariableNameLabel { get; }
    public string ResolvedPathLabel { get; }
    public string DescriptionLabel { get; }
    public string PathTemplateTesterText { get; }
    public string InputTemplateText { get; }
    public string ResolvedOutputText { get; }
    public string OverridePrecedenceTitle { get; }
    public string SaveChangesText { get; }
    public string ResetText { get; }
    public string DeleteVariableText { get; }
    public string ParseErrorTitle { get; }
    public string ErrorStatusText { get; }
    public string ErrorBadgeText { get; }
    public string OverridePrecedence { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<string> Filters { get; }

    public VariableRow? SelectedVariable { get; private set; }

    public void Load()
    {
        try
        {
            var rows = new List<VariableRow>();
            foreach (var pair in _systemVariableProvider.GetVariables().OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new VariableRow($"%{pair.Key.ToUpperInvariant()}%", pair.Value, "SYS", Pick("系统", "System"), "ready", false));
            }

            foreach (var provider in _sourceProviders)
            {
                var probeInstance = new GameInstance
                {
                    GameId = "probe",
                    DisplayName = "probe",
                    SourceProviderId = provider.ProviderId,
                };

                try
                {
                    foreach (var pair in provider.ResolveVariables(probeInstance).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        rows.Add(new VariableRow($"%{pair.Key.ToUpperInvariant()}%", pair.Value, "SRC", Pick("来源", "Source"), "remote newer", false));
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Source provider {ProviderId} failed to resolve variables.", provider.ProviderId);
                }
            }

            foreach (var variable in _userVariablesStore.List().OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new VariableRow(NormalizeName(variable.Name), variable.Value, "USR", Pick("自定义", "Custom"), "synced", false));
            }

            _allVariables = rows;
            ApplyFilters();

            var selected = SelectedVariable is not null
                ? rows.FirstOrDefault(row => string.Equals(row.Name, SelectedVariable.Name, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectVariable(selected ?? rows.FirstOrDefault());
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load variables page data.");
        }
    }

    public void SelectVariable(VariableRow? row)
    {
        SelectedVariable = row;
        if (row is null)
        {
            SelectedVariableName = string.Empty;
            VariableScopeText = string.Empty;
            VariableProperties = [];
            return;
        }

        SelectedVariableName = row.Name;
        VariableScopeText = row.ScopeLabel;

        var userVariable = _userVariablesStore.List()
            .FirstOrDefault(variable => string.Equals(NormalizeName(variable.Name), row.Name, StringComparison.OrdinalIgnoreCase));
        LastModifiedText = userVariable is not null
            ? Pick($"最近修改 {UiFormat.RelativeTime(userVariable.UpdatedAtUtc, true)}", $"Last modified {UiFormat.RelativeTime(userVariable.UpdatedAtUtc, false)}")
            : Pick("内置变量 · 随系统解析", "Built-in · resolved from the system");

        var properties = new List<UiPropertyRow>
        {
            new(VariableNameLabel, row.Name),
            new(ResolvedPathLabel, row.Value),
            new(Pick("范围", "Scope"), row.ScopeLabel),
        };
        if (!string.IsNullOrWhiteSpace(userVariable?.Description))
        {
            properties.Add(new UiPropertyRow(DescriptionLabel, userVariable.Description));
        }

        VariableProperties = properties;
    }

    public bool IsSelectedVariableUserDefined()
    {
        return SelectedVariable?.TypeCode == "USR";
    }

    public UserVariable? GetSelectedUserVariable()
    {
        if (SelectedVariable is null)
        {
            return null;
        }

        return _userVariablesStore.List()
            .FirstOrDefault(variable => string.Equals(NormalizeName(variable.Name), SelectedVariable.Name, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveUserVariable(string name, string value, string? description)
    {
        _userVariablesStore.Upsert(new UserVariable
        {
            Name = NormalizeName(name),
            Value = value,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
        });
        Load();
    }

    public void DeleteSelectedUserVariable()
    {
        var variable = GetSelectedUserVariable();
        if (variable is null)
        {
            return;
        }

        _userVariablesStore.Remove(variable.Name);
        SelectedVariable = null;
        Load();
    }

    public void TestTemplate(string template)
    {
        TemplateInput = template;
        if (string.IsNullOrWhiteSpace(template))
        {
            TemplateOutput = string.Empty;
            ShowParseError = false;
            return;
        }

        try
        {
            var resolved = _pathResolver.Resolve(
                template,
                _systemVariableProvider.GetVariables(),
                BuildSourceVariables(),
                _userVariablesStore.AsDictionary());

            var unresolved = UnresolvedVariablePattern.Matches(resolved)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unresolved.Length > 0)
            {
                TemplateOutput = resolved;
                ShowParseError = true;
                ParseErrorMessage = Pick(
                    $"存在未解析的变量：{string.Join(", ", unresolved)}",
                    $"Unresolved variables remain: {string.Join(", ", unresolved)}");
                ParseErrorCode = "TEMPLATE_UNRESOLVED_VARIABLE";
            }
            else
            {
                TemplateOutput = resolved;
                ShowParseError = false;
            }
        }
        catch (Exception exception)
        {
            TemplateOutput = ErrorStatusText;
            ShowParseError = true;
            ParseErrorMessage = exception.Message;
            ParseErrorCode = "TEMPLATE_PARSE_ERROR";
        }
    }

    public void ClearTester()
    {
        TemplateInput = string.Empty;
        TemplateOutput = string.Empty;
        ShowParseError = false;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedScopeFilterChanged(string value) => ApplyFilters();

    private IReadOnlyDictionary<string, string> BuildSourceVariables()
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _sourceProviders)
        {
            try
            {
                var probeInstance = new GameInstance
                {
                    GameId = "probe",
                    DisplayName = "probe",
                    SourceProviderId = provider.ProviderId,
                };
                foreach (var pair in provider.ResolveVariables(probeInstance))
                {
                    merged[pair.Key] = pair.Value;
                }
            }
            catch
            {
            }
        }

        return merged;
    }

    private void ApplyFilters()
    {
        IEnumerable<VariableRow> filtered = _allVariables;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(row =>
                row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                row.Value.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedScopeFilter) && SelectedScopeFilter != Filters[0])
        {
            filtered = filtered.Where(row => string.Equals(row.ScopeLabel, SelectedScopeFilter, StringComparison.OrdinalIgnoreCase));
        }

        Variables = filtered.ToArray();
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim().Trim('%').ToUpperInvariant().Replace(' ', '_');
        return $"%{trimmed}%";
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record VariableRow(
    string Name,
    string Value,
    string TypeCode,
    string ScopeLabel,
    string StatusVariant,
    bool IsSelected);
