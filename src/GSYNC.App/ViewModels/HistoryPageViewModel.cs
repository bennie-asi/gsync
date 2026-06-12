using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class HistoryPageViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly ISyncEngine _syncEngine;

    private IReadOnlyList<HistoryRow> _allEntries = [];
    private IReadOnlyDictionary<Guid, string> _instanceNames = new Dictionary<Guid, string>();

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private string _selectedHistoryFilter;

    [ObservableProperty]
    private string _selectedGameFilter;

    [ObservableProperty]
    private string _selectedResultFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<HistoryRow> _entries = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _recordDetails = [];

    [ObservableProperty]
    private IReadOnlyList<SnapshotRow> _snapshots = [];

    [ObservableProperty]
    private IReadOnlyList<string> _gameFilters = [];

    [ObservableProperty]
    private string _detailRecordId = string.Empty;

    [ObservableProperty]
    private string _detailStatus = string.Empty;

    [ObservableProperty]
    private string _detailStatusVariant = "ready";

    [ObservableProperty]
    private string _detailGameName = string.Empty;

    [ObservableProperty]
    private string _systemStatus = string.Empty;

    [ObservableProperty]
    private string _snapshotGameLabel = string.Empty;

    public HistoryPageViewModel(
        ILocalizationService localizationService,
        ISyncHistoryRepository syncHistoryRepository,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        ISyncEngine syncEngine)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _syncHistoryRepository = syncHistoryRepository;
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncEngine = syncEngine;

        PageTitle = Pick("历史与快照", "History & Snapshots");
        PageSubtitle = Pick("精炼审计工作区，集中展示记录详情、恢复动作与空状态恢复入口。", "Refined audit workspace with record details, restore actions, and an empty-state recovery path.");
        SearchPlaceholder = Pick("搜索记录", "Search records");
        GameFilterPlaceholder = Pick("全部游戏", "All Games");
        ResultFilterPlaceholder = Pick("全部结果", "All Results");
        TimeRangePlaceholder = Pick("最近 30 天", "Last 30 Days");
        RefreshButtonText = Pick("刷新", "Refresh");
        RestoreSnapshotButtonText = Pick("恢复快照", "Restore Snapshot");
        LogsButtonText = Pick("日志", "Logs");
        HelpButtonText = Pick("帮助", "Help");
        TableTitle = Pick("同步记录", "Sync Records");
        TableSubtitle = Pick("左侧主表格保持高密度审计视图，右侧持续显示选中记录和快照。", "The left table stays dense while the right pane keeps the selected record and snapshots visible.");
        DetailTitle = Pick("记录详情", "Record Details");
        DetailSubtitle = Pick("右侧持续保留选中记录、恢复动作与相关快照。", "The right pane keeps the selected record, restore actions, and related snapshots visible.");
        DetailSummaryTitle = Pick("选中记录", "Selected Record");
        DetailSummarySubtitle = Pick("时间、结果与关联快照在同一处查看。", "Time, result, and the related snapshot stay together.");
        SnapshotsTitle = Pick("可用快照", "Available Snapshots");
        SnapshotsSubtitle = Pick("最近快照保持紧凑显示，便于直接恢复。", "Recent snapshots stay compact and ready to restore.");
        RestoreActionText = Pick("恢复", "Restore");
        EmptyStateTitle = Pick("还没有历史记录", "No History Yet");
        EmptyStateMessage = Pick("首次同步完成后，这里会显示按时间排序的记录、结果与快照恢复入口。", "After the first sync, this view will list records, results, and snapshot restore entry points.");
        EmptyStateHint = Pick("你可以先返回 Library 发起首次同步，随后这里会自动回到完整审计视图。", "Start the first sync from Library and this screen will return to the full audit view automatically.");
        EmptyStateActionText = Pick("前往 Library 发起首次同步", "Go to Library for the first sync");
        EmptyStateSecondaryActionText = Pick("查看日志说明", "Review log guidance");
        EmptyStateInspectorTitle = Pick("空状态仍保留恢复上下文", "Empty state still keeps recovery context");
        EmptyStateInspectorMessage = Pick("右侧面板继续固定显示后续步骤和快照恢复建议，而不是退化为脱离主结构的空白区域。", "The right pane keeps next steps and recovery guidance visible instead of collapsing into a disconnected blank area.");
        EmptyStateRecoveryTitle = Pick("下一步", "Next Steps");
        EmptyStateRecoverySteps =
        [
            Pick("返回 Library 发起首次同步。", "Return to Library and run the first sync."),
            Pick("确认同步目标与本地路径都保持可用。", "Confirm that both the sync target and the local path stay available."),
            Pick("完成首次同步后回到此页检查记录与快照。", "Come back after the first sync to review records and snapshots."),
        ];

        ResultFilters =
        [
            Pick("全部结果", "All Results"),
            Pick("成功", "Success"),
            Pick("失败", "Failed"),
            Pick("已取消", "Cancelled"),
        ];
        TimeRangeFilters =
        [
            Pick("今天", "Today"),
            Pick("最近 7 天", "Last 7 Days"),
            Pick("最近 30 天", "Last 30 Days"),
            Pick("全部时间", "All Time"),
        ];

        _gameFilters = [GameFilterPlaceholder];
        _selectedGameFilter = GameFilterPlaceholder;
        _selectedResultFilter = ResultFilters[0];
        _selectedHistoryFilter = TimeRangeFilters[2];
        _systemStatus = string.Empty;
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string GameFilterPlaceholder { get; }
    public string ResultFilterPlaceholder { get; }
    public string TimeRangePlaceholder { get; }
    public string RefreshButtonText { get; }
    public string RestoreSnapshotButtonText { get; }
    public string LogsButtonText { get; }
    public string HelpButtonText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string DetailTitle { get; }
    public string DetailSubtitle { get; }
    public string DetailSummaryTitle { get; }
    public string DetailSummarySubtitle { get; }
    public string SnapshotsTitle { get; }
    public string SnapshotsSubtitle { get; }
    public string RestoreActionText { get; }
    public string EmptyStateTitle { get; }
    public string EmptyStateMessage { get; }
    public string EmptyStateHint { get; }
    public string EmptyStateActionText { get; }
    public string EmptyStateSecondaryActionText { get; }
    public string EmptyStateInspectorTitle { get; }
    public string EmptyStateInspectorMessage { get; }
    public string EmptyStateRecoveryTitle { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<string> ResultFilters { get; }
    public IReadOnlyList<string> TimeRangeFilters { get; }
    public IReadOnlyList<string> EmptyStateRecoverySteps { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            _instanceNames = instances.ToDictionary(instance => instance.Id, instance => instance.DisplayName);

            var records = await _syncHistoryRepository.ListRecentRecordsAsync(300, cancellationToken);
            var targetNames = new Dictionary<Guid, string>();
            var rows = new List<HistoryRow>(records.Count);
            foreach (var record in records)
            {
                if (!targetNames.TryGetValue(record.GameInstanceId, out var targetName))
                {
                    targetName = await ResolveTargetNameAsync(record.GameInstanceId, cancellationToken);
                    targetNames[record.GameInstanceId] = targetName;
                }

                rows.Add(CreateRow(record, targetName));
            }

            _allEntries = rows;
            GameFilters = new[] { GameFilterPlaceholder }
                .Concat(instances.Select(instance => instance.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (!GameFilters.Contains(SelectedGameFilter, StringComparer.Ordinal))
            {
                SelectedGameFilter = GameFilters[0];
            }

            ApplyFilters();

            var completedToday = records.Count(record =>
                record.Status == SyncJobStatus.Completed &&
                (record.CompletedAtUtc ?? record.StartedAtUtc).ToLocalTime().Date == DateTimeOffset.Now.Date);
            SystemStatus = Pick(
                $"共 {records.Count} 条记录 • 今日完成 {completedToday} 次同步",
                $"{records.Count} records • {completedToday} syncs completed today");

            if (rows.Count > 0)
            {
                await SelectRecordAsync(rows[0], cancellationToken);
            }
            else
            {
                RecordDetails = [];
                Snapshots = [];
                DetailRecordId = string.Empty;
                DetailGameName = string.Empty;
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load history page data.");
        }
    }

    public async Task SelectRecordAsync(HistoryRow row, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = row.Record;
            DetailRecordId = $"REC-{record.Id.ToString("N")[..8]}";
            DetailStatus = $"{UiFormat.DirectionText(record.Direction, _isChinese)} · {UiFormat.StatusText(record.Status, _isChinese)}";
            DetailStatusVariant = UiFormat.StatusVariant(record.Status);
            DetailGameName = row.Game;
            SnapshotGameLabel = row.Game;

            var duration = record.CompletedAtUtc is not null
                ? (record.CompletedAtUtc.Value - record.StartedAtUtc).TotalSeconds.ToString("0") + "s"
                : "--";
            var details = new List<UiPropertyRow>
            {
                new(Pick("时间", "Time"), UiFormat.TimeOfRecord(record.StartedAtUtc, _isChinese)),
                new(Pick("耗时", "Duration"), duration),
                new(Pick("已处理文件", "Processed Files"), $"{record.ProcessedFiles}/{record.TotalFiles}"),
                new(Pick("摘要", "Summary"), record.Summary ?? "--"),
            };
            if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
            {
                details.Add(new UiPropertyRow(Pick("错误", "Error"), record.ErrorMessage));
            }

            if (record.SnapshotId is not null)
            {
                details.Add(new UiPropertyRow(Pick("相关快照", "Related Snapshot"), $"SN-{record.SnapshotId.Value.ToString("N")[..8]}"));
            }

            RecordDetails = details;

            var snapshots = await _syncHistoryRepository.ListSnapshotsAsync(record.GameInstanceId, cancellationToken);
            Snapshots = snapshots
                .Select((snapshot, index) => new SnapshotRow(
                    $"SN-{snapshot.Id.ToString("N")[..8]}",
                    index == 0 ? Pick("最新", "Latest") : string.Empty,
                    UiFormat.TimeOfRecord(snapshot.CreatedAtUtc, _isChinese),
                    Environment.MachineName,
                    UiFormat.Bytes(snapshot.TotalBytes))
                {
                    SnapshotId = snapshot.Id,
                })
                .ToArray();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load record details.");
        }
    }

    public async Task<string?> RestoreSnapshotAsync(SnapshotRow snapshotRow, CancellationToken cancellationToken = default)
    {
        try
        {
            await _syncEngine.RestoreSnapshotAsync(snapshotRow.SnapshotId, cancellationToken);
            await LoadAsync(cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Snapshot restore failed from history page.");
            return exception.Message;
        }
    }

    partial void OnSelectedHistoryFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedGameFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedResultFilterChanged(string value) => ApplyFilters();

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<HistoryRow> filtered = _allEntries;

        if (!string.IsNullOrWhiteSpace(SelectedGameFilter) && SelectedGameFilter != GameFilterPlaceholder)
        {
            filtered = filtered.Where(row => string.Equals(row.Game, SelectedGameFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedResultFilter) && SelectedResultFilter != ResultFilters[0])
        {
            filtered = filtered.Where(row => string.Equals(row.Result, SelectedResultFilter, StringComparison.OrdinalIgnoreCase));
        }

        var cutoff = SelectedHistoryFilter == TimeRangeFilters[0]
            ? DateTimeOffset.Now.Date
            : SelectedHistoryFilter == TimeRangeFilters[1]
                ? DateTimeOffset.Now.AddDays(-7)
                : SelectedHistoryFilter == TimeRangeFilters[2]
                    ? DateTimeOffset.Now.AddDays(-30)
                    : DateTimeOffset.MinValue;
        if (cutoff > DateTimeOffset.MinValue)
        {
            filtered = filtered.Where(row => (row.Record.CompletedAtUtc ?? row.Record.StartedAtUtc).ToLocalTime() >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(row =>
                row.Game.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (row.Record.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Entries = filtered.ToArray();
        IsEmptyState = _allEntries.Count == 0;
    }

    private HistoryRow CreateRow(SyncRecord record, string targetName)
    {
        var gameName = _instanceNames.GetValueOrDefault(record.GameInstanceId, Pick("未知游戏", "Unknown game"));
        return new HistoryRow(
            UiFormat.TimeOfRecord(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
            gameName,
            UiFormat.DirectionText(record.Direction, _isChinese),
            UiFormat.StatusText(record.Status, _isChinese),
            UiFormat.StatusVariant(record.Status),
            targetName,
            Environment.MachineName,
            record.TotalFiles > 0 ? $"{record.ProcessedFiles}/{record.TotalFiles}" : "--")
        {
            Record = record,
        };
    }

    private async Task<string> ResolveTargetNameAsync(Guid gameInstanceId, CancellationToken cancellationToken)
    {
        try
        {
            var bindings = await _storageBindingRepository.ListByGameInstanceAsync(gameInstanceId, cancellationToken);
            var binding = bindings.FirstOrDefault();
            if (binding is null)
            {
                return Pick("未绑定", "Unbound");
            }

            return binding.Settings.TryGetValue("targetName", out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : binding.StorageProviderId;
        }
        catch
        {
            return "--";
        }
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record HistoryRow(
    string Time,
    string Game,
    string Direction,
    string Result,
    string ResultVariant,
    string Target,
    string Device,
    string Size)
{
    public required SyncRecord Record { get; init; }
}

public sealed record SnapshotRow(
    string Name,
    string StateText,
    string Timestamp,
    string Device,
    string Size)
{
    public Guid SnapshotId { get; init; }

    public string Detail => string.IsNullOrWhiteSpace(StateText) ? $"{Device} · {Size}" : $"{StateText} · {Device} · {Size}";
}
