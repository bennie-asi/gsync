using System.Text;
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
    private readonly IAppPathService _appPathService;

    private IReadOnlyList<DiscoveredGame> _discoveredGames = [];
    private IReadOnlyList<GameContentDefinition> _definitions = [];
    private DiscoveredGame? _selectedGame;
    private GameDefinitionMatch? _selectedMatch;
    private IReadOnlyList<PathValidationResult> _pathValidationResults = [];
    private readonly HashSet<string> _selectedContentIds = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private int _currentStep = 1;

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
    private IReadOnlyList<UiPropertyRow> _pathReview = [];

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
        IAppPathService appPathService)
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
        _appPathService = appPathService;

        PageTitle = Pick("添加游戏", "Add Game");
        PageSubtitle = Pick("六步向导：选择来源、确认游戏、勾选内容、检查路径、绑定目标并创建配置。", "Six-step wizard: choose a source, confirm the game, pick content, review paths, bind a target, and create the profile.");
        WizardRailTitle = Pick("配置流程", "Profile Flow");
        WizardRailSubtitle = Pick("固定六步，不在向导过程中重排信息架构。", "Fixed six-step flow with no layout drift between steps.");
        Step2DetailsTitle = Pick("检测到的游戏详情", "Detected game details");
        Step4ReviewTitle = Pick("已解析路径检查", "Resolved path review");
        Step6ReadyTitle = Pick("准备添加游戏", "Ready to Add Game");
        FinishCalloutTitle = Pick("确认后将创建游戏配置", "The game profile is created on confirm");
        FinishCalloutMessage = Pick("创建后可随时在游戏详情页执行上传、下载、比较与恢复。", "After creation you can upload, download, compare, and restore from the game detail page at any time.");
        StartFirstSyncText = Pick("完成后立即上传首次同步", "Start the first upload after finishing");
        CancelText = Pick("取消", "Cancel");
        BackText = Pick("返回", "Back");
        NoResultsTitle = Pick("没有找到匹配的游戏", "No matching games found");
        NoResultsMessage = Pick("当前来源没有检测到游戏。返回上一步切换来源，或改用手动配置继续。", "No games were detected under the current source. Go back to switch source, or continue with manual setup.");
        NoResultsPrimaryActionText = Pick("改用手动配置", "Switch to manual setup");
        NoResultsSecondaryActionText = Pick("返回来源选择", "Back to source selection");

        Steps =
        [
            new(1, Pick("选择来源", "Select Source"), Pick("选择 GSYNC 应该从哪里查找游戏。", "Choose where GSYNC should look for the game.")),
            new(2, Pick("选择游戏", "Select Game"), Pick("选择检测到的游戏或手动录入。", "Pick a discovered game or enter one manually.")),
            new(3, Pick("内容项", "Content Items"), Pick("选择要同步的存档与配置内容。", "Select saves and config content to synchronize.")),
            new(4, Pick("检查路径", "Review Paths"), Pick("验证解析路径与可移植性覆盖情况。", "Validate resolved paths and portability coverage.")),
            new(5, Pick("绑定目标", "Bind Target"), Pick("选择同步数据应存储到哪个目标。", "Choose where the profile will store synchronized data.")),
            new(6, Pick("完成", "Finish"), Pick("检查摘要并创建配置。", "Review the summary and create the profile.")),
        ];

        SourcesTitle = Pick("可用来源", "Available sources");
        SourcesSubtitle = Pick("自动来源会尝试检测已安装游戏；手动来源允许自定义路径。", "Automated sources try to detect installed games; the manual source allows custom paths.");
        GamesTitle = Pick("检测到的游戏", "Detected games");
        GamesSubtitle = Pick("从所选来源中选择检测到的游戏。", "Choose a discovered game from the selected source.");
        ContentTitle = Pick("同步内容", "Content to sync");
        ContentSubtitle = Pick("点击条目可切换启用状态。", "Tap an item to toggle whether it is synchronized.");
        TargetsTitle = Pick("可用目标", "Available targets");
        TargetsSubtitle = Pick("目标在“同步目标”页管理，这里选择此游戏要绑定的目标。", "Targets are managed on the Sync Targets page; pick the one this game binds to.");

        BuildSources();
        SelectedSourceKey = null;
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
    public IReadOnlyList<WizardStepItem> Steps { get; }
    public bool IsChinese => _isChinese;

    public string? SelectedSourceKey { get; private set; }
    public Guid? SelectedTargetId { get; private set; }
    public Guid? LastCreatedInstanceId { get; private set; }

    public string CurrentStepTitle => CurrentStep switch
    {
        1 => Pick("第 1 步 · 选择来源", "Step 1 · Select Source"),
        2 => Pick("第 2 步 · 选择游戏", "Step 2 · Select Game"),
        3 => Pick("第 3 步 · 内容项", "Step 3 · Content Items"),
        4 => Pick("第 4 步 · 检查路径", "Step 4 · Review Paths"),
        5 => Pick("第 5 步 · 绑定目标", "Step 5 · Bind Target"),
        6 => Pick("第 6 步 · 完成", "Step 6 · Finish"),
        _ => Pick("第 1 步 · 选择来源", "Step 1 · Select Source"),
    };

    public string CurrentStepDescription => CurrentStep switch
    {
        1 => Pick("选择最适合要添加游戏的来源。Steam/Epic 会扫描本机安装目录；手动来源支持任意游戏。", "Choose the source that matches the game. Steam/Epic scan local install directories; the manual source supports any game."),
        2 => ShowNoResultsState
            ? Pick("当前来源下没有匹配的检测结果。可以返回切换来源，或改用手动配置。", "No matching results were found under the current source. Go back to switch source, or use manual setup.")
            : Pick("从所选来源中选择一个检测到的游戏。系统会优先使用稳定标识匹配内容定义，其次回退到名称级匹配。", "Choose a discovered game. The system prefers stable identifiers when matching content definitions, then falls back to name-level matching."),
        3 => Pick("选择此配置应同步哪些存档与配置内容。点击条目切换启用状态。", "Choose which save and configuration items should be synchronized. Tap an item to toggle it."),
        4 => Pick("在绑定目标前验证路径模板与解析结果。阻断错误会阻止创建，警告会允许继续但需要确认。", "Validate path templates and resolved results before binding a target. Blocking errors stop creation; warnings allow you to continue with caution."),
        5 => Pick("选择此游戏配置应同步到哪个目标。", "Choose the sync target that should receive this game profile."),
        6 => Pick("检查最终摘要、匹配依据和路径预检结果，确认后创建游戏配置。", "Review the summary, match basis, and path validation results, then create the game profile."),
        _ => string.Empty,
    };

    public string CurrentStepFooter => CurrentStep switch
    {
        1 => Pick("共 6 步中的第 1 步 · 选择来源后继续。", "Step 1 of 6 · Continue after choosing a source."),
        2 => ShowNoResultsState
            ? Pick("共 6 步中的第 2 步 · 返回上一步或切换到手动配置。", "Step 2 of 6 · Go back or switch to manual setup.")
            : Pick("共 6 步中的第 2 步 · 继续前确认检测到的游戏。", "Step 2 of 6 · Confirm the detected game before continuing."),
        3 => Pick("共 6 步中的第 3 步 · 至少保留一个启用的内容项。", "Step 3 of 6 · Keep at least one content item enabled."),
        4 => Pick("共 6 步中的第 4 步 · 绑定前验证所有解析路径。", "Step 4 of 6 · Verify all resolved paths before binding."),
        5 => Pick("共 6 步中的第 5 步 · 选择主要目标。", "Step 5 of 6 · Pick the primary destination."),
        6 => Pick("共 6 步中的第 6 步 · 已准备创建配置。", "Step 6 of 6 · Ready to create the profile."),
        _ => string.Empty,
    };

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsStep6 => CurrentStep == 6;
    public bool CanGoBack => CurrentStep > 1;
    public string NextButtonText => CurrentStep == 6 ? Pick("创建配置", "Create Profile") : Pick("下一步", "Next Step");
    public bool IsManualSourceSelected => string.Equals(SelectedSourceKey, ManualSourceKey, StringComparison.OrdinalIgnoreCase);

    partial void OnCurrentStepChanged(int value)
    {
        if (value != 2)
        {
            ShowNoResultsState = false;
        }

        OnPropertyChanged(nameof(CurrentStepTitle));
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
                break;
            case 2:
                SelectGame(option.Key);
                break;
            case 3:
                ToggleContentItem(option.Key);
                break;
            case 5:
                SelectedTargetId = Guid.TryParse(option.Key, out var targetId) ? targetId : null;
                Targets = Targets.Select(candidate => candidate with { IsSelected = candidate.Key == option.Key }).ToArray();
                break;
        }
    }

    public string? ValidateCurrentStep()
    {
        return CurrentStep switch
        {
            1 when SelectedSourceKey is null => Pick("请先选择一个来源。", "Choose a source first."),
            2 when _selectedGame is null || _selectedMatch is null => Pick("请先选择一个游戏。", "Choose a game first."),
            3 when _selectedContentIds.Count == 0 => Pick("至少保留一个启用的内容项。", "Keep at least one content item enabled."),
            4 when _pathValidationResults.Any(result => result.Severity == PathValidationSeverity.Blocking) => Pick("仍存在阻断性路径错误，请先修复后再继续。", "Blocking path validation errors remain. Fix them before continuing."),
            5 when SelectedTargetId is null => Pick("请先选择一个同步目标。", "Choose a sync target first."),
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
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    public void SwitchToManualSetup()
    {
        ShowNoResultsState = false;
        SelectedSourceKey = ManualSourceKey;
        Sources = Sources.Select(candidate => candidate with { IsSelected = candidate.Key == ManualSourceKey }).ToArray();
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
                    return new WizardOption(game.GameId, game.DisplayName, DescribeMatchSummary(match));
                })
                .ToArray();

            ShowNoResultsState = Games.Count == 0;
            _selectedGame = null;
            _selectedMatch = null;
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

    public void SetManualGame(string displayName, string savePath)
    {
        var manualGame = new DiscoveredGame
        {
            GameId = $"custom-{Slugify(displayName)}",
            DisplayName = displayName.Trim(),
            SourceProviderId = "custom",
            InstallDirectory = null,
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };

        _discoveredGames = [manualGame];
        _selectedGame = manualGame;
        _selectedMatch = new GameDefinitionMatch(
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
        _selectedContentIds.Clear();
        _pathValidationResults = [];

        Games = [new WizardOption(manualGame.GameId, manualGame.DisplayName, Pick("手动配置 · 1 个内容项", "Manual setup · 1 content item"), true)];
        ShowNoResultsState = false;
        BuildGameDetails();
    }

    public async Task<string?> CreateProfileAsync(CancellationToken cancellationToken = default)
    {
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
            var isUserDefinition = _selectedMatch.Origin != MatchOrigin.ExistingDefinition;
            if (isUserDefinition)
            {
                WriteUserDefinition(_selectedMatch.Definition);
            }

            var instance = new GameInstance
            {
                GameId = _selectedMatch.Definition.GameId,
                DisplayName = _selectedGame.DisplayName,
                SourceProviderId = _selectedGame.SourceProviderId,
                InstallDirectory = _selectedGame.InstallDirectory,
                PlatformInstanceId = _selectedGame.PlatformGameId,
                Variables = _selectedGame.Variables,
            };
            await _gameInstanceRepository.UpsertAsync(instance, cancellationToken);

            var bindingSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in target.Settings)
            {
                bindingSettings[pair.Key] = pair.Value;
            }

            bindingSettings["targetId"] = target.Id.ToString("D");
            bindingSettings["targetName"] = target.Name;
            bindingSettings["matchConfidence"] = _selectedMatch.Confidence.ToString();
            bindingSettings["matchReason"] = _selectedMatch.Reason;

            var binding = new StorageBinding
            {
                GameInstanceId = instance.Id,
                StorageProviderId = target.ProviderId,
                RemoteNamespace = Slugify(_selectedGame.DisplayName),
                Settings = bindingSettings,
                ContentSelections = _selectedMatch.Definition.ContentItems
                    .Select(item => new ContentSelection
                    {
                        ContentId = item.ContentId,
                        IsEnabled = _selectedContentIds.Contains(item.ContentId),
                    })
                    .ToArray(),
            };
            await _storageBindingRepository.UpsertAsync(binding, cancellationToken);
            LastCreatedInstanceId = instance.Id;

            if (StartFirstSync)
            {
                await _syncEngine.QueueAsync(new SyncJob
                {
                    GameInstanceId = instance.Id,
                    Direction = SyncDirection.Upload,
                }, cancellationToken);
            }

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
            options.Add(new WizardOption(provider.ProviderId, provider.DisplayName, summary));
        }

        options.Add(new WizardOption(
            ManualSourceKey,
            Pick("手动 / 自定义", "Manual / Custom"),
            Pick("手动输入游戏名称与存档目录，适用于任何游戏或便携安装", "Enter the game name and save folder manually; works for any game or portable install")));

        Sources = options;
    }

    private void SelectGame(string gameId)
    {
        _selectedGame = _discoveredGames.FirstOrDefault(game =>
            string.Equals(game.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        if (_selectedGame is null)
        {
            return;
        }

        if (!IsManualSourceSelected)
        {
            _selectedMatch = _matchService.Match(_selectedGame, _definitions, CreateFallbackDefinition);
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
            .Select(item => new WizardOption(
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
            _selectedGame.Variables);

        PathReview = _pathValidationResults
            .Select(result => new UiPropertyRow(
                $"{result.ContentId} · {result.Template}",
                $"{result.ResolvedPath} · {DescribeValidationSeverity(result.Severity)} · {DescribeValidationMessage(result.MessageCode)}"))
            .ToArray();
    }

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
        var configs = _syncTargetStore.List();
        SelectedTargetId ??= _syncTargetStore.GetDefault()?.Id;

        Targets = configs
            .Select(config => new WizardOption(
                config.Id.ToString("D"),
                config.Name,
                config.ProviderId == "webdav"
                    ? $"WebDAV · {config.Settings.GetValueOrDefault("baseUrl", "--")}"
                    : Pick($"本地文件夹 · {config.Settings.GetValueOrDefault("rootPath", "--")}", $"Local folder · {config.Settings.GetValueOrDefault("rootPath", "--")}"),
                config.Id == SelectedTargetId))
            .ToArray();
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
            new(Pick("远端命名空间", "Remote Namespace"), _selectedGame is null ? "--" : Slugify(_selectedGame.DisplayName)),
        ];
    }

    private GameContentDefinition CreateFallbackDefinition(DiscoveredGame game)
    {
        var pathTemplates = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.InstallDirectory))
        {
            pathTemplates.Add(game.InstallDirectory.Replace('\\', '/'));
        }

        return new GameContentDefinition
        {
            GameId = $"custom-{Slugify(game.DisplayName)}",
            DisplayName = game.DisplayName,
            Description = Pick("未匹配社区清单时生成的回退定义。", "Fallback definition generated when no manifest match exists."),
            ContentItems =
            [
                new ContentItem
                {
                    ContentId = "saves",
                    Category = ContentCategory.Save,
                    PathTemplates = pathTemplates,
                    DefaultEnabled = true,
                },
            ],
        };
    }

    private void WriteUserDefinition(GameContentDefinition definition)
    {
        var directory = _appPathService.GetDefinitionsDirectory();
        Directory.CreateDirectory(directory);

        var builder = new StringBuilder();
        builder.AppendLine($"gameId: \"{definition.GameId}\"");
        builder.AppendLine($"displayName: \"{definition.DisplayName}\"");
        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            builder.AppendLine($"description: \"{definition.Description}\"");
        }

        builder.AppendLine("contentItems:");
        foreach (var item in definition.ContentItems)
        {
            builder.AppendLine($"  - contentId: \"{item.ContentId}\"");
            builder.AppendLine($"    category: {item.Category}");
            builder.AppendLine($"    defaultEnabled: {(item.DefaultEnabled ? "true" : "false")}");
            builder.AppendLine("    pathTemplates:");
            foreach (var template in item.PathTemplates)
            {
                builder.AppendLine($"      - \"{template.Replace("\\", "/")}\"");
            }
        }

        var path = Path.Combine(directory, $"{Slugify(definition.GameId)}.yaml");
        File.WriteAllText(path, builder.ToString());
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

    private static string Slugify(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        return builder.Length == 0 ? "game" : builder.ToString().TrimEnd('-');
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record WizardStepItem(int Index, string Title, string Summary);

public sealed record WizardOption(string Key, string Title, string Summary, bool IsSelected = false)
{
    public Microsoft.UI.Xaml.Media.Brush BorderBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources[IsSelected ? "AppPrimaryBrush" : "AppDividerBrush"];
}
