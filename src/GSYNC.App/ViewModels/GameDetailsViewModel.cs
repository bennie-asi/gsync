using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
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

public sealed record ConflictNavigationContext(Guid InstanceId, string GameName, SyncComparison Comparison, Guid? TargetId = null);

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IManifestService _manifestService;
    private readonly ISyncEngine _syncEngine;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;
    private readonly IUserDefinitionStore _userDefinitionStore;
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;
    private readonly UserVariablesStore _userVariablesStore;
    private readonly SyncTargetStore _syncTargetStore;
    private readonly ISyncBindingUpdater _bindingUpdater;

    private GameInstance? _instance;
    private StorageBinding? _binding;
    private GameContentDefinition? _definition;
    private readonly object _comparisonGate = new();
    private readonly List<Task> _comparisonTasks = [];
    private CancellationTokenSource? _comparisonCancellation;
    private long _loadVersion;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _coverUri = string.Empty;

    [ObservableProperty]
    private string _localArtworkPath = string.Empty;

    [ObservableProperty]
    private string _programPath = string.Empty;

    [ObservableProperty]
    private string _activeTarget = string.Empty;

    [ObservableProperty]
    private string _appIdText = string.Empty;

    [ObservableProperty]
    private string _studioText = string.Empty;

    [ObservableProperty]
    private string _lastPlayedText = string.Empty;

    [ObservableProperty]
    private string _overallStatusText = string.Empty;

    [ObservableProperty]
    private string _overallStatusVariant = string.Empty;

    [ObservableProperty]
    private string _totalSizeText = string.Empty;

    [ObservableProperty]
    private string _tableFooterText = string.Empty;

    [ObservableProperty]
    private string _propertiesTitle = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _overviewMetrics = [];

    [ObservableProperty]
    private IReadOnlyList<ContentItemRow> _contentItems = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _selectedItemProperties = [];

    [ObservableProperty]
    private IReadOnlyList<UiActivityItem> _recentActivityItems = [];

    [ObservableProperty]
    private IReadOnlyList<SnapshotRow> _snapshots = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInspectorVisible;

    public GameDetailsViewModel(
        ILocalizationService localizationService,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IManifestService manifestService,
        ISyncEngine syncEngine,
        IEnumerable<ISourceProvider> sourceProviders,
        IUserDefinitionStore userDefinitionStore,
        PathResolver pathResolver,
        SystemVariableProvider systemVariableProvider,
        UserVariablesStore userVariablesStore,
        SyncTargetStore syncTargetStore,
        ISyncBindingUpdater bindingUpdater)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _manifestService = manifestService;
        _syncEngine = syncEngine;
        _sourceProviders = sourceProviders;
        _userDefinitionStore = userDefinitionStore;
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;
        _userVariablesStore = userVariablesStore;
        _syncTargetStore = syncTargetStore;
        _bindingUpdater = bindingUpdater;

        Title = Pick("游戏详情", "Game Details");
        PageSubtitle = Pick("查看存档状态、同步内容与恢复记录。", "Review save status, synchronized content, and recovery history.");
        PrimaryActionLabel = Pick("快速操作", "Quick Actions");
        UploadText = Pick("上传", "Upload");
        DownloadText = Pick("下载", "Download");
        CompareText = Pick("比较存档", "Compare saves");
        RestoreText = Pick("恢复备份", "Restore Backup");
        TableSectionTitle = Pick("同步内容", "Sync content");
        TableSectionSubtitle = Pick("本地存档路径与同步配置。", "Local save paths and sync configuration.");
        AddItemText = Pick("添加条目", "Add Item");
        PropertiesTitle = Pick("检查器", "Inspector");
        PropertiesSubtitle = Pick("所选内容项的路径模板、策略与可移植性检查结果。", "Path template, policy, and portability checks for the selected content item.");
        OpenFolderText = Pick("打开目录", "Open Folder");
        TestPathText = Pick("测试路径", "Test Path");
        HistorySectionTitle = Pick("历史与快照", "History & Snapshots");
        HistorySectionSubtitle = Pick("查看最近活动与可恢复的快照。", "Review recent activity and recoverable snapshots.");
        HistoryTabText = Pick("最近同步历史", "Recent Sync History");
        SnapshotsTabText = Pick("快照", "Snapshots");
        HistoryFeedTitle = Pick("最近活动", "Recent Activity");
        SnapshotFeedTitle = Pick("可恢复快照", "Recoverable Snapshots");
        OverallStatusLabel = Pick("整体状态", "Overall Status");
        TotalSizeLabel = Pick("本地大小", "Local Size");
        ActiveTargetLabel = Pick("当前目标", "Active Target");
        OverviewMetricsTitle = Pick("上下文摘要", "Context Summary");
        InspectorStatusTitle = Pick("检查状态", "Inspection Status");
        InspectorStatusMessage = Pick("在同步前先核对所选内容项的解析路径与启用状态。", "Validate the resolved path and enabled state of the selected content item before syncing.");

        TableTitle = string.Empty;
        TableSubtitle = string.Empty;
        _overallStatusText = Pick("未同步", "Not synced");
        _activeTarget = Pick("未绑定", "Unbound");

        AppIdText = string.Empty;
        StudioText = string.Empty;
        LastPlayedText = string.Empty;
        OverallStatusVariant = "pending";
        TotalSizeText = "--";
        TableFooterText = string.Empty;
        OverviewMetrics = [];
        ContentItems = [];
        SelectedItemProperties = [];
        RecentActivityItems = [];
        Snapshots = [];
        IsInspectorVisible = false;
    }

    public string PageSubtitle { get; }
    public string GameMonogram
    {
        get
        {
            var text = new System.Globalization.StringInfo(Title.Trim());
            return text.LengthInTextElements == 0 ? "?" : text.SubstringByTextElements(0, Math.Min(2, text.LengthInTextElements)).ToUpperInvariant();
        }
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(GameMonogram));
    partial void OnProgramPathChanged(string value) => OnPropertyChanged(nameof(HasProgramPath));
    public bool HasProgramPath => !string.IsNullOrWhiteSpace(ProgramPath);
    public string PrimaryActionLabel { get; }
    public string UploadText { get; }
    public string DownloadText { get; }
    public string CompareText { get; }
    public string RestoreText { get; }
    public string TableSectionTitle { get; }
    public string TableSectionSubtitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string AddItemText { get; }
    public string EditGameText => Pick("编辑游戏", "Edit game");
    public string AutoPullTitle => Pick("安全自动拉取", "Safe automatic pull");
    public string ConfirmBaselineText => Pick("比较并确认共同版本", "Compare and confirm common version");
    public string AutoPullNote => Pick("请先关闭游戏。首次关联或发生冲突时，先手动处理并让两端文件一致，再确认共同版本。更改路径不会移动存档。", "Close the game first. On first connection or conflict, resolve differences manually until both sides match, then confirm the common version. Changing paths does not move saves.");
    public string SyncDestinationTitle => Pick("本机与同步目标", "This device and sync target");
    public string LocalDeviceLabel => Pick($"本机 · {Environment.MachineName}", $"This device · {Environment.MachineName}");
    public string TargetLabel => Pick("同步目标", "Sync target");
    public string LocalPathSummary
    {
        get
        {
            var paths = ContentItems.Where(item => item.Enabled).Select(item => item.ResolvedPath).ToArray();
            return paths.Length switch
            {
                0 => Pick("没有启用的存档路径", "No save paths are enabled"),
                1 => paths[0],
                _ => paths[0] + Pick($"\n另有 {paths.Length - 1} 个路径", $"\n{paths.Length - 1} more paths"),
            };
        }
    }
    partial void OnContentItemsChanged(IReadOnlyList<ContentItemRow> value) => OnPropertyChanged(nameof(LocalPathSummary));
    public string ChangeDestinationText => Pick("修改同步位置", "Change destination");
    public string RemoteNamespaceText => Pick("远端目录", "Remote directory");
    public string SelectTargetText => Pick("同步目标", "Sync target");
    public string SaveDestinationText => Pick("保存同步位置", "Save destination");
    public string ManageTargetsText => Pick("管理同步目标", "Manage sync targets");
    public string CancelDestinationText => Pick("取消", "Cancel");
    public string DestinationHelp => Pick("切换只修改此游戏的绑定，不会移动或删除本地及原远端存档。保存后请先比较新位置，再确认自动拉取的共同版本。", "Changing the destination only updates this game's binding; it does not move or delete local or old remote saves. Compare the new location and confirm a common version before automatic pull.");
    public string ShowInspectorText => Pick("查看详情", "Show details");
    public string CloseDetailsText => Pick("关闭详情", "Close details");
    public string RestoreSnapshotText => Pick("恢复此快照", "Restore snapshot");
    public bool HasSnapshots => Snapshots.Count > 0;
    public bool HasRecentActivity => RecentActivityItems.Count > 0;
    public string EmptySnapshotsText => Pick("还没有可恢复的快照。", "There are no snapshots to restore yet.");
    public string EmptyActivityText => Pick("还没有同步活动。", "There is no sync activity yet.");

    partial void OnSnapshotsChanged(IReadOnlyList<SnapshotRow> value) => OnPropertyChanged(nameof(HasSnapshots));
    partial void OnRecentActivityItemsChanged(IReadOnlyList<UiActivityItem> value) => OnPropertyChanged(nameof(HasRecentActivity));
    public bool IsChinese => _isChinese;
    public string PropertiesSubtitle { get; }
    public string OpenFolderText { get; }
    public string TestPathText { get; }
    public string HistorySectionTitle { get; }
    public string HistorySectionSubtitle { get; }
    public string HistoryTabText { get; }
    public string SnapshotsTabText { get; }
    public string HistoryFeedTitle { get; }
    public string SnapshotFeedTitle { get; }
    public string OverallStatusLabel { get; }
    public string TotalSizeLabel { get; }
    public string ActiveTargetLabel { get; }
    public string OverviewMetricsTitle { get; }
    public string InspectorStatusTitle { get; }
    public string InspectorStatusMessage { get; }

    public Guid? InstanceId => _instance?.Id;

    public string MoreActionsText => Pick("更多", "More");
    public string RemoveGameText => Pick("从游戏库移除", "Remove from library");

    public async Task<string?> RemoveGameAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (_instance is not { } instance) return Pick("游戏尚未加载。", "The game is not loaded.");
        IsBusy = true;
        try
        {
            await StopStatusCheckAsync();
            await _bindingUpdater.RunGameRemovalAsync(instance.Id,
                token => _gameInstanceRepository.DeleteAsync(instance.Id, token), cancellationToken);
            _instance = null;
            _binding = null;
            return null;
        }
        catch (SyncBindingBusyException)
        {
            return Pick("该游戏仍有排队或运行中的同步任务，请结束后再移除。", "Wait for this game's queued or running sync tasks before removing it.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to remove game {InstanceId} from the library.", instance.Id);
            return Pick("无法移除游戏：", "Could not remove the game: ") + exception.Message;
        }
        finally { IsBusy = false; }
    }

    public GameInstance? EditableInstance => _instance;
    public Guid? CurrentTargetId => _binding is not null && _binding.Settings.TryGetValue("targetId", out var id) && Guid.TryParse(id, out var parsed) ? parsed : null;
    public string CurrentRemoteNamespace => _binding?.RemoteNamespace ?? (_instance is null ? string.Empty : GameIdentity.StorageNamespace(_instance.GameId));
    public string CurrentTargetLocation
    {
        get
        {
            var settings = CurrentTargetId is { } id ? _syncTargetStore.Get(id)?.Settings : _binding?.Settings;
            return settings?.GetValueOrDefault("baseUrl") ?? settings?.GetValueOrDefault("rootPath") ?? string.Empty;
        }
    }

    public IReadOnlyList<GameTargetOption> GetTargetOptions() => _syncTargetStore.List()
        .Select(target => new GameTargetOption(target.Id, target.Name, target.ProviderId,
            target.Settings.GetValueOrDefault("baseUrl", target.Settings.GetValueOrDefault("rootPath", string.Empty))))
        .ToArray();

    public string LinkRemoteText => Pick("关联已有远端存档…", "Link existing remote saves…");
    public IReadOnlyList<LocalContentRoot> GetLinkableRoots() => _definition is null ? [] :
        RemoteContentLayout.LocalRoots(_definition, _binding?.ContentSelections ?? Array.Empty<ContentSelection>()).Select(root =>
        {
            var row = ContentItems.FirstOrDefault(item => string.Equals(item.Name, root.ContentId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.PathTemplate.Replace('\\', '/'), root.Template, StringComparison.OrdinalIgnoreCase));
            return row is null ? root : root with { LocalPath = row.ResolvedPath };
        }).ToArray();

    public ContentItemRow? SelectedContentItem { get; private set; }

    public async Task<string?> UpdateDestinationAsync(Guid targetId, string remoteNamespace, CancellationToken cancellationToken = default, IReadOnlyList<ContentRootLink>? links = null)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (_instance is null) return Pick("游戏实例尚未加载。", "Game instance is not loaded.");
        var target = _syncTargetStore.Get(targetId);
        if (target is null) return Pick("所选同步目标已不存在，请重新选择。", "The selected target no longer exists. Choose another one.");
        string normalized;
        try
        {
            normalized = SafePath.ValidateRelative(remoteNamespace.Trim()).TrimEnd('/');
            if (normalized.Split('/').Any(segment => string.IsNullOrEmpty(segment) ||
                segment.Equals("__gsync_meta", StringComparison.OrdinalIgnoreCase) || segment.Equals("__gsync_versions", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return Pick("远端目录必须是相对路径，不能包含空目录、.. 或保留目录。", "Enter a relative remote directory without empty segments, .., or reserved directories.");
        }
        var destinationChanged = CurrentTargetId != targetId || !string.Equals(CurrentRemoteNamespace, normalized, StringComparison.Ordinal) ||
            !string.Equals(_binding?.StorageProviderId, target.ProviderId, StringComparison.OrdinalIgnoreCase);
        if (!destinationChanged && links is null) return null;

        IsBusy = true;
        StopStatusCheck();
        try
        {
            var existing = _binding;
            var settings = new Dictionary<string, string>(existing?.Settings ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "targetId", "targetName", "rootPath", "baseUrl", "credentialId", "username", "password", "token", "accessToken", "refreshToken", "apiKey", "clientSecret", "__gsyncTargetRevision" }) settings.Remove(key);
            foreach (var pair in target.Settings) settings[pair.Key] = pair.Value;
            settings["targetId"] = target.Id.ToString("D");
            settings["targetName"] = target.Name;
            settings["__gsyncTargetRevision"] = Guid.NewGuid().ToString("N");
            var selections = existing?.ContentSelections ?? Array.Empty<ContentSelection>();
            if (links is not null)
            {
                if (_definition is null) throw new InvalidOperationException("The game definition is unavailable.");
                selections = RemoteContentLayout.ApplyLinks(_definition, selections, links);
                settings["__gsyncRootMappingTargetId"] = target.Id.ToString("D");
            }
            else if (destinationChanged) settings.Remove("__gsyncRootMappingTargetId");
            var updatedAt = DateTimeOffset.UtcNow;
            if (existing is not null && updatedAt <= existing.UpdatedAtUtc) updatedAt = existing.UpdatedAtUtc.AddTicks(1);
            var updated = new StorageBinding
            {
                Id = existing?.Id ?? Guid.NewGuid(), GameInstanceId = _instance.Id, StorageProviderId = target.ProviderId,
                RemoteNamespace = normalized, Settings = settings, ContentSelections = selections,
                CreatedAtUtc = existing?.CreatedAtUtc ?? updatedAt, UpdatedAtUtc = updatedAt,
            };
            await _bindingUpdater.UpdateBindingAsync(updated, existing?.Id, existing?.UpdatedAtUtc, cancellationToken);
            await LoadAsync(updated.GameInstanceId, cancellationToken);
            OnPropertyChanged(nameof(CurrentTargetId));
            OnPropertyChanged(nameof(CurrentRemoteNamespace));
            OnPropertyChanged(nameof(CurrentTargetLocation));
            return null;
        }
        catch (SyncBindingBusyException)
        {
            if (_instance is not null) await LoadAsync(_instance.Id, cancellationToken);
            return Pick("该游戏仍有排队或运行中的同步任务，请结束后再切换。", "Wait for this game's queued or running sync tasks before changing the destination.");
        }
        catch (SyncBindingChangedException)
        {
            if (_instance is not null) await LoadAsync(_instance.Id, cancellationToken);
            return Pick("同步位置已发生变化，请重新打开游戏详情后再试。", "The sync destination changed. Reopen the game details and try again.");
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to update game sync destination.");
            return exception.Message;
        }
        finally { IsBusy = false; }
    }

    public async Task<string?> UpdateGameAsync(string name, string? installDirectory, IReadOnlyDictionary<string, string> variables)
    {
        if (_instance is null || string.IsNullOrWhiteSpace(name))
            return Pick("请填写游戏名称。", "Enter a game name.");
        try
        {
            var existing = await _gameInstanceRepository.GetAsync(_instance.Id, CancellationToken.None);
            if (existing is null) return Pick("游戏已不存在。", "The game no longer exists.");
            await _gameInstanceRepository.UpsertAsync(new GameInstance
            {
                Id = existing.Id, GameId = existing.GameId, DisplayName = name.Trim(),
                SourceProviderId = existing.SourceProviderId, PlatformInstanceId = existing.PlatformInstanceId,
                InstallDirectory = string.IsNullOrWhiteSpace(installDirectory) ? null : installDirectory.Trim(),
                CreatedAtUtc = existing.CreatedAtUtc, UpdatedAtUtc = DateTimeOffset.UtcNow, Variables = variables,
            }, CancellationToken.None);
            await LoadAsync(existing.Id);
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to update game settings.");
            return exception.Message;
        }
    }

    public async Task<string?> UpdateContentPathAsync(ContentItemRow row, string path)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (string.IsNullOrWhiteSpace(path)) return Pick("请填写存档路径。", "Enter a save path.");
        if (_definition is null || _binding is null) return Pick("请先为游戏设置同步目标。", "Set a sync target for the game first.");
        IsBusy = true;
        try
        {
            var definition = await _manifestService.GetDefinitionAsync(_definition.GameId, CancellationToken.None)
                ?? throw new InvalidOperationException("The game definition no longer exists.");
            var item = definition.ContentItems.FirstOrDefault(x => string.Equals(x.ContentId, row.Name, StringComparison.OrdinalIgnoreCase));
            if (item is null || !item.PathTemplates.Contains(row.PathTemplate))
                return Pick("内容项已发生变化，请刷新后重试。", "The content item changed. Refresh and try again.");
            var previous = _binding;
            var selections = definition.ContentItems.Select(content =>
            {
                var old = previous.ContentSelections.FirstOrDefault(selection =>
                    string.Equals(selection.ContentId, content.ContentId, StringComparison.OrdinalIgnoreCase));
                var paths = new Dictionary<string, string>(old?.PathOverrides ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
                if (string.Equals(content.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = path.Trim().Replace('\\', '/');
                    if (string.Equals(normalized, row.PathTemplate.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) paths.Remove(row.PathTemplate);
                    else paths[row.PathTemplate] = normalized;
                }
                return new ContentSelection { ContentId = content.ContentId,
                    IsEnabled = previous.ContentSelections.Count == 0 ? content.DefaultEnabled : old?.IsEnabled ?? false,
                    PathOverrides = paths, RemoteRootOverrides = old?.RemoteRootOverrides ?? new Dictionary<string, string>(), RemoteSubdirectoryOverrides = old?.RemoteSubdirectoryOverrides ?? new Dictionary<string, string>(), DisabledPathTemplates = old?.DisabledPathTemplates ?? Array.Empty<string>(),
                    RemovedPathTemplates = old?.RemovedPathTemplates ?? Array.Empty<string>() };
            }).ToArray();
            var updatedAt = DateTimeOffset.UtcNow;
            if (updatedAt <= previous.UpdatedAtUtc) updatedAt = previous.UpdatedAtUtc.AddTicks(1);
            var updated = new StorageBinding
            {
                Id = previous.Id, GameInstanceId = previous.GameInstanceId, StorageProviderId = previous.StorageProviderId,
                RemoteNamespace = previous.RemoteNamespace, Settings = previous.Settings, CreatedAtUtc = previous.CreatedAtUtc,
                UpdatedAtUtc = updatedAt, ContentSelections = selections,
            };
            await _bindingUpdater.UpdateBindingAsync(updated, previous.Id, previous.UpdatedAtUtc, CancellationToken.None);
            _binding = updated;
            BuildContentItems();
            HideInspector();
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to update content path.");
            return exception.Message;
        }
        finally { IsBusy = false; }
    }

    public async Task<string?> UpdateContentSelectionAsync(ContentItemRow row, bool? enabled, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (_definition is null || _binding is null) return Pick("请先为游戏设置同步目标。", "Set a sync target for the game first.");
        IsBusy = true;
        try
        {
            var definition = await _manifestService.GetDefinitionAsync(_definition.GameId, cancellationToken)
                ?? throw new InvalidOperationException("The game definition no longer exists.");
            var item = definition.ContentItems.FirstOrDefault(value => string.Equals(value.ContentId, row.Name, StringComparison.OrdinalIgnoreCase));
            if (item is null || !item.PathTemplates.Any(value => string.Equals(value.Replace('\\', '/'), row.PathTemplate.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                return Pick("内容项已发生变化，请刷新后重试。", "The content item changed. Refresh and try again.");
            var previous = _binding;
            var selections = definition.ContentItems.Select(content =>
            {
                var old = previous.ContentSelections.FirstOrDefault(value => string.Equals(value.ContentId, content.ContentId, StringComparison.OrdinalIgnoreCase));
                var groupEnabled = previous.ContentSelections.Count == 0 ? content.DefaultEnabled : old?.IsEnabled ?? false;
                var disabled = new HashSet<string>(old?.DisabledPathTemplates ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                var removed = new HashSet<string>(old?.RemovedPathTemplates ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                if (string.Equals(content.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))
                {
                    if (old?.IsTemplateRemoved(row.PathTemplate) == true) throw new InvalidOperationException("The content item was already removed. Refresh and try again.");
                    if (enabled is null) removed.Add(row.PathTemplate);
                    else
                    {
                        if (!groupEnabled) foreach (var template in content.PathTemplates) disabled.Add(template);
                        groupEnabled = true;
                        if (enabled.Value) disabled.Remove(row.PathTemplate); else disabled.Add(row.PathTemplate);
                    }
                }
                return new ContentSelection { ContentId = content.ContentId, IsEnabled = groupEnabled,
                    PathOverrides = old?.PathOverrides ?? new Dictionary<string, string>(), RemoteRootOverrides = old?.RemoteRootOverrides ?? new Dictionary<string, string>(), RemoteSubdirectoryOverrides = old?.RemoteSubdirectoryOverrides ?? new Dictionary<string, string>(), DisabledPathTemplates = disabled.ToArray(), RemovedPathTemplates = removed.ToArray() };
            }).ToArray();
            var updatedAt = DateTimeOffset.UtcNow;
            if (updatedAt <= previous.UpdatedAtUtc) updatedAt = previous.UpdatedAtUtc.AddTicks(1);
            var updated = new StorageBinding { Id = previous.Id, GameInstanceId = previous.GameInstanceId,
                StorageProviderId = previous.StorageProviderId, RemoteNamespace = previous.RemoteNamespace, Settings = previous.Settings,
                CreatedAtUtc = previous.CreatedAtUtc, UpdatedAtUtc = updatedAt, ContentSelections = selections };
            await _bindingUpdater.UpdateBindingAsync(updated, previous.Id, previous.UpdatedAtUtc, cancellationToken);
            await LoadAsync(previous.GameInstanceId, cancellationToken);
            return null;
        }
        catch (SyncBindingBusyException) { return Pick("该游戏仍有同步任务，请结束后再修改。", "Wait for this game's sync tasks to finish before changing its content."); }
        catch (SyncBindingChangedException) { return Pick("配置已发生变化，请刷新后重试。", "The configuration changed. Refresh and try again."); }
        catch (Exception exception) { Log.Warning(exception, "Failed to update content selection."); return exception.Message; }
        finally { IsBusy = false; }
    }

    public async Task LoadAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        StopStatusCheck();
        var loadVersion = Interlocked.Read(ref _loadVersion);
        try
        {
            var instance = await _gameInstanceRepository.GetAsync(instanceId, cancellationToken);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            _instance = instance;
            if (_instance is null)
            {
                Title = Pick("未找到游戏实例", "Game instance not found");
                return;
            }

            Title = _instance.DisplayName;
            AppIdText = _instance.PlatformInstanceId ?? _instance.GameId;
            StudioText = DescribeSource(_instance.SourceProviderId);
            LastPlayedText = Pick(
                $"添加于 {UiFormat.RelativeTime(_instance.CreatedAtUtc, true)}",
                $"Added {UiFormat.RelativeTime(_instance.CreatedAtUtc, false)}");

            var bindings = await _storageBindingRepository.ListByGameInstanceAsync(instanceId, cancellationToken);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            _binding = bindings.FirstOrDefault();
            ActiveTarget = _binding is null ? Pick("未绑定", "Unbound") :
                CurrentTargetId is { } targetId
                    ? _syncTargetStore.Get(targetId)?.Name ?? Pick("目标已移除，请重新选择", "Target removed; choose another")
                    : _binding.Settings.GetValueOrDefault("targetName", _binding.StorageProviderId);
            OnPropertyChanged(nameof(CurrentTargetId));
            OnPropertyChanged(nameof(CurrentRemoteNamespace));
            OnPropertyChanged(nameof(CurrentTargetLocation));

            var definition = await _manifestService.GetDefinitionAsync(_instance.GameId, cancellationToken);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            _definition = definition;
            var artwork = GameArtworkResolver.Resolve(_instance, definition);
            if (artwork.CoverUri is null)
            {
                var catalog = await Task.Run(() => _manifestService.GetDefinitionsAsync(cancellationToken), cancellationToken);
                if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
                var index = await Task.Run(() => GameArtworkResolver.BuildCatalogIndex(catalog), cancellationToken);
                if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
                artwork = GameArtworkResolver.ResolveWithIndex(_instance, definition, index);
            }
            CoverUri = artwork.CoverUri ?? string.Empty;
            LocalArtworkPath = artwork.LocalArtworkPath ?? string.Empty;
            ProgramPath = artwork.ProgramPath ?? string.Empty;

            var records = await _syncHistoryRepository.ListRecordsAsync(instanceId, cancellationToken);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            var lastRecord = records.FirstOrDefault();
            (OverallStatusText, OverallStatusVariant) = _binding is null
                ? (Pick("未绑定", "Unbound"), "disabled")
                : (Pick("检查中…", "Checking…"), "pending");

            OverviewMetrics =
            [
                new(Pick("平台", "Platform"), DescribeSource(_instance.SourceProviderId)),
                new(Pick("安装目录", "Install Directory"), _instance.InstallDirectory ?? "--"),
                new(Pick("上次同步", "Last Sync"), UiFormat.RelativeTime(lastRecord?.CompletedAtUtc ?? lastRecord?.StartedAtUtc, _isChinese)),
                new(Pick("同步记录数", "Sync Records"), records.Count.ToString()),
            ];

            BuildContentItems();

            var activityRecords = records.Take(8).ToArray();
            RecentActivityItems = activityRecords
                .Select((record, index) => new UiActivityItem(
                    $"{UiFormat.DirectionText(record, _isChinese)} · {UiFormat.StatusText(record.Status, _isChinese)}",
                    UiFormat.RecordMessage(record, _isChinese),
                    UiFormat.RelativeTime(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
                    UiFormat.StatusVariant(record.Status),
                    index == activityRecords.Length - 1))
                .ToArray();

            var snapshots = await _syncHistoryRepository.ListSnapshotsAsync(instanceId, cancellationToken);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            Snapshots = snapshots
                .Select((snapshot, index) => new SnapshotRow(
                    $"SN-{snapshot.Id.ToString("N")[..8]}",
                    index == 0 ? Pick("最新", "Latest") : string.Empty,
                    UiFormat.TimeOfRecord(snapshot.CreatedAtUtc, _isChinese),
                    snapshot.FileCount,
                    UiFormat.Bytes(snapshot.TotalBytes),
                    _isChinese)
                {
                    SnapshotId = snapshot.Id,
                })
                .ToArray();
            if (_binding is not null && loadVersion == Interlocked.Read(ref _loadVersion))
            {
                var comparisonCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                comparisonCancellation.CancelAfter(TimeSpan.FromSeconds(15));
                lock (_comparisonGate)
                {
                    if (loadVersion != Interlocked.Read(ref _loadVersion))
                    {
                        comparisonCancellation.Dispose();
                        return;
                    }
                    _comparisonCancellation = comparisonCancellation;
                    _comparisonTasks.RemoveAll(task => task.IsCompleted);
                    _comparisonTasks.Add(RefreshSyncStatusAsync(instanceId, loadVersion, comparisonCancellation));
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load game details for instance {InstanceId}.", instanceId);
        }
    }

    public void StopStatusCheck()
    {
        CancellationTokenSource? cancellation;
        lock (_comparisonGate)
        {
            Interlocked.Increment(ref _loadVersion);
            cancellation = Interlocked.Exchange(ref _comparisonCancellation, null);
        }
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public async Task StopStatusCheckAsync()
    {
        StopStatusCheck();
        Task[] pending;
        lock (_comparisonGate) pending = _comparisonTasks.ToArray();
        await Task.WhenAll(pending);
    }

    private async Task RefreshSyncStatusAsync(Guid instanceId, long loadVersion, CancellationTokenSource cancellation)
    {
        try
        {
            var comparison = await Task.Run(() => _syncEngine.CompareAsync(new SyncJob
            {
                GameInstanceId = instanceId,
                Direction = SyncDirection.Compare,
            }, cancellation.Token), cancellation.Token);
            if (loadVersion != Interlocked.Read(ref _loadVersion)) return;
            (OverallStatusText, OverallStatusVariant) = comparison.Entries.Count == 0
                ? (Pick("无文件", "No files"), "missing")
                : comparison.Entries.Any(entry => entry.ChangeKind != SyncChangeKind.Unchanged)
                    ? (Pick("存在差异", "Differences"), "conflict")
                    : (Pick("已同步", "Synced"), "synced");
        }
        catch (Exception exception)
        {
            if (loadVersion == Interlocked.Read(ref _loadVersion))
            {
                (OverallStatusText, OverallStatusVariant) = (Pick("无法比较", "Compare failed"), "disabled");
                if (exception is not OperationCanceledException) Log.Warning(exception, "Failed to compare game {InstanceId}.", instanceId);
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _comparisonCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    public void SelectContentItem(ContentItemRow row)
    {
        SelectedContentItem = row;
        IsInspectorVisible = true;
        PropertiesTitle = Pick($"检查器：{row.Name}", $"Inspector: {row.Name}");
        var properties = new List<UiPropertyRow>
        {
            new(Pick("条目名称", "Item Name"), row.Name),
            new(Pick("分类", "Category"), row.Category),
            new(Pick("路径模板", "Path Template"), row.PathTemplate),
            new(Pick("解析路径", "Resolved Path"), row.ResolvedPath),
            new(Pick("启用状态", "Enabled"), row.Enabled ? Pick("已启用", "Enabled") : Pick("已禁用", "Disabled")),
        };
        if (!string.IsNullOrWhiteSpace(row.Patterns))
        {
            properties.Add(new UiPropertyRow(Pick("包含/排除模式", "Include/Exclude Patterns"), row.Patterns));
        }

        SelectedItemProperties = properties;
    }

    public void HideInspector()
    {
        IsInspectorVisible = false;
    }

    public void ShowInspector()
    {
        IsInspectorVisible = true;
    }

    public async Task<string?> AddCustomContentItemAsync(string name, string pathTemplate, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(pathTemplate))
            return Pick("请填写条目名称和存档路径。", "Enter an item name and save path.");
        if (_definition is null || _instance is null)
        {
            return Pick("当前游戏没有可编辑的内容定义。", "No editable content definition is available for this game.");
        }

        IsBusy = true;
        try
        {
            var previous = _binding;
            await _bindingUpdater.RunConfigurationChangeAsync(_instance.Id, async token =>
            {
                var oldBinding = (await _storageBindingRepository.ListByGameInstanceAsync(_instance.Id, token)).FirstOrDefault();
                if (oldBinding?.Id != previous?.Id || oldBinding?.UpdatedAtUtc != previous?.UpdatedAtUtc)
                    throw new SyncBindingChangedException();
                var targetDefinition = await _manifestService.GetDefinitionAsync(_definition.GameId, token)
                    ?? throw new InvalidOperationException("The game definition no longer exists.");
                var updatedItems = targetDefinition.ContentItems.ToList();
                var existingIds = updatedItems.Select(item => item.ContentId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var baseId = SlugifyContentId(name);
                var contentId = baseId;
                for (var suffix = 2; existingIds.Contains(contentId); suffix++) contentId = baseId + "-" + suffix;
                updatedItems.Add(new ContentItem
                {
                    ContentId = contentId,
                    Category = ContentCategory.Extra,
                    PathTemplates = [pathTemplate.Trim().Replace('\\', '/')],
                    DefaultEnabled = true,
                    Description = name.Trim(),
                });

                targetDefinition = new GameContentDefinition
                {
                    GameId = targetDefinition.GameId,
                    DisplayName = targetDefinition.DisplayName,
                    Description = targetDefinition.Description,
                    SourceHints = targetDefinition.SourceHints,
                    ContentItems = updatedItems,
                };

                StorageBinding? updatedBinding = null;
                if (oldBinding is not null)
                {
                    var updatedAt = DateTimeOffset.UtcNow;
                    if (updatedAt <= oldBinding.UpdatedAtUtc) updatedAt = oldBinding.UpdatedAtUtc.AddTicks(1);
                    updatedBinding = new StorageBinding
                    {
                        Id = oldBinding.Id, GameInstanceId = oldBinding.GameInstanceId, StorageProviderId = oldBinding.StorageProviderId,
                        RemoteNamespace = oldBinding.RemoteNamespace, Settings = oldBinding.Settings, CreatedAtUtc = oldBinding.CreatedAtUtc,
                        UpdatedAtUtc = updatedAt,
                        ContentSelections = updatedItems.Select(item => new ContentSelection
                        {
                            ContentId = item.ContentId,
                            IsEnabled = item.ContentId == contentId || (oldBinding.ContentSelections.Count == 0 ? item.DefaultEnabled :
                                oldBinding.ContentSelections.Any(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && selection.IsEnabled)),
                            PathOverrides = oldBinding.ContentSelections.FirstOrDefault(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))?.PathOverrides ?? new Dictionary<string, string>(),
                            RemoteRootOverrides = oldBinding.ContentSelections.FirstOrDefault(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))?.RemoteRootOverrides ?? new Dictionary<string, string>(),
                            RemoteSubdirectoryOverrides = oldBinding.ContentSelections.FirstOrDefault(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))?.RemoteSubdirectoryOverrides ?? new Dictionary<string, string>(),
                            DisabledPathTemplates = oldBinding.ContentSelections.FirstOrDefault(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))?.DisabledPathTemplates ?? Array.Empty<string>(),
                            RemovedPathTemplates = oldBinding.ContentSelections.FirstOrDefault(selection => string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase))?.RemovedPathTemplates ?? Array.Empty<string>(),
                        }).ToArray(),
                    };
                    await _storageBindingRepository.UpsertAsync(updatedBinding, token);
                }
                try { await _userDefinitionStore.SaveDefinitionAsync(targetDefinition, token); }
                catch
                {
                    if (oldBinding is not null) await _storageBindingRepository.UpsertAsync(oldBinding, CancellationToken.None);
                    throw;
                }
                _binding = updatedBinding;
                _definition = targetDefinition;
            }, cancellationToken);
            BuildContentItems();
            return null;
        }
        catch (SyncBindingBusyException) { return Pick("该游戏仍有同步任务，请结束后再修改。", "Wait for this game's sync tasks to finish before changing its content."); }
        catch (SyncBindingChangedException) { return Pick("配置已发生变化，请刷新后重试。", "The configuration changed. Refresh and try again."); }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to add custom content item.");
            return exception.Message;
        }
        finally { IsBusy = false; }
    }

    public (bool Exists, string Message) TestSelectedPath()
    {
        if (SelectedContentItem is null)
        {
            return (false, Pick("尚未选择内容项。", "No content item selected."));
        }

        var path = SelectedContentItem.ResolvedPath;
        if (File.Exists(path) || Directory.Exists(path))
        {
            return (true, Pick($"路径存在：{path}", $"Path exists: {path}"));
        }

        return (false, Pick($"路径不存在：{path}", $"Path does not exist: {path}"));
    }

    public async Task<string?> QueueSyncAsync(SyncDirection direction, CancellationToken cancellationToken = default, IReadOnlyList<Guid>? targetIds = null)
    {
        if (IsBusy) return Pick("操作进行中，请稍候。", "An operation is in progress. Please wait.");
        if (_instance is null)
        {
            return Pick("游戏实例尚未加载。", "Game instance is not loaded.");
        }

        if (_binding is null)
        {
            return Pick("该游戏尚未绑定同步目标。", "This game has no storage binding yet.");
        }

        var queuedCount = 0;
        try
        {
            var selectedTargets = targetIds ?? Array.Empty<Guid>();
            if (selectedTargets.Count > 1 && direction != SyncDirection.Upload)
                return Pick("下载时请选择一个目标。", "Choose one target when downloading.");
            var targets = selectedTargets.Distinct().Select(id => (Id: id, Target: _syncTargetStore.Get(id))).ToArray();
            if (targets.Any(item => item.Target is null)) return Pick("所选同步目标已不存在。", "The selected sync target no longer exists.");
            foreach (var (targetId, target) in targets.Length == 0 ? new[] { (Id: Guid.Empty, Target: (GSYNC.App.Infrastructure.Configuration.SyncTargetConfig?)null) } : targets)
            {
                await _syncEngine.QueueAsync(new SyncJob
                {
                    GameInstanceId = _instance.Id, TargetId = targetId == Guid.Empty ? null : targetId,
                    Direction = direction, ProtectRemoteChanges = direction == SyncDirection.Upload,
                    DisplayName = target is null ? _instance.DisplayName : $"{_instance.DisplayName} · {target.Name}",
                }, cancellationToken);
                queuedCount++;
            }
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue {Direction} job.", direction);
            return queuedCount == 0 ? exception.Message : Pick($"已有 {queuedCount} 个任务加入队列，其余目标未加入：{exception.Message}", $"{queuedCount} jobs were queued; remaining targets were not queued: {exception.Message}");
        }
    }

    public async Task<(ConflictNavigationContext? Context, string? Error)> CompareAsync(CancellationToken cancellationToken = default, Guid? targetId = null)
    {
        if (_instance is null || _binding is null)
        {
            return (null, Pick("该游戏尚未绑定同步目标。", "This game has no storage binding yet."));
        }

        IsBusy = true;
        try
        {
            var comparison = await _syncEngine.CompareAsync(new SyncJob
            {
                GameInstanceId = _instance.Id,
                TargetId = targetId,
                Direction = SyncDirection.Compare,
                DisplayName = _instance.DisplayName,
            }, cancellationToken);

            return (new ConflictNavigationContext(_instance.Id, _instance.DisplayName, comparison, targetId), null);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Compare failed for instance {InstanceId}.", _instance.Id);
            return (null, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<string?> RestoreLatestSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var latest = Snapshots.FirstOrDefault();
        if (latest is null)
        {
            return Pick("还没有可恢复的快照。可以在设置中启用下载前自动快照。", "No snapshots to restore yet. Automatic snapshots before downloads can be enabled in Settings.");
        }

        return await RestoreSnapshotAsync(latest, cancellationToken);
    }

    public async Task<string?> RestoreSnapshotAsync(SnapshotRow snapshot, CancellationToken cancellationToken = default)
    {
        if (!Snapshots.Any(candidate => candidate.SnapshotId == snapshot.SnapshotId))
        {
            return Pick("该快照已不可用，请刷新后重试。", "The snapshot is no longer available. Refresh and try again.");
        }

        try
        {
            await _syncEngine.RestoreSnapshotAsync(snapshot.SnapshotId, cancellationToken);
            if (_instance is not null)
            {
                await LoadAsync(_instance.Id, cancellationToken);
            }

            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Restore failed for instance {InstanceId}.", _instance?.Id);
            return exception.Message;
        }
    }

    private void BuildContentItems()
    {
        if (_instance is null || _definition is null)
        {
            ContentItems = [];
            TableFooterText = Pick("没有可用的内容定义。", "No content definition available.");
            return;
        }

        var systemVariables = _systemVariableProvider.GetVariables();
        var sourceProvider = _sourceProviders.FirstOrDefault(provider =>
            string.Equals(provider.ProviderId, _instance.SourceProviderId, StringComparison.OrdinalIgnoreCase));
        var sourceVariables = sourceProvider?.ResolveVariables(_instance)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bindingSettings = _binding?.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var rows = new List<ContentItemRow>();
        long totalBytes = 0;
        foreach (var item in _definition.ContentItems)
        {
            foreach (var template in item.PathTemplates)
            {
                var selection = _binding?.ContentSelections.FirstOrDefault(value => string.Equals(value.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));
                if (selection?.IsTemplateRemoved(template) == true) continue;
                var enabled = IsContentEnabled(item) && (selection?.IsTemplateEnabled(template) ?? true);
                var effectiveTemplate = selection?.EffectiveTemplate(template) ?? template;
                var resolved = _pathResolver.Resolve(effectiveTemplate, systemVariables, sourceVariables, _instance.Variables, _userVariablesStore.AsDictionary(), bindingSettings);
                var (statusText, statusVariant, size) = DescribeLocalPath(resolved, enabled);
                totalBytes += size;
                rows.Add(new ContentItemRow(
                    item.ContentId,
                    DescribeCategory(item.Category),
                    resolved,
                    Pick("双向", "Bidirectional"),
                    statusText,
                    statusVariant,
                    enabled)
                {
                    PathTemplate = template,
                    EffectivePathTemplate = effectiveTemplate,
                    Patterns = BuildPatternText(item),
                    EnabledText = enabled ? Pick("开启", "On") : Pick("关闭", "Off"),
                    IsChinese = _isChinese,
                });
            }
        }

        ContentItems = rows;
        TotalSizeText = UiFormat.Bytes(totalBytes);
        var enabledCount = rows.Count(row => row.Enabled);
        TableFooterText = Pick(
            $"{rows.Count} 个内容项 · {enabledCount} 个已启用",
            $"{rows.Count} {(rows.Count == 1 ? "item" : "items")} · {enabledCount} enabled");

        if (rows.Count > 0)
        {
            var inspectorWasVisible = IsInspectorVisible;
            SelectContentItem(rows[0]);
            IsInspectorVisible = inspectorWasVisible;
        }
    }

    private bool IsContentEnabled(ContentItem item)
    {
        if (_binding is null || _binding.ContentSelections.Count == 0)
        {
            return item.DefaultEnabled;
        }

        return _binding.ContentSelections.Any(selection =>
            string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && selection.IsEnabled);
    }

    private (string Text, string Variant, long SizeBytes) DescribeLocalPath(string resolvedPath, bool enabled)
    {
        if (!enabled)
        {
            return (Pick("已禁用", "Disabled"), "disabled", 0);
        }

        try
        {
            var (searchRoot, pattern) = PathGlob.SplitRoot(resolvedPath);
            if (pattern is not null && Directory.Exists(searchRoot))
            {
                var matches = Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
                    .Where(file => PathGlob.IsMatch(pattern, Path.GetRelativePath(searchRoot, file), includeDescendants: true)).ToArray();
                if (matches.Length > 0) return (Pick("就绪", "Ready"), "ready", matches.Sum(file => new FileInfo(file).Length));
            }

            if (File.Exists(resolvedPath))
            {
                return (Pick("就绪", "Ready"), "ready", new FileInfo(resolvedPath).Length);
            }

            if (Directory.Exists(resolvedPath))
            {
                var size = Directory.EnumerateFiles(resolvedPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
                return (Pick("就绪", "Ready"), "ready", size);
            }
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to inspect path {Path}.", resolvedPath);
        }

        return (Pick("本地缺失", "Missing locally"), "pending", 0);
    }

    private string DescribeCategory(ContentCategory category)
    {
        return category switch
        {
            ContentCategory.Save => Pick("存档", "Saves"),
            ContentCategory.Config => Pick("配置", "Config"),
            ContentCategory.Extra => Pick("附加", "Extra"),
            _ => category.ToString(),
        };
    }

    private static string BuildPatternText(ContentItem item)
    {
        var parts = new List<string>();
        parts.AddRange(item.IncludePatterns.Select(pattern => $"+ {pattern}"));
        parts.AddRange(item.ExcludePatterns.Select(pattern => $"- {pattern}"));
        return string.Join('\n', parts);
    }

    private static string SlugifyContentId(string name)
    {
        var buffer = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(character);
            }
            else if ((char.IsWhiteSpace(character) || character is '-' or '_') && (buffer.Length == 0 || buffer[^1] != '-'))
            {
                buffer.Append('-');
            }
        }

        return buffer.Length == 0 ? "custom-item" : buffer.ToString().Trim('-');
    }

    private string DescribeSource(string sourceProviderId)
    {
        return sourceProviderId.ToLowerInvariant() switch
        {
            "steam" => "Steam",
            "epic" => "Epic",
            "custom" => Pick("自定义", "Custom"),
            _ => sourceProviderId,
        };
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record GameTargetOption(Guid Id, string Name, string ProviderId, string Location)
{
    public string Label => string.IsNullOrWhiteSpace(Location) ? Name : $"{Name} · {Location}";
}

public sealed record ContentItemRow(
    string Name,
    string Category,
    string ResolvedPath,
    string Policy,
    string Status,
    string StatusVariant,
    bool Enabled)
{
    public string PathTemplate { get; init; } = string.Empty;
    public string EffectivePathTemplate { get; init; } = string.Empty;

    public string Patterns { get; init; } = string.Empty;

    public string EnabledText { get; init; } = string.Empty;
    public bool IsChinese { get; init; }
    public string OpenFolderText => IsChinese ? "打开目录" : "Open folder";
    public string TestPathText => IsChinese ? "测试路径" : "Test path";
    public string DetailsText => IsChinese ? "查看详情" : "Details";
    public string EditText => IsChinese ? "编辑路径" : "Edit path";
    public string ToggleText => Enabled ? (IsChinese ? "停用" : "Disable") : (IsChinese ? "启用" : "Enable");
    public string RemoveText => IsChinese ? "移除" : "Remove";

    public string EnabledVariant => Enabled ? "synced" : "disabled";
}
