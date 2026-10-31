using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure.Wizard;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Utilities;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class AddGameWizardViewModel : ObservableObject
{
    public const string ManualSourceKey = "manual";

    private readonly bool _isChinese;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;
    private readonly IManifestService _manifestService;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly SyncTargetStore _syncTargetStore;
    private readonly AddGameMatchService _matchService;
    private readonly AddGamePathValidationService _pathValidationService;
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;
    private readonly ISyncEngine _syncEngine;
    private readonly IUserDefinitionStore _userDefinitionStore;
    private readonly IGameProfileRepository _profiles;
    private readonly SteamCatalogSearchService? _steamCatalogSearch;

    private IReadOnlyList<DiscoveredGame> _discoveredGames = [];
    private IReadOnlyList<GameContentDefinition> _definitions = [];
    private DiscoveredGame? _selectedGame;
    private GameDefinitionMatch? _selectedMatch;
    private IReadOnlyList<PathValidationResult> _pathValidationResults = [];
    private readonly HashSet<string> _selectedContentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _pathOverrides = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<GameCatalogCandidate> _manualCandidates = [];
    private GameProgramSelection? _programSelection;
    private const string ManualPathOptionKey = "__manual_path__";

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private bool _showNoResultsState;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _startFirstSync;

    [ObservableProperty]
    private IReadOnlyList<WizardOption> _sources = [];

    [ObservableProperty]
    private IReadOnlyList<WizardOption> _games = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _gameDetails = [];

    [ObservableProperty]
    private IReadOnlyList<WizardOption> _contentItems = [];

    [ObservableProperty]
    private IReadOnlyList<WizardPathReviewRow> _pathReview = [];

    [ObservableProperty]
    private IReadOnlyList<WizardOption> _targets = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _finishSummary = [];

    public AddGameWizardViewModel(
        ILocalizationService localizationService,
        IEnumerable<ISourceProvider> sourceProviders,
        IManifestService manifestService,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        SyncTargetStore syncTargetStore,
        AddGameMatchService matchService,
        AddGamePathValidationService pathValidationService,
        PathResolver pathResolver,
        SystemVariableProvider systemVariableProvider,
        ISyncEngine syncEngine,
        IUserDefinitionStore userDefinitionStore,
        IGameProfileRepository profiles,
        SteamCatalogSearchService? steamCatalogSearch = null)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _sourceProviders = sourceProviders;
        _manifestService = manifestService;
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncTargetStore = syncTargetStore;
        _matchService = matchService;
        _pathValidationService = pathValidationService;
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;
        _syncEngine = syncEngine;
        _userDefinitionStore = userDefinitionStore;
        _profiles = profiles;
        _steamCatalogSearch = steamCatalogSearch;

        PageTitle = Pick("添加游戏", "Add Game");
        PageSubtitle = Pick("选择游戏和存档、设置同步目标，然后检查并完成。", "Choose a game and saves, set a sync target, then review and finish.");
        WizardRailTitle = Pick("配置流程", "Profile Flow");
        WizardRailSubtitle = Pick("按三个阶段完成配置。", "Complete setup in three stages.");
        Step2DetailsTitle = Pick("所选游戏详情", "Selected game details");
        Step4ReviewTitle = Pick("已解析路径检查", "Resolved path review");
        Step6ReadyTitle = Pick("准备添加游戏", "Ready to Add Game");
        FinishCalloutTitle = Pick("确认后将创建游戏配置", "The game profile is created on confirm");
        FinishCalloutMessage = Pick("创建后可随时在游戏详情页执行上传、下载、比较与恢复。", "After creation you can upload, download, compare, and restore from the game detail page at any time.");
        StartFirstSyncText = Pick("创建后检查远端并准备首次同步", "Check the remote target and prepare the first sync after creation");
        CancelText = Pick("取消", "Cancel");
        BackText = Pick("返回", "Back");
        NoResultsTitle = Pick("没有找到匹配的游戏", "No matching games found");
        NoResultsMessage = Pick("当前来源没有检测到游戏。返回上一步切换来源，或改用手动配置继续。", "No games were detected under the current source. Go back to switch source, or continue with manual setup.");
        NoResultsPrimaryActionText = Pick("改用手动配置", "Switch to manual setup");
        NoResultsSecondaryActionText = Pick("返回来源选择", "Back to source selection");

        SourcesTitle = Pick("可用来源", "Available sources");
        SourcesSubtitle = Pick("自动来源会尝试检测已安装游戏；手动来源允许自定义路径。", "Automated sources try to detect installed games; the manual source allows custom paths.");
        GamesTitle = Pick("游戏候选", "Game candidates");
        GamesSubtitle = Pick("确认或选择要添加的游戏。", "Confirm or choose the game to add.");
        ContentTitle = Pick("同步内容", "Content to sync");
        ContentSubtitle = Pick("点击条目可切换启用状态。", "Tap an item to toggle whether it is synchronized.");
        TargetsTitle = Pick("可用目标", "Available targets");
        TargetsSubtitle = Pick("选择保存位置；也可以在此配置或测试目标。", "Choose a destination, or configure and test a target here.");

        BuildSources();
        SelectedSourceKey = null;
        CurrentStep = 1;
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string WizardRailTitle { get; }
    public string WizardRailSubtitle { get; }
    public string SourcesTitle { get; }
    public string SourcesSubtitle { get; }
    public string GamesTitle { get; }
    public string GamesSubtitle { get; }
    public string Step2DetailsTitle { get; }
    public string ContentTitle { get; }
    public string ContentSubtitle { get; }
    public string Step4ReviewTitle { get; }
    public string TargetsTitle { get; }
    public string TargetsSubtitle { get; }
    public string Step6ReadyTitle { get; }
    public string FinishCalloutTitle { get; }
    public string FinishCalloutMessage { get; }
    public string StartFirstSyncText { get; }
    public string CancelText { get; }
    public string BackText { get; }
    public string NoResultsTitle { get; }
    public string NoResultsMessage { get; }
    public string NoResultsPrimaryActionText { get; }
    public string NoResultsSecondaryActionText { get; }
    public IReadOnlyList<WizardStepItem> Steps =>
    [
        MakeStep(1, Pick("选择来源", "Source")),
        MakeStep(2, Pick("选择游戏", "Game")),
        MakeStep(3, Pick("选择内容", "Content")),
        MakeStep(4, Pick("检查路径", "Paths")),
        MakeStep(5, Pick("同步目标", "Target")),
        MakeStep(6, Pick("检查并完成", "Finish")),
    ];
    private WizardStepItem MakeStep(int index, string title) => new(index, title, CurrentStep == index, CurrentStep > index);
    public IReadOnlyList<WizardStepItem> Stages =>
    [
        new(1, Pick("游戏与存档", "Game and saves"), CurrentStep <= 4, CurrentStep > 4),
        new(2, Pick("同步目标", "Sync target"), CurrentStep == 5, CurrentStep > 5),
        new(3, Pick("检查并完成", "Review and finish"), CurrentStep == 6, false),
    ];
    public string ManageTargetsText => Pick("配置或测试目标", "Configure or test targets");
    private string _manualGameName = string.Empty;
    private string _manualSavePath = string.Empty;
    private string _manualProgramPath = string.Empty;
    private string _manualMatchMessage = string.Empty;
    private string? _namespaceGameId;
    private Guid? _linkedTargetId;
    private string? _linkedNamespace;
    private IReadOnlyList<ContentRootLink>? _remoteLinks;
    private bool _applyingRemoteLink;

    [ObservableProperty]
    private string _remoteNamespaceText = string.Empty;

    public string RemoteNamespaceLabel => Pick("远端目录", "Remote directory");
    public string RemoteNamespaceHelp => Pick("在另一台电脑已有存档时，请选择已有远端存档并对应本机内容。", "To use saves from another computer, link an existing remote save and match its content.");
    public string LinkRemoteText => Pick("关联已有远端存档…", "Link existing remote saves…");
    public string LinkRemoteStatus => HasRemoteLinks() ? Pick("已关联远端存档，创建后请先比较。", "Remote saves linked. Compare them after creating the profile.") : string.Empty;
    public IReadOnlyList<LocalContentRoot> GetLinkableRoots() => _selectedMatch is null ? [] :
        RemoteContentLayout.LocalRoots(_selectedMatch.Definition, BuildBaseContentSelections()).Select(root =>
        {
            var reviewed = PathReview.FirstOrDefault(row => string.Equals(row.ContentId, root.ContentId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.Template.Replace('\\', '/'), root.Template, StringComparison.OrdinalIgnoreCase));
            return reviewed is null ? root : root with { LocalPath = reviewed.ResolvedPath };
        }).ToArray();

    public void LinkExistingRemote(Guid targetId, string remoteNamespace, IReadOnlyList<ContentRootLink> links)
    {
        if (_selectedMatch is null || SelectedTargetId != targetId) throw new InvalidOperationException("The selected game or target changed.");
        _ = RemoteContentLayout.ApplyLinks(_selectedMatch.Definition, BuildBaseContentSelections(), links);
        _applyingRemoteLink = true;
        try { RemoteNamespaceText = remoteNamespace; }
        finally { _applyingRemoteLink = false; }
        _linkedTargetId = targetId;
        _linkedNamespace = remoteNamespace;
        _remoteLinks = links.ToArray();
        OnPropertyChanged(nameof(LinkRemoteStatus));
    }

    partial void OnRemoteNamespaceTextChanged(string value)
    {
        if (!_applyingRemoteLink) _remoteLinks = null;
        OnPropertyChanged(nameof(LinkRemoteStatus));
    }

    public string ManualGameName
    {
        get => _manualGameName;
        set => SetProperty(ref _manualGameName, value);
    }

    public string ManualSavePath
    {
        get => _manualSavePath;
        set => SetProperty(ref _manualSavePath, value);
    }

    public string ManualProgramPath
    {
        get => _manualProgramPath;
        private set => SetProperty(ref _manualProgramPath, value);
    }

    public string ManualMatchMessage
    {
        get => _manualMatchMessage;
        private set => SetProperty(ref _manualMatchMessage, value);
    }

    public string ManualNameLabel => Pick("游戏名称", "Game name");
    public string ManualNamePlaceholder => Pick("例如：Elden Ring", "For example: Elden Ring");
    public string ManualPathLabel => Pick("自定义存档目录或文件（未匹配时填写）", "Custom save folder or file (if not matched)");
    public string ManualProgramLabel => Pick("游戏程序或快捷方式（可选）", "Game executable or shortcut (optional)");
    public string BrowseProgramText => Pick("选择程序或快捷方式", "Choose executable or shortcut");
    public string ManualMatchHelp => Pick("根据名称查找本地游戏资料；必要时查询 Steam 公开名称。选择程序不会启动游戏。", "Match the name against local game definitions and, when needed, public Steam titles. Selecting a program does not launch it.");
    public string RefreshCatalogText => Pick("更新游戏资料库", "Update game catalog");
    public string BrowseFolderText => Pick("选择文件夹", "Browse folder");
    public string BrowseFileText => Pick("选择文件", "Browse file");
    public bool IsChinese => _isChinese;

    public string? SelectedSourceKey { get; private set; }
    public Guid? SelectedTargetId { get; private set; }
    public Guid? LastCreatedInstanceId { get; private set; }

    public string CurrentStepTitle => CurrentStep switch
    {
        1 => Pick("游戏与存档 · 选择来源", "Game and saves · Choose a source"),
        2 => Pick("游戏与存档 · 选择游戏", "Game and saves · Choose a game"),
        3 => Pick("游戏与存档 · 选择内容", "Game and saves · Choose content"),
        4 => Pick("游戏与存档 · 检查路径", "Game and saves · Review paths"),
        5 => Pick("同步目标 · 选择位置", "Sync target · Choose a destination"),
        6 => Pick("检查并完成", "Review and finish"),
        _ => Pick("游戏与存档", "Game and saves"),
    };

    public string CurrentStepDescription => CurrentStep switch
    {
        1 => Pick("选择最适合要添加游戏的来源。Steam/Epic 会扫描本机安装目录；手动来源支持任意游戏。", "Choose the source that matches the game. Steam/Epic scan local install directories; the manual source supports any game."),
        2 when IsManualSourceSelected => Pick("确认匹配的游戏资料；相似结果需要手动选择。", "Confirm the matching game definition; choose among similar results."),
        2 => ShowNoResultsState
            ? Pick("当前来源下没有匹配的检测结果。可以返回切换来源，或改用手动配置。", "No matching results were found under the current source. Go back to switch source, or use manual setup.")
            : Pick("从所选来源中选择一个检测到的游戏。系统会优先使用稳定标识匹配内容定义，其次回退到名称级匹配。", "Choose a discovered game. The system prefers stable identifiers when matching content definitions, then falls back to name-level matching."),
        3 => Pick("选择此配置应同步哪些存档与配置内容。点击条目切换启用状态。", "Choose which save and configuration items should be synchronized. Tap an item to toggle it."),
        4 => Pick("检查每条路径；解析不正确时可直接输入本机路径或选择文件、文件夹。", "Review each path; enter a local path or browse for a file or folder if the result is incorrect."),
        5 => Pick("选择此游戏配置应同步到哪个目标。", "Choose the sync target that should receive this game profile."),
        6 => Pick("检查最终摘要、匹配依据和路径预检结果，确认后创建游戏配置。", "Review the summary, match basis, and path validation results, then create the game profile."),
        _ => string.Empty,
    };

    public string CurrentStepFooter => CurrentStep switch
    {
        1 => Pick("选择来源后继续。", "Choose a source to continue."),
        2 => ShowNoResultsState
            ? Pick("返回上一步或切换到手动配置。", "Go back or use manual setup.")
            : Pick("确认选择的游戏。", "Confirm the selected game."),
        3 => Pick("至少保留一个内容项。", "Keep at least one content item."),
        4 => Pick("检查解析后的路径。", "Review the resolved paths."),
        5 => Pick("选择同步目标。", "Choose a sync target."),
        6 => Pick("检查配置后创建。", "Review and create the profile."),
        _ => string.Empty,
    };

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsStep6 => CurrentStep == 6;
    public int PathWarningCount => _pathValidationResults.Count(result => result.Severity == PathValidationSeverity.Warning);
    public bool CanGoBack => CurrentStep > 1;
    public bool CanProceed => !IsBusy;
    public string NextButtonText => CurrentStep == 6 ? Pick("创建配置", "Create Profile") : Pick("下一步", "Next Step");
    public bool IsManualSourceSelected => string.Equals(SelectedSourceKey, ManualSourceKey, StringComparison.OrdinalIgnoreCase);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanProceed));

    partial void OnCurrentStepChanged(int value)
    {
        if (value != 2)
        {
            ShowNoResultsState = false;
        }

        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(Stages));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(CurrentStepFooter));
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsStep5));
        OnPropertyChanged(nameof(IsStep6));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(NextButtonText));
    }

    partial void OnShowNoResultsStateChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(CurrentStepFooter));
    }

    public void SelectOption(WizardOption option)
    {
        switch (CurrentStep)
        {
            case 1:
                SelectedSourceKey = option.Key;
                Sources = Sources.Select(candidate => candidate with { IsSelected = candidate.Key == option.Key }).ToArray();
                OnPropertyChanged(nameof(IsManualSourceSelected));
                break;
            case 2:
                SelectGame(option.Key);
                break;
            case 3:
                ToggleContentItem(option.Key);
                break;
            case 5:
                if (SelectedTargetId?.ToString("D") != option.Key) _remoteLinks = null;
                SelectedTargetId = Guid.TryParse(option.Key, out var targetId) ? targetId : null;
                Targets = Targets.Select(candidate => candidate with { IsSelected = candidate.Key == option.Key }).ToArray();
                OnPropertyChanged(nameof(LinkRemoteStatus));
                break;
        }
    }

    public string? ValidateCurrentStep()
    {
        return CurrentStep switch
        {
            1 when SelectedSourceKey is null => Pick("请先选择一个来源。", "Choose a source first."),
            2 when _selectedGame is null || _selectedMatch is null => Pick("请先选择一个游戏。", "Choose a game first."),
            3 when _selectedMatch?.Definition.ContentItems.Count == 0 => Pick("未找到可靠的存档路径，请返回并使用手动配置指定存档位置。", "No reliable save paths were found. Go back and use manual setup to specify the save location."),
            3 when _selectedContentIds.Count == 0 => Pick("至少保留一个启用的内容项。", "Keep at least one content item enabled."),
            4 when _pathValidationResults.Any(result => result.Severity == PathValidationSeverity.Blocking) => Pick("仍存在阻断性路径错误，请先修复后再继续。", "Blocking path validation errors remain. Fix them before continuing."),
            5 when SelectedTargetId is null => Pick("请先选择一个同步目标。", "Choose a sync target first."),
            5 or 6 when !TryNormalizeRemoteNamespace(RemoteNamespaceText, out _) => Pick("远端目录必须是相对路径，不能包含空目录、.. 或保留目录。", "Enter a relative remote directory without empty segments, .., or reserved directories."),
            _ => null,
        };
    }

    public void GoNext()
    {
        if (CurrentStep < 6)
        {
            CurrentStep++;
            PrepareStep();
        }
    }

    public void GoBack()
    {
        if (CurrentStep == 3 && IsManualSourceSelected && _manualCandidates.Count == 0)
        {
            CurrentStep = 1;
        }
        else if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    public void SwitchToManualSetup()
    {
        ShowNoResultsState = false;
        SelectedSourceKey = ManualSourceKey;
        Sources = Sources.Select(candidate => candidate with { IsSelected = candidate.Key == ManualSourceKey }).ToArray();
        OnPropertyChanged(nameof(IsManualSourceSelected));
    }

    public void BackToSourceSelection()
    {
        ShowNoResultsState = false;
        CurrentStep = 1;
    }

    public async Task ScanSelectedSourceAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _definitions = await _manifestService.GetDefinitionsAsync(cancellationToken);

            var provider = _sourceProviders.FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderId, SelectedSourceKey, StringComparison.OrdinalIgnoreCase));
            _discoveredGames = provider is null
                ? []
                : await provider.ScanAsync(cancellationToken);

            Games = _discoveredGames
                .OrderBy(game => game.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(game =>
                {
                    var match = _matchService.Match(game, _definitions, CreateFallbackDefinition);
                    return MakeOption(game.GameId, game.DisplayName, DescribeMatchSummary(match));
                })
                .ToArray();

            ShowNoResultsState = Games.Count == 0;
            _selectedGame = null;
            _selectedMatch = null;
            _pathOverrides.Clear();
            _pathValidationResults = [];
            GameDetails = [];
            PathReview = [];
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Source scan failed for {SourceKey}.", SelectedSourceKey);
            ShowNoResultsState = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetProgramSelection(GameProgramSelection selection)
    {
        var previousName = _programSelection?.DisplayName;
        _programSelection = selection;
        ManualProgramPath = selection.SourcePath;
        if (string.IsNullOrWhiteSpace(ManualGameName) || string.Equals(ManualGameName, previousName, StringComparison.OrdinalIgnoreCase))
            ManualGameName = selection.DisplayName;
    }

    public async Task<string?> RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is already in progress.");
        IsBusy = true;
        try
        {
            await Task.Run(() => _manifestService.RefreshCommunityDefinitionsAsync(cancellationToken), cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not refresh the game catalog.");
            return Pick($"更新资料库失败：{exception.Message}", $"Could not update the game catalog: {exception.Message}");
        }
        finally { IsBusy = false; }
    }

    public async Task<string?> PrepareManualCandidatesAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is already in progress.");
        if (string.IsNullOrWhiteSpace(ManualGameName))
            return Pick("请填写游戏名称或选择游戏程序。", "Enter a game name or choose a game executable.");
        IsBusy = true;
        try
        {
            _definitions = await Task.Run(() => _manifestService.GetDefinitionsAsync(cancellationToken), cancellationToken);
            var game = CreateManualDiscoveredGame(ManualGameName);
            _discoveredGames = [game];
            _selectedGame = game;
            _selectedMatch = null;
            _pathOverrides.Clear();
            _selectedContentIds.Clear();
            _pathValidationResults = [];
            var candidates = (await Task.Run(() => _matchService.FindNameCandidates(game.DisplayName, _definitions), cancellationToken)).ToList();
            var searchFailed = false;
            if (!string.IsNullOrWhiteSpace(_programSelection?.SteamId))
            {
                foreach (var definition in _definitions.Where(definition => definition.ContentItems.Count > 0 &&
                    definition.SourceHints.TryGetValue("steam.id", out var id) && id == _programSelection.SteamId))
                    if (!candidates.Any(candidate => candidate.Definition.GameId == definition.GameId))
                        candidates.Add(new GameCatalogCandidate(definition, true, $"Steam ID {_programSelection.SteamId}"));
            }
            if (candidates.Count(candidate => candidate.IsExact) != 1 && _steamCatalogSearch is not null)
            {
                try
                {
                    var results = await _steamCatalogSearch.SearchAsync(game.DisplayName, _isChinese, cancellationToken);
                    var bySteamId = _definitions.Where(definition => definition.ContentItems.Count > 0 && definition.SourceHints.ContainsKey("steam.id"))
                        .GroupBy(definition => definition.SourceHints["steam.id"], StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
                    foreach (var result in results)
                    {
                        if (!bySteamId.TryGetValue(result.SteamId, out var matches)) continue;
                        foreach (var definition in matches)
                        {
                            var exact = AddGameMatchService.NormalizeName(result.Name) == AddGameMatchService.NormalizeName(game.DisplayName);
                            var existingIndex = candidates.FindIndex(candidate => candidate.Definition.GameId == definition.GameId);
                            if (existingIndex >= 0)
                            {
                                if (exact && !candidates[existingIndex].IsExact)
                                    candidates[existingIndex] = new GameCatalogCandidate(definition, true, result.Name);
                            }
                            else candidates.Add(new GameCatalogCandidate(definition, exact, result.Name));
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or IOException or UriFormatException)
                {
                    searchFailed = true;
                    Log.Warning(exception, "Public game name lookup failed; local matching is still available.");
                }
            }
            _manualCandidates = candidates.OrderByDescending(candidate => candidate.IsExact)
                .ThenBy(candidate => candidate.Definition.DisplayName, StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
            if (_manualCandidates.Count == 0 && string.IsNullOrWhiteSpace(ManualSavePath))
                return searchFailed
                    ? Pick("没有找到本地匹配，Steam 名称查询暂不可用。请填写自定义存档路径后继续。", "No local match was found and Steam name search is unavailable. Enter a custom save path to continue.")
                    : Pick("没有找到游戏资料。可更新游戏资料库，或填写自定义存档路径后继续。", "No game definition was found. Update the game catalog or enter a custom save path to continue.");

            ManualMatchMessage = searchFailed
                ? Pick("Steam 名称查询暂不可用，以下为本地资料库结果。", "Steam name search is unavailable; showing local catalog results.")
                : Pick("请确认匹配结果；自定义路径可作为独立选项。", "Confirm the match; the custom path is offered as a separate option.");
            var options = _manualCandidates.Select(candidate => MakeOption(candidate.Definition.GameId,
                candidate.Definition.DisplayName,
                candidate.IsExact
                    ? Pick($"名称或标识匹配 · {candidate.MatchedName}", $"Name or identifier match · {candidate.MatchedName}")
                    : Pick($"相似结果，请确认 · {candidate.MatchedName}", $"Similar result; confirm · {candidate.MatchedName}"))).ToList();
            if (!string.IsNullOrWhiteSpace(ManualSavePath))
                options.Add(MakeOption(ManualPathOptionKey, Pick("使用自定义存档路径", "Use custom save path"), ManualSavePath));
            Games = options;
            ShowNoResultsState = false;
            GameDetails = [];
            var exactCandidates = _manualCandidates.Where(candidate => candidate.IsExact).ToArray();
            if (exactCandidates.Length == 1) SelectGame(exactCandidates[0].Definition.GameId);
            else if (_manualCandidates.Count == 0) SelectGame(ManualPathOptionKey);
            return null;
        }
        finally { IsBusy = false; }
    }

    public void SetManualGame(string displayName, string savePath)
    {
        _remoteLinks = null;
        ManualGameName = displayName.Trim();
        ManualSavePath = savePath.Trim();
        var manualGame = CreateManualDiscoveredGame(displayName);
        _discoveredGames = [manualGame];
        _selectedGame = manualGame;
        _manualCandidates = [];
        _selectedMatch = CreateManualMatch(manualGame, savePath);
        _pathOverrides.Clear();
        _selectedContentIds.Clear();
        _pathValidationResults = [];

        Games = [MakeOption(manualGame.GameId, manualGame.DisplayName, Pick("手动配置 · 1 个内容项", "Manual setup · 1 content item"), true)];
        ShowNoResultsState = false;
        BuildGameDetails();
    }

    private DiscoveredGame CreateManualDiscoveredGame(string displayName)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_programSelection is not null)
            variables[GameProgramSelectionReader.ProgramPathVariable] = _programSelection.ExecutablePath ?? _programSelection.SourcePath;
        return new DiscoveredGame
        {
            GameId = GameIdentity.ForManualName(displayName),
            DisplayName = displayName.Trim(),
            SourceProviderId = "custom",
            InstallDirectory = _programSelection?.InstallDirectory,
            PlatformGameId = _programSelection?.SteamId,
            Variables = variables,
        };
    }

    private GameDefinitionMatch CreateManualMatch(DiscoveredGame manualGame, string savePath) =>
        new(
            manualGame,
            new GameContentDefinition
            {
                GameId = manualGame.GameId,
                DisplayName = manualGame.DisplayName,
                Description = Pick("用户手动创建的内容定义。", "User-created content definition."),
                ContentItems =
                [
                    new ContentItem
                    {
                        ContentId = "saves",
                        Category = ContentCategory.Save,
                        PathTemplates = [savePath.Trim().Replace('\\', '/')],
                        DefaultEnabled = true,
                        Description = Pick("手动指定的存档目录。", "Manually specified save location."),
                    },
                ],
            },
            MatchConfidence.Manual,
            MatchOrigin.ManualDefinition,
            "manual-entry",
            true);

    public async Task<string?> CreateProfileAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("正在创建配置，请稍候。", "The profile is being created. Please wait.");
        var validation = ValidateCurrentStep();
        if (validation is not null)
        {
            return validation;
        }

        if (_selectedGame is null || _selectedMatch is null || SelectedTargetId is not { } targetId)
        {
            return Pick("向导状态不完整，请从头开始。", "Wizard state is incomplete; start over.");
        }

        var target = _syncTargetStore.Get(targetId);
        if (target is null)
        {
            return Pick("所选同步目标已不存在。", "The selected sync target no longer exists.");
        }

        IsBusy = true;
        LastCreatedInstanceId = null;
        try
        {
            if (_selectedContentIds.Count == 0 || _selectedMatch.Definition.ContentItems.Count == 0)
                return Pick("请先明确选择存档路径。", "Choose the save paths first.");
            var pathResults = _pathValidationService.Validate(_selectedMatch,
                _selectedMatch.Definition.ContentItems.Where(item => _selectedContentIds.Contains(item.ContentId)),
                BuildSourceVariables(_selectedGame, _selectedMatch.Definition), _selectedGame.Variables, BuildContentSelections());
            if (pathResults.Any(result => result.Severity == PathValidationSeverity.Blocking))
                return Pick("存档路径仍有错误，请先修复。", "Fix the save path errors before creating the profile.");
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            if (instances.Any(existing => string.Equals(existing.SourceProviderId, _selectedGame.SourceProviderId, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(existing.GameId, _selectedMatch.Definition.GameId, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrWhiteSpace(_selectedGame.PlatformGameId) && string.Equals(existing.PlatformInstanceId, _selectedGame.PlatformGameId, StringComparison.OrdinalIgnoreCase)) ||
                 (_selectedGame.SourceProviderId == "custom" && string.Equals(existing.DisplayName.Trim(), _selectedGame.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase)))))
                return Pick("该游戏已经在游戏库中，请编辑现有配置。", "This game is already in the library. Edit its existing profile.");

            var instance = new GameInstance
            {
                GameId = _selectedMatch.Definition.GameId,
                DisplayName = _selectedGame.DisplayName,
                SourceProviderId = _selectedGame.SourceProviderId,
                InstallDirectory = _selectedGame.InstallDirectory,
                PlatformInstanceId = _selectedGame.PlatformGameId,
                Variables = _selectedGame.Variables,
            };

            var bindingSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in target.Settings)
            {
                bindingSettings[pair.Key] = pair.Value;
            }

            bindingSettings["targetId"] = target.Id.ToString("D");
            bindingSettings["targetName"] = target.Name;
            bindingSettings["matchConfidence"] = _selectedMatch.Confidence.ToString();
            bindingSettings["matchReason"] = _selectedMatch.Reason;
            if (HasRemoteLinks()) bindingSettings["__gsyncRootMappingTargetId"] = target.Id.ToString("D");

            var binding = new StorageBinding
            {
                GameInstanceId = instance.Id,
                StorageProviderId = target.ProviderId,
                RemoteNamespace = TryNormalizeRemoteNamespace(RemoteNamespaceText, out var normalizedNamespace) ? normalizedNamespace : throw new InvalidDataException("Invalid remote directory."),
                Settings = bindingSettings,
                ContentSelections = BuildContentSelections(),
            };
            var hasUserDefinition = (await _userDefinitionStore.LoadDefinitionsAsync(cancellationToken))
                .Any(definition => string.Equals(definition.GameId, instance.GameId, StringComparison.OrdinalIgnoreCase));
            if (!hasUserDefinition)
                await _userDefinitionStore.SaveDefinitionAsync(_selectedMatch.Definition, cancellationToken, overwrite: false);
            await _profiles.CreateAsync(instance, binding, cancellationToken);
            LastCreatedInstanceId = instance.Id;

            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to create game profile for {GameName}.", _selectedGame.DisplayName);
            return exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<(ConflictNavigationContext? Context, string? Error)> CompareCreatedProfileAsync(CancellationToken cancellationToken = default)
    {
        if (LastCreatedInstanceId is not { } id || _selectedGame is null)
            return (null, Pick("游戏配置尚未创建。", "The game profile has not been created."));
        try
        {
            var comparison = await _syncEngine.CompareAsync(new SyncJob
            {
                GameInstanceId = id,
                Direction = SyncDirection.Compare,
                DisplayName = _selectedGame.DisplayName,
            }, cancellationToken);
            return (new ConflictNavigationContext(id, _selectedGame.DisplayName, comparison), null);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to compare a newly created profile.");
            return (null, exception.Message);
        }
    }

    public async Task<string?> QueueFirstUploadAsync(CancellationToken cancellationToken = default)
    {
        if (LastCreatedInstanceId is not { } id || _selectedGame is null)
            return Pick("游戏配置尚未创建。", "The game profile has not been created.");
        try
        {
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = id,
                Direction = SyncDirection.Upload,
                ProtectRemoteChanges = true,
                DisplayName = _selectedGame.DisplayName,
            }, cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue the first upload.");
            return exception.Message;
        }
    }

    private void PrepareStep()
    {
        switch (CurrentStep)
        {
            case 3:
                BuildContentItems();
                break;
            case 4:
                BuildPathReview();
                break;
            case 5:
                BuildTargets();
                break;
            case 6:
                BuildFinishSummary();
                break;
        }
    }

    private void BuildSources()
    {
        var options = new List<WizardOption>();
        foreach (var provider in _sourceProviders.Where(provider => provider.ProviderId is "steam" or "epic"))
        {
            var summary = provider.ProviderId switch
            {
                "steam" => Pick("扫描 Steam 库目录中已安装的游戏", "Scan installed games across Steam library folders"),
                "epic" => Pick("扫描 Epic 启动器安装的游戏", "Scan games installed by the Epic launcher"),
                _ => provider.DisplayName,
            };
            options.Add(MakeOption(provider.ProviderId, provider.DisplayName, summary));
        }

        options.Add(MakeOption(
            ManualSourceKey,
            Pick("手动 / 自定义", "Manual / Custom"),
            Pick("选择程序或输入名称，自动匹配游戏资料。", "Choose a program or enter a name to match game definitions.")));

        Sources = options;
    }

    private void SelectGame(string gameId)
    {
        _remoteLinks = null;
        OnPropertyChanged(nameof(LinkRemoteStatus));
        var previousId = _selectedMatch?.Definition.GameId;
        if (IsManualSourceSelected && _discoveredGames.Count == 1 && Games.Any(option => option.Key == gameId))
        {
            // Rebuild from the original program selection so switching candidates cannot
            // retain a different game's derived store ID or account wildcard.
            _selectedGame = CreateManualDiscoveredGame(ManualGameName);
            if (gameId == ManualPathOptionKey)
                _selectedMatch = CreateManualMatch(_selectedGame, ManualSavePath);
            else if (_manualCandidates.FirstOrDefault(candidate => candidate.Definition.GameId == gameId) is { } candidate)
            {
                var variables = new Dictionary<string, string>(_selectedGame.Variables, StringComparer.OrdinalIgnoreCase);
                if (candidate.Definition.ContentItems.SelectMany(item => item.PathTemplates)
                    .Any(path => path.Contains("%STORE_USER_ID%", StringComparison.OrdinalIgnoreCase)))
                    variables.TryAdd("STORE_USER_ID", "*");
                if (candidate.Definition.SourceHints.TryGetValue("steam.id", out var steamId))
                    variables.TryAdd("STORE_GAME_ID", steamId);
                _selectedGame = new DiscoveredGame
                {
                    GameId = _selectedGame.GameId, DisplayName = _selectedGame.DisplayName,
                    SourceProviderId = _selectedGame.SourceProviderId, InstallDirectory = _selectedGame.InstallDirectory,
                    PlatformGameId = _selectedGame.PlatformGameId is { } shortcutId &&
                        candidate.Definition.SourceHints.TryGetValue("steam.id", out var matchedId) && matchedId == shortcutId
                        ? shortcutId : null,
                    Variables = variables,
                };
                _selectedMatch = new GameDefinitionMatch(_selectedGame, candidate.Definition, MatchConfidence.NormalizedName,
                    MatchOrigin.ExistingDefinition, candidate.MatchedName, false);
            }
            else return;
            if (!string.Equals(previousId, _selectedMatch.Definition.GameId, StringComparison.OrdinalIgnoreCase)) _pathOverrides.Clear();
            _discoveredGames = [_selectedGame];
            _selectedContentIds.Clear();
            _pathValidationResults = [];
            Games = Games.Select(option => option with { IsSelected = option.Key == gameId }).ToArray();
            BuildGameDetails();
            return;
        }
        _selectedGame = _discoveredGames.FirstOrDefault(game =>
            string.Equals(game.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        if (_selectedGame is null)
        {
            return;
        }

        if (!IsManualSourceSelected)
        {
            _selectedMatch = _matchService.Match(_selectedGame, _definitions, CreateFallbackDefinition);
            if (!string.Equals(previousId, _selectedMatch.Definition.GameId, StringComparison.OrdinalIgnoreCase)) _pathOverrides.Clear();
            _selectedContentIds.Clear();
            _pathValidationResults = [];
        }

        Games = Games.Select(candidate => candidate with { IsSelected = candidate.Key == gameId }).ToArray();
        BuildGameDetails();
    }

    private void BuildGameDetails()
    {
        if (_selectedGame is null)
        {
            GameDetails = [];
            return;
        }

        GameDetails =
        [
            new(Pick("游戏", "Game"), _selectedGame.DisplayName),
            new(Pick("来源", "Source"), DescribeSource(_selectedGame.SourceProviderId)),
            new(Pick("安装路径", "Install Path"), _selectedGame.InstallDirectory ?? Pick("未检测到", "Not detected")),
            new(Pick("匹配方式", "Match Method"), DescribeMatchReason(_selectedMatch)),
            new(Pick("内容定义", "Content Definition"), _selectedMatch?.Definition.GameId ?? Pick("未匹配", "Not matched")),
        ];
    }

    private void BuildContentItems()
    {
        if (_selectedMatch is null)
        {
            ContentItems = [];
            return;
        }

        if (_selectedContentIds.Count == 0)
        {
            foreach (var item in _selectedMatch.Definition.ContentItems.Where(item => item.DefaultEnabled))
            {
                _selectedContentIds.Add(item.ContentId);
            }
        }

        ContentItems = _selectedMatch.Definition.ContentItems
            .Select(item => MakeOption(
                item.ContentId,
                item.ContentId,
                BuildContentSummary(item),
                _selectedContentIds.Contains(item.ContentId)))
            .ToArray();
    }

    private string BuildContentSummary(ContentItem item)
    {
        var templates = string.Join("; ", item.PathTemplates);
        var category = item.Category switch
        {
            ContentCategory.Save => Pick("存档", "Saves"),
            ContentCategory.Config => Pick("配置", "Config"),
            ContentCategory.Extra => Pick("附加", "Extra"),
            _ => item.Category.ToString(),
        };
        return $"{category} · {templates}";
    }

    private void ToggleContentItem(string contentId)
    {
        _remoteLinks = null;
        OnPropertyChanged(nameof(LinkRemoteStatus));
        if (!_selectedContentIds.Remove(contentId))
        {
            _selectedContentIds.Add(contentId);
        }

        ContentItems = ContentItems
            .Select(candidate => candidate with { IsSelected = _selectedContentIds.Contains(candidate.Key) })
            .ToArray();
    }

    private void BuildPathReview()
    {
        if (_selectedMatch is null || _selectedGame is null)
        {
            PathReview = [];
            _pathValidationResults = [];
            return;
        }

        var sourceVariables = BuildSourceVariables(_selectedGame, _selectedMatch.Definition);
        _pathValidationResults = _pathValidationService.Validate(
            _selectedMatch,
            _selectedMatch.Definition.ContentItems.Where(item => _selectedContentIds.Contains(item.ContentId)),
            sourceVariables,
            _selectedGame.Variables,
            BuildContentSelections());

        PathReview = _pathValidationResults
            .Select(result => new WizardPathReviewRow(result.ContentId, result.Template, result.ResolvedPath,
                $"{DescribeValidationSeverity(result.Severity)} · {DescribeValidationMessage(result.MessageCode)}",
                _pathOverrides.TryGetValue(result.ContentId, out var paths) && paths.TryGetValue(result.Template, out var path) ? path : string.Empty,
                _isChinese))
            .ToArray();
    }

    public void ApplyPathChanges()
    {
        foreach (var row in PathReview)
        {
            if (!_pathOverrides.TryGetValue(row.ContentId, out var paths))
                _pathOverrides[row.ContentId] = paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(row.OverridePath)) paths.Remove(row.Template);
            else paths[row.Template] = row.OverridePath.Trim().Replace('\\', '/');
        }
        BuildPathReview();
    }

    private bool HasRemoteLinks() => _remoteLinks is not null && _linkedTargetId == SelectedTargetId &&
        string.Equals(_linkedNamespace, RemoteNamespaceText.Trim().Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<ContentSelection> BuildContentSelections() => HasRemoteLinks() && _selectedMatch is not null
        ? RemoteContentLayout.ApplyLinks(_selectedMatch.Definition, BuildBaseContentSelections(), _remoteLinks!)
        : BuildBaseContentSelections();

    private IReadOnlyList<ContentSelection> BuildBaseContentSelections() =>
        _selectedMatch?.Definition.ContentItems.Select(item => new ContentSelection
        {
            ContentId = item.ContentId,
            IsEnabled = _selectedContentIds.Contains(item.ContentId),
            PathOverrides = _pathOverrides.TryGetValue(item.ContentId, out var paths)
                ? new Dictionary<string, string>(paths, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(),
        }).ToArray() ?? [];

    private IReadOnlyDictionary<string, string> BuildSourceVariables(DiscoveredGame game, GameContentDefinition definition)
    {
        var sourceProvider = _sourceProviders.FirstOrDefault(provider =>
            string.Equals(provider.ProviderId, game.SourceProviderId, StringComparison.OrdinalIgnoreCase));
        var probeInstance = new GameInstance
        {
            GameId = definition.GameId,
            DisplayName = game.DisplayName,
            SourceProviderId = game.SourceProviderId,
            InstallDirectory = game.InstallDirectory,
            PlatformInstanceId = game.PlatformGameId,
            Variables = game.Variables,
        };

        return sourceProvider?.ResolveVariables(probeInstance)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void BuildTargets()
    {
        if (_selectedMatch is not null && !string.Equals(_namespaceGameId, _selectedMatch.Definition.GameId, StringComparison.OrdinalIgnoreCase))
        {
            _namespaceGameId = _selectedMatch.Definition.GameId;
            RemoteNamespaceText = GameIdentity.NamedStorageNamespace(_selectedMatch.Definition.DisplayName, _selectedMatch.Definition.GameId);
        }
        var configs = _syncTargetStore.List();
        SelectedTargetId ??= _syncTargetStore.GetDefault()?.Id;

        Targets = configs
            .Select(config => MakeOption(
                config.Id.ToString("D"),
                config.Name,
                config.ProviderId == "webdav"
                    ? $"WebDAV · {config.Settings.GetValueOrDefault("baseUrl", "--")}"
                    : Pick($"本地文件夹 · {config.Settings.GetValueOrDefault("rootPath", "--")}", $"Local folder · {config.Settings.GetValueOrDefault("rootPath", "--")}"),
                config.Id == SelectedTargetId))
            .ToArray();
    }

    public void RefreshTargets(Guid? selectedTargetId = null)
    {
        if (selectedTargetId is { } requested && requested != SelectedTargetId) _remoteLinks = null;
        if (selectedTargetId is { } id && _syncTargetStore.Get(id) is not null) SelectedTargetId = id;
        else if (SelectedTargetId is { } current && _syncTargetStore.Get(current) is null) SelectedTargetId = null;
        BuildTargets();
    }

    private void BuildFinishSummary()
    {
        var target = SelectedTargetId is { } targetId ? _syncTargetStore.Get(targetId) : null;
        var blockingCount = _pathValidationResults.Count(result => result.Severity == PathValidationSeverity.Blocking);
        var warningCount = _pathValidationResults.Count(result => result.Severity == PathValidationSeverity.Warning);
        FinishSummary =
        [
            new(Pick("来源", "Source"), DescribeSource(_selectedGame?.SourceProviderId ?? SelectedSourceKey ?? "--")),
            new(Pick("游戏", "Game"), _selectedGame?.DisplayName ?? "--"),
            new(Pick("匹配方式", "Match Method"), DescribeMatchReason(_selectedMatch)),
            new(Pick("内容定义", "Content Definition"), _selectedMatch?.Definition.GameId ?? "--"),
            new(Pick("启用内容项", "Enabled Content"), _selectedContentIds.Count.ToString()),
            new(Pick("路径预检", "Path Validation"), Pick($"{blockingCount} 个阻断 / {warningCount} 个警告", $"{blockingCount} blocking / {warningCount} warnings")),
            new(Pick("目标", "Target"), target?.Name ?? "--"),
            new(Pick("远端目录", "Remote directory"), RemoteNamespaceText),
        ];
    }

    private static bool TryNormalizeRemoteNamespace(string value, out string normalized)
    {
        normalized = string.Empty;
        try
        {
            normalized = SafePath.ValidateRelative(value.Trim()).TrimEnd('/');
            return normalized.Split('/').All(segment => !string.IsNullOrEmpty(segment) &&
                !segment.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) &&
                !segment.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    private GameContentDefinition CreateFallbackDefinition(DiscoveredGame game)
    {
        return new GameContentDefinition
        {
            GameId = game.GameId,
            DisplayName = game.DisplayName,
            Description = Pick("未匹配社区清单时生成的回退定义。", "Fallback definition generated when no manifest match exists."),
            ContentItems = [],
        };
    }

    private string DescribeSource(string sourceProviderId)
    {
        return sourceProviderId.ToLowerInvariant() switch
        {
            "steam" => "Steam",
            "epic" => "Epic",
            "custom" or ManualSourceKey => Pick("自定义", "Custom"),
            _ => sourceProviderId,
        };
    }

    private string DescribeMatchSummary(GameDefinitionMatch match)
    {
        return match.Confidence switch
        {
            MatchConfidence.SourceHint => Pick($"稳定标识命中 · {match.Reason}", $"Stable identifier match · {match.Reason}"),
            MatchConfidence.PlatformId => Pick($"平台标识命中 · {match.Reason}", $"Platform identifier match · {match.Reason}"),
            MatchConfidence.NormalizedName => Pick($"名称级命中 · {match.Reason}", $"Name-level match · {match.Reason}"),
            MatchConfidence.Fallback => Pick("未匹配清单 · 将使用回退定义", "No manifest match · fallback definition will be used"),
            MatchConfidence.Manual => Pick("手动配置 · 用户提供存档路径", "Manual setup · user-provided save path"),
            _ => match.Reason,
        };
    }

    private string DescribeMatchReason(GameDefinitionMatch? match)
    {
        if (match is null)
        {
            return Pick("未匹配", "Not matched");
        }

        return match.Confidence switch
        {
            MatchConfidence.SourceHint => Pick($"稳定标识：{match.Reason}", $"Stable identifier: {match.Reason}"),
            MatchConfidence.PlatformId => Pick($"平台标识：{match.Reason}", $"Platform identifier: {match.Reason}"),
            MatchConfidence.NormalizedName => Pick($"名称级匹配：{match.Reason}", $"Name-level match: {match.Reason}"),
            MatchConfidence.Fallback => Pick("回退定义（未找到可靠清单）", "Fallback definition (no reliable manifest match)"),
            MatchConfidence.Manual => Pick("手动配置", "Manual setup"),
            _ => match.Reason,
        };
    }

    private string DescribeValidationSeverity(PathValidationSeverity severity)
    {
        return severity switch
        {
            PathValidationSeverity.Ok => Pick("通过", "OK"),
            PathValidationSeverity.Warning => Pick("警告", "Warning"),
            PathValidationSeverity.Blocking => Pick("阻断", "Blocking"),
            _ => severity.ToString(),
        };
    }

    private string DescribeValidationMessage(string messageCode)
    {
        return messageCode switch
        {
            "unresolved-template" => Pick("模板未能解析为有效路径", "Template could not be resolved to a valid path"),
            "path-exists" => Pick("路径存在，可用于建档", "Path exists and can be used"),
            "path-missing" => Pick("路径当前不存在，可能需要先运行游戏生成", "Path does not exist yet; the game may need to run first"),
            _ => messageCode,
        };
    }

    private WizardOption MakeOption(string key, string title, string summary, bool isSelected = false) =>
        new(key, title, summary, isSelected) { IsChinese = _isChinese };

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record WizardStepItem(int Index, string Title, bool IsCurrent, bool IsComplete)
{
    public double Opacity => IsCurrent ? 1 : IsComplete ? 0.8 : 0.55;
    public string Indicator => IsComplete ? "✓" : Index.ToString();
}

public sealed class WizardPathReviewRow(string contentId, string template, string resolvedPath, string status, string overridePath, bool chinese)
{
    public string ContentId { get; } = contentId;
    public string Template { get; } = template;
    public string ResolvedPath { get; } = resolvedPath;
    public string Status { get; } = status;
    public string OverridePath { get; set; } = overridePath;
    public string Title => $"{ContentId} · {Template}";
    public string Result => $"{ResolvedPath} · {Status}";
    public string OverrideLabel => chinese ? "本机路径（留空使用默认模板）" : "Local path (leave empty to use the default template)";
    public string BrowseFolderText => chinese ? "选择文件夹" : "Browse folder";
    public string BrowseFileText => chinese ? "选择文件" : "Browse file";
    public string ApplyText => chinese ? "重新检查" : "Recheck";
    public string ResetText => chinese ? "使用默认路径" : "Use default";
}

public sealed partial record WizardOption(string Key, string Title, string Summary, bool IsSelected = false)
{
    public bool IsChinese { get; init; }
    public string SelectionStatus => IsSelected ? (IsChinese ? "已选择" : "Selected") : (IsChinese ? "未选择" : "Not selected");
}
