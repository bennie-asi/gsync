using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class LibraryPageViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IManifestService _manifestService;
    private readonly ISyncEngine _syncEngine;
    private readonly ISyncQueue _syncQueue;
    private readonly SyncTargetStore _syncTargetStore;

    private IReadOnlyList<LibraryGameRow> _allGames = [];
    private bool _sortByNameAscending = true;
    private CancellationTokenSource? _statusRefreshCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private bool _isSyncInProgress;

    [ObservableProperty]
    private string _selectedStatus = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<LibraryGameRow> _games = [];

    [ObservableProperty]
    private IReadOnlyList<LibraryOverviewMetric> _overviewMetrics = [];

    [ObservableProperty]
    private IReadOnlyList<LibraryStat> _stats = [];

    [ObservableProperty]
    private IReadOnlyList<UiActivityItem> _activity = [];

    public LibraryPageViewModel(
        ILocalizationService localizationService,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IManifestService manifestService,
        ISyncEngine syncEngine,
        ISyncQueue syncQueue,
        SyncTargetStore syncTargetStore)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _manifestService = manifestService;
        _syncEngine = syncEngine;
        _syncQueue = syncQueue;
        _syncTargetStore = syncTargetStore;

        PageTitle = Pick("游戏库", "Game Library");
        PageSubtitle = Pick("密集型多游戏总览，集中展示同步状态、近期活动与快捷恢复操作。", "Dense multi-game overview with explicit sync state, recent activity, and quick recovery actions.");
        SearchPlaceholder = Pick("搜索游戏", "Search games");
        StatusFilterPlaceholder = Pick("状态", "Status");
        SortText = Pick("排序", "Sort");
        RefreshText = Pick("刷新", "Refresh");
        AddGameText = Pick("添加游戏", "Add Game");
        SyncNowText = Pick("立即同步", "Sync Now");
        TableTitle = string.Empty;
        TableSubtitle = string.Empty;
        TableFooterText = string.Empty;
        OverviewTitle = Pick("总览", "Overview");
        OverviewSubtitle = Pick("保留关键指标摘要，活动轨迹在下方独立显示。", "Keep key metrics here, with the activity timeline displayed below.");
        ActivityTitle = Pick("活动时间线", "Activity Timeline");
        ActivitySubtitle = Pick("按时间顺序查看最近同步、冲突与恢复事件。", "Review recent sync, conflict, and recovery events in chronological order.");
        OpenButtonText = Pick("打开", "Open");
        MoreButtonText = Pick("更多", "More");

        StatusFilters =
        [
            Pick("全部状态", "All statuses"),
            Pick("已同步", "Synced"),
            Pick("本地较新", "Local newer"),
            Pick("远端较新", "Remote newer"),
            Pick("冲突", "Conflict"),
        ];
        SelectedStatus = StatusFilters[0];
        SearchText = string.Empty;
        Games = [];
        OverviewMetrics = [];
        Stats = [];
        Activity = [];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string StatusFilterPlaceholder { get; }
    public string SortText { get; }
    public string RefreshText { get; }
    public string AddGameText { get; }
    public string SyncNowText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string OverviewTitle { get; }
    public string OverviewSubtitle { get; }
    public string ActivityTitle { get; }
    public string ActivitySubtitle { get; }
    public string OpenButtonText { get; }
    public string MoreButtonText { get; }
    public bool IsChinese => _isChinese;

    public IReadOnlyList<string> StatusFilters { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            var recentRecords = await _syncHistoryRepository.ListRecentRecordsAsync(200, cancellationToken);
            var lastRecordByInstance = recentRecords
                .GroupBy(record => record.GameInstanceId)
                .ToDictionary(group => group.Key, group => group.First());
            var instanceNames = instances.ToDictionary(instance => instance.Id, instance => instance.DisplayName);

            var rows = new List<LibraryGameRow>(instances.Count);
            var comparableInstanceIds = new List<Guid>();
            var checkingText = Pick("检查中…", "Checking…");
            var unboundStatus = Pick("未绑定", "Unbound");
            foreach (var instance in instances)
            {
                var bindings = await _storageBindingRepository.ListByGameInstanceAsync(instance.Id, cancellationToken);
                var binding = bindings.FirstOrDefault();
                var definition = await _manifestService.GetDefinitionAsync(instance.GameId, cancellationToken);
                lastRecordByInstance.TryGetValue(instance.Id, out var lastRecord);

                // Local/remote status reflects a real two-sided comparison, which is computed in the
                // background after the page renders. Until that completes, show a neutral placeholder
                // (or "Unbound" for games without a storage target, which cannot be compared).
                string localText, remoteText, localVariant, remoteVariant;
                if (binding is null)
                {
                    localText = remoteText = unboundStatus;
                    localVariant = remoteVariant = "disabled";
                }
                else
                {
                    localText = remoteText = checkingText;
                    localVariant = remoteVariant = "pending";
                    comparableInstanceIds.Add(instance.Id);
                }

                rows.Add(new LibraryGameRow(
                    instance.DisplayName,
                    DescribeSource(instance.SourceProviderId),
                    (definition?.ContentItems.Count ?? 0).ToString(),
                    localText,
                    remoteText,
                    UiFormat.RelativeTime(lastRecord?.CompletedAtUtc ?? lastRecord?.StartedAtUtc, _isChinese),
                    ResolveTargetName(binding),
                    false,
                    localVariant,
                    remoteVariant)
                {
                    InstanceId = instance.Id,
                    OpenLabel = OpenButtonText,
                    MoreLabel = MoreButtonText,
                });
            }

            _allGames = rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            ApplyFilters();
            IsEmptyState = rows.Count == 0;

            var failedCount = recentRecords.Count(record => record.Status == SyncJobStatus.Failed);
            OverviewMetrics =
            [
                new(Pick("游戏总数", "Total Games"), instances.Count.ToString()),
                new(Pick("同步记录", "Sync Records"), recentRecords.Count.ToString()),
                new(Pick("最近失败", "Recent Failures"), failedCount.ToString()),
                new(Pick("队列中", "Queued"), _syncQueue.QueueDepth.ToString()),
            ];

            var defaultTarget = _syncTargetStore.GetDefault();
            var lastSync = recentRecords.FirstOrDefault();
            Stats =
            [
                new(Pick("主要目标", "Primary target"), defaultTarget?.Name ?? Pick("未配置", "Not configured")),
                new(Pick("最近同步", "Last sync"), UiFormat.RelativeTime(lastSync?.CompletedAtUtc ?? lastSync?.StartedAtUtc, _isChinese)),
                new(Pick("冲突策略", "Conflict policy"), Pick("覆盖前提示", "Prompt before overwrite")),
                new(Pick("下载前快照", "Snapshot before download"), Pick("已启用", "Enabled")),
            ];

            var activityRecords = recentRecords.Take(12).ToArray();
            Activity = activityRecords
                .Select((record, index) => new UiActivityItem(
                    DescribeActivityTitle(record, instanceNames),
                    record.ErrorMessage ?? record.Summary ?? UiFormat.StatusText(record.Status, _isChinese),
                    UiFormat.RelativeTime(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
                    UiFormat.StatusVariant(record.Status),
                    index == activityRecords.Length - 1))
                .ToArray();

            _statusRefreshCts?.Cancel();
            _statusRefreshCts = new CancellationTokenSource();
            _ = RefreshSyncStatusesAsync(comparableInstanceIds, _statusRefreshCts.Token);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load library page data.");
        }
    }

    /// <summary>
    /// Computes the real local-vs-remote sync state for each game in the background and folds the
    /// result back into the visible rows. This is read-only (it never writes history records) but
    /// touches the network and hashes local files, so it runs after the initial page render rather
    /// than blocking it.
    /// </summary>
    private async Task RefreshSyncStatusesAsync(IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return;
        }

        var results = new Dictionary<Guid, LibraryStatusPair>();
        foreach (var instanceId in instanceIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var comparison = await Task.Run(
                    () => _syncEngine.CompareAsync(
                        new SyncJob { GameInstanceId = instanceId, Direction = SyncDirection.Compare },
                        cancellationToken),
                    cancellationToken);
                results[instanceId] = AggregateStatus(comparison);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Failed to compute sync status for game {GameInstanceId}.", instanceId);
                var failed = Pick("无法比较", "Compare failed");
                results[instanceId] = new LibraryStatusPair(failed, "disabled", failed, "disabled");
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _allGames = _allGames
            .Select(row => results.TryGetValue(row.InstanceId, out var pair)
                ? row with
                {
                    LocalStatus = pair.LocalText,
                    LocalStatusVariant = pair.LocalVariant,
                    RemoteStatus = pair.RemoteText,
                    RemoteStatusVariant = pair.RemoteVariant,
                }
                : row)
            .ToArray();
        ApplyFilters();
    }

    /// <summary>
    /// Folds a per-file comparison into two independent status badges: the local column reflects
    /// whether the on-disk save has unsynced edits, the remote column whether the cloud copy is
    /// ahead. When both sides changed (or a true content conflict exists) both columns read
    /// "Conflict".
    /// </summary>
    private LibraryStatusPair AggregateStatus(SyncComparison comparison)
    {
        var hasLocal = false;
        var hasRemote = false;
        var localNewer = false;
        var remoteNewer = false;
        var conflict = false;

        foreach (var entry in comparison.Entries)
        {
            switch (entry.ChangeKind)
            {
                case SyncChangeKind.Unchanged:
                    hasLocal = true;
                    hasRemote = true;
                    break;
                case SyncChangeKind.AddedLocally:
                case SyncChangeKind.UpdatedLocally:
                    hasLocal = true;
                    localNewer = true;
                    break;
                case SyncChangeKind.AddedRemotely:
                case SyncChangeKind.UpdatedRemotely:
                    hasRemote = true;
                    remoteNewer = true;
                    break;
                case SyncChangeKind.Conflict:
                    hasLocal = true;
                    hasRemote = true;
                    var order = CompareModifiedTimes(entry.LocalModifiedAtUtc, entry.RemoteModifiedAtUtc);
                    if (order > 0)
                    {
                        localNewer = true;
                    }
                    else if (order < 0)
                    {
                        remoteNewer = true;
                    }
                    else
                    {
                        conflict = true;
                    }

                    break;
            }
        }

        if (!hasLocal && !hasRemote)
        {
            var empty = Pick("空", "Empty");
            return new LibraryStatusPair(empty, "missing", empty, "missing");
        }

        if (conflict || (localNewer && remoteNewer))
        {
            var conflictText = Pick("冲突", "Conflict");
            return new LibraryStatusPair(conflictText, "conflict", conflictText, "conflict");
        }

        string localText, localVariant, remoteText, remoteVariant;

        if (!hasLocal)
        {
            localText = Pick("缺失", "Missing");
            localVariant = "missing";
        }
        else if (localNewer)
        {
            localText = Pick("本地较新", "Local newer");
            localVariant = "local newer";
        }
        else
        {
            localText = Pick("已同步", "Synced");
            localVariant = "synced";
        }

        if (!hasRemote)
        {
            remoteText = Pick("未上传", "Not uploaded");
            remoteVariant = "missing";
        }
        else if (remoteNewer)
        {
            remoteText = Pick("远端较新", "Remote newer");
            remoteVariant = "remote newer";
        }
        else
        {
            remoteText = Pick("已同步", "Synced");
            remoteVariant = "synced";
        }

        return new LibraryStatusPair(localText, localVariant, remoteText, remoteVariant);
    }

    private static int CompareModifiedTimes(DateTimeOffset? local, DateTimeOffset? remote)
    {
        if (local is null && remote is null)
        {
            return 0;
        }

        if (local is null)
        {
            return -1;
        }

        if (remote is null)
        {
            return 1;
        }

        var difference = (local.Value - remote.Value).TotalSeconds;
        if (difference > 1)
        {
            return 1;
        }

        if (difference < -1)
        {
            return -1;
        }

        return 0;
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsSyncInProgress)
        {
            return;
        }

        IsSyncInProgress = true;
        try
        {
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            foreach (var instance in instances)
            {
                await _syncEngine.QueueAsync(new SyncJob
                {
                    GameInstanceId = instance.Id,
                    Direction = SyncDirection.Upload,
                    DisplayName = instance.DisplayName,
                }, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue sync-all jobs.");
        }
        finally
        {
            IsSyncInProgress = false;
        }
    }

    public async Task QueueSyncForGameAsync(Guid instanceId, SyncDirection direction, string? displayName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = instanceId,
                Direction = direction,
                DisplayName = displayName,
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue {Direction} for game {GameInstanceId}.", direction, instanceId);
        }
    }

    public void ToggleSort()
    {
        _sortByNameAscending = !_sortByNameAscending;
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedStatusChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<LibraryGameRow> filtered = _allGames;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(row => row.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != StatusFilters[0])
        {
            filtered = filtered.Where(row =>
                string.Equals(row.LocalStatus, SelectedStatus, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.RemoteStatus, SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        filtered = _sortByNameAscending
            ? filtered.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            : filtered.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase);

        Games = filtered.ToArray();
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

    private string ResolveTargetName(StorageBinding? binding)
    {
        if (binding is null)
        {
            return Pick("未绑定", "Unbound");
        }

        if (binding.Settings.TryGetValue("targetName", out var targetName) && !string.IsNullOrWhiteSpace(targetName))
        {
            return targetName;
        }

        return binding.StorageProviderId;
    }

    private string DescribeActivityTitle(SyncRecord record, IReadOnlyDictionary<Guid, string> instanceNames)
    {
        var name = instanceNames.GetValueOrDefault(record.GameInstanceId, Pick("未知游戏", "Unknown game"));
        var action = (record.Direction, record.Status) switch
        {
            (_, SyncJobStatus.Failed) => Pick("失败", "failed"),
            (SyncDirection.Upload, _) => Pick("已上传", "uploaded"),
            (SyncDirection.Download, _) => Pick("已下载", "downloaded"),
            (SyncDirection.Compare, _) => Pick("已比较", "compared"),
            _ => Pick("已同步", "synced"),
        };

        return _isChinese ? $"{name} {action}" : $"{name} {action}";
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record LibraryOverviewMetric(string Label, string Value);

public sealed record LibraryGameRow(
    string Name,
    string Source,
    string ItemCount,
    string LocalStatus,
    string RemoteStatus,
    string LastSync,
    string Target,
    bool IsSelected,
    string LocalStatusVariant = "ready",
    string RemoteStatusVariant = "ready")
{
    public Guid InstanceId { get; init; }

    public string OpenLabel { get; init; } = string.Empty;

    public string MoreLabel { get; init; } = string.Empty;
}

internal sealed record LibraryStatusPair(string LocalText, string LocalVariant, string RemoteText, string RemoteVariant);

public sealed record LibraryStat(string Label, string Value);
