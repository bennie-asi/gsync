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

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private bool _isSyncInProgress;

    [ObservableProperty]
    private string _selectedStatus;

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
        _selectedStatus = StatusFilters[0];
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
            foreach (var instance in instances)
            {
                var bindings = await _storageBindingRepository.ListByGameInstanceAsync(instance.Id, cancellationToken);
                var binding = bindings.FirstOrDefault();
                var definition = await _manifestService.GetDefinitionAsync(instance.GameId, cancellationToken);
                lastRecordByInstance.TryGetValue(instance.Id, out var lastRecord);

                var (statusText, statusVariant) = DescribeInstanceStatus(lastRecord);
                rows.Add(new LibraryGameRow(
                    instance.DisplayName,
                    DescribeSource(instance.SourceProviderId),
                    (definition?.ContentItems.Count ?? 0).ToString(),
                    statusText,
                    statusText,
                    UiFormat.RelativeTime(lastRecord?.CompletedAtUtc ?? lastRecord?.StartedAtUtc, _isChinese),
                    ResolveTargetName(binding),
                    false,
                    statusVariant,
                    statusVariant)
                {
                    InstanceId = instance.Id,
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

            Activity = recentRecords
                .Take(12)
                .Select(record => new UiActivityItem(
                    DescribeActivityTitle(record, instanceNames),
                    record.ErrorMessage ?? record.Summary ?? UiFormat.StatusText(record.Status, _isChinese),
                    UiFormat.RelativeTime(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
                    record.Status == SyncJobStatus.Failed))
                .ToArray();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load library page data.");
        }
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

    public async Task QueueSyncForGameAsync(Guid instanceId, SyncDirection direction, CancellationToken cancellationToken = default)
    {
        try
        {
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = instanceId,
                Direction = direction,
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

    private (string Text, string Variant) DescribeInstanceStatus(SyncRecord? lastRecord)
    {
        if (lastRecord is null)
        {
            return (Pick("未同步", "Not synced"), "pending");
        }

        return lastRecord.Status switch
        {
            SyncJobStatus.Completed => (Pick("已同步", "Synced"), "synced"),
            SyncJobStatus.Failed => (Pick("失败", "Failed"), "conflict"),
            SyncJobStatus.Cancelled => (Pick("已取消", "Cancelled"), "disabled"),
            _ => (Pick("进行中", "In progress"), "pending"),
        };
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
}

public sealed record LibraryStat(string Label, string Value);
