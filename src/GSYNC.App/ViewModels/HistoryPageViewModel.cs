using System.IO.Compression;
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
    private readonly ISyncEngine _syncEngine;

    private IReadOnlyList<HistoryRow> _allEntries = [];
    private IReadOnlyDictionary<Guid, string> _instanceNames = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, GameInstance> _instancesById = new Dictionary<Guid, GameInstance>();
    private Guid? _selectedRecordId;
    private long _selectionVersion;
    private Guid? _navigationGameId;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private bool _isRestoreInProgress;

    [ObservableProperty]
    private string _selectedHistoryFilter = string.Empty;

    [ObservableProperty]
    private string _selectedGameFilter = string.Empty;

    [ObservableProperty]
    private string _selectedResultFilter = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<HistoryRow> _entries = [];

    [ObservableProperty]
    private HistoryRow? _selectedEntry;

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
    private string _detailStatusVariant = string.Empty;

    [ObservableProperty]
    private string _detailSummarySubtitle = string.Empty;

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
        _syncEngine = syncEngine;

        PageTitle = Pick("历史与快照", "History & Snapshots");
        PageSubtitle = Pick("查看同步历史、检查记录详情、恢复快照", "View sync history, inspect record details, and restore snapshots");
        SearchPlaceholder = Pick("搜索记录", "Search records");
        GameFilterPlaceholder = Pick("全部游戏", "All Games");
        ResultFilterPlaceholder = Pick("全部结果", "All Results");
        TimeRangePlaceholder = Pick("最近 30 天", "Last 30 Days");
        RefreshButtonText = Pick("刷新", "Refresh");
        RestoreSnapshotButtonText = Pick("恢复快照", "Restore Snapshot");
        LogsButtonText = Pick("日志", "Logs");
        HelpButtonText = Pick("帮助", "Help");
        TableTitle = Pick("同步记录", "Sync Records");
        TableSubtitle = Pick("点击记录查看详情与关联快照", "Click a record to view its details and snapshots");
        DetailTitle = Pick("记录详情", "Record Details");
        DetailSubtitle = Pick("选中记录的详情与关联快照", "Details and snapshots for the selected record");
        DetailSummaryTitle = Pick("选中记录", "Selected Record");
        SnapshotsTitle = Pick("可用快照", "Available Snapshots");
        SnapshotsSubtitle = Pick("点击「恢复」可将本地存档回滚至该快照", "Click Restore to roll back local saves to that snapshot");
        RestoreActionText = Pick("恢复", "Restore");
        RestoringText = Pick("正在恢复快照…", "Restoring snapshot…");
        EmptyStateTitle = Pick("还没有历史记录", "No History Yet");
        EmptyStateMessage = Pick("首次同步完成后，这里会显示按时间排序的记录、结果与快照恢复入口。", "After the first sync, this view will list records, results, and snapshot restore entry points.");
        EmptyStateHint = Pick("你可以先返回游戏库发起首次同步。", "Start the first sync from Library.");
        EmptyStateActionText = Pick("前往游戏库", "Go to Library");
        EmptyStateSecondaryActionText = Pick("打开日志目录", "Open log folder");
        EmptyStateInspectorTitle = Pick("等待首次同步", "Waiting for First Sync");
        EmptyStateInspectorMessage = Pick("完成首次同步后，此处将显示完整历史记录与快照恢复功能。", "After the first sync completes, the full record history and snapshot restore panel will appear here.");
        EmptyStateRecoveryTitle = Pick("下一步", "Next Steps");
        EmptyStateRecoverySteps =
        [
            Pick("返回游戏库发起首次同步。", "Return to Library and run the first sync."),
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

        GameFilters = [GameFilterPlaceholder];
        SelectedGameFilter = GameFilterPlaceholder;
        SelectedResultFilter = ResultFilters[0];
        SelectedHistoryFilter = TimeRangeFilters[2];
        SystemStatus = string.Empty;
        SearchText = string.Empty;
        Entries = [];
        RecordDetails = [];
        Snapshots = [];
        DetailRecordId = string.Empty;
        DetailStatus = string.Empty;
        DetailStatusVariant = "ready";
        DetailSummarySubtitle = string.Empty;
        DetailGameName = string.Empty;
        SnapshotGameLabel = string.Empty;
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
    public string SnapshotsTitle { get; }
    public string SnapshotsSubtitle { get; }
    public string RestoreActionText { get; }
    public string RestoringText { get; }
    public string RecordFilesText => Pick("查看记录文件", "View record files");
    public string NoMatchingRecordsText => Pick("没有符合筛选条件的记录。", "No records match the current filters.");
    public string SelectRecordText => Pick("选择一条记录以查看详情和快照。", "Select a record to view its details and snapshots.");
    public bool NoMatchingRecords => !IsEmptyState && Entries.Count == 0;
    public bool HasSelectedEntry => SelectedEntry is not null;
    public bool NeedsSelection => !IsEmptyState && Entries.Count > 0 && SelectedEntry is null;

    /// <summary>True while no restore is running, so restore entry points can bind their enabled state.</summary>
    public bool CanRestore => !IsRestoreInProgress && Snapshots.Count > 0;
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
            _instancesById = instances.ToDictionary(instance => instance.Id);

            var records = await _syncHistoryRepository.ListRecentRecordsAsync(300, cancellationToken);
            var rows = new List<HistoryRow>(records.Count);
            foreach (var record in records)
            {
                // Older records may not contain the target used at execution time.
                var target = !string.IsNullOrWhiteSpace(record.TargetName) ? record.TargetName
                    : record.TargetId is { } id ? id.ToString("D") : Pick("目标未记录", "Target not recorded");
                rows.Add(CreateRow(record, target));
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

            if (Entries.FirstOrDefault() is { } firstVisibleRow)
            {
                await SelectRecordAsync(firstVisibleRow, cancellationToken);
            }
            else
            {
                ClearSelection();
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load history page data.");
        }
    }

    public async Task SelectRecordAsync(HistoryRow row, CancellationToken cancellationToken = default)
    {
        var selectionVersion = Interlocked.Increment(ref _selectionVersion);
        _selectedRecordId = row.Record.Id;
        SelectedEntry = row;
        RecordDetails = [];
        Snapshots = [];
        try
        {
            var record = row.Record;
            DetailRecordId = $"REC-{record.Id.ToString("N")[..8]}";
            DetailStatus = $"{UiFormat.DirectionText(record, _isChinese)} · {UiFormat.StatusText(record.Status, _isChinese)}";
            DetailStatusVariant = UiFormat.StatusVariant(record.Status);
            DetailGameName = row.Game;
            DetailSummarySubtitle = $"{UiFormat.TimeOfRecord(record.StartedAtUtc, _isChinese)} · {UiFormat.DirectionText(record, _isChinese)}";
            SnapshotGameLabel = row.Game;

            var snapshots = await Task.Run(async () =>
            {
                return await _syncHistoryRepository.ListSnapshotsAsync(record.GameInstanceId, cancellationToken);
            }, cancellationToken);

            if (selectionVersion != Interlocked.Read(ref _selectionVersion))
            {
                return;
            }

            var duration = record.CompletedAtUtc is not null
                ? (record.CompletedAtUtc.Value - record.StartedAtUtc).TotalSeconds.ToString("0") + "s"
                : "--";
            var details = new List<UiPropertyRow>
            {
                new(Pick("时间", "Time"), UiFormat.TimeOfRecord(record.StartedAtUtc, _isChinese)),
                new(Pick("耗时", "Duration"), duration),
                new(Pick("已处理文件", "Processed Files"), $"{record.ProcessedFiles}/{record.TotalFiles}"),
                new(Pick("同步目标", "Storage Target"), row.Target),
                new(Pick("远端命名空间", "Remote Namespace"), record.RemoteNamespace ?? Pick("未记录", "Not recorded")),
                new(Pick("来源", "Source"), row.Source),
                new(Pick("摘要", "Summary"), UiFormat.RecordSummary(record, _isChinese)),
            };
            if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
            {
                details.Add(new UiPropertyRow(Pick("错误", "Error"), UiFormat.RecordError(record, _isChinese)));
            }

            if (record.SnapshotId is not null)
            {
                details.Add(new UiPropertyRow(Pick("相关快照", "Related Snapshot"), $"SN-{record.SnapshotId.Value.ToString("N")[..8]}"));
            }

            RecordDetails = details;

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
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load record details.");
        }
    }

    public bool IsRecordSelected(HistoryRow row) => _selectedRecordId == row.Record.Id;

    /// <summary>
    /// Returns the file-level contents recorded for a sync record. The only persisted per-file
    /// artifact is the snapshot archive captured for the operation, so we enumerate that zip when
    /// one exists; otherwise the caller falls back to the aggregate counts in the record.
    /// </summary>
    public async Task<IReadOnlyList<UiPropertyRow>> GetRecordSnapshotFilesAsync(HistoryRow row, CancellationToken cancellationToken = default)
    {
        var record = row.Record;
        if (record.SnapshotId is null)
        {
            return [];
        }

        try
        {
            var snapshots = await _syncHistoryRepository.ListSnapshotsAsync(record.GameInstanceId, cancellationToken);
            var snapshot = snapshots.FirstOrDefault(candidate => candidate.Id == record.SnapshotId.Value);
            if (snapshot is null || !File.Exists(snapshot.ArchivePath))
            {
                return [];
            }

            return await Task.Run<IReadOnlyList<UiPropertyRow>>(() =>
            {
                using var archive = ZipFile.OpenRead(snapshot.ArchivePath);
                return archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.FullName != Snapshot.MetadataEntryName)
                    .Select(entry => new UiPropertyRow(entry.FullName, UiFormat.Bytes(entry.Length)))
                    .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to read snapshot files for record {RecordId}.", record.Id);
            return [];
        }
    }

    public async Task<string?> RestoreSnapshotAsync(SnapshotRow snapshotRow, CancellationToken cancellationToken = default)
    {
        // Restore + the reload it triggers must run as a single, non-reentrant operation: a second
        // click while one is in flight could extract two snapshots over the same local files.
        if (IsRestoreInProgress)
        {
            return null;
        }

        IsRestoreInProgress = true;
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
        finally
        {
            IsRestoreInProgress = false;
        }
    }

    partial void OnIsRestoreInProgressChanged(bool value) => OnPropertyChanged(nameof(CanRestore));
    partial void OnSnapshotsChanged(IReadOnlyList<SnapshotRow> value) => OnPropertyChanged(nameof(CanRestore));
    partial void OnSelectedEntryChanged(HistoryRow? value) { OnPropertyChanged(nameof(HasSelectedEntry)); OnPropertyChanged(nameof(NeedsSelection)); }
    partial void OnEntriesChanged(IReadOnlyList<HistoryRow> value) { OnPropertyChanged(nameof(NoMatchingRecords)); OnPropertyChanged(nameof(NeedsSelection)); }
    partial void OnIsEmptyStateChanged(bool value) { OnPropertyChanged(nameof(NoMatchingRecords)); OnPropertyChanged(nameof(NeedsSelection)); }

    partial void OnSelectedHistoryFilterChanged(string value) => ApplyFilters();

    public void FilterForGame(Guid gameId)
    {
        if (!_instanceNames.TryGetValue(gameId, out var name)) return;
        SelectedGameFilter = name;
        _navigationGameId = gameId;
        SelectedHistoryFilter = TimeRangeFilters[^1];
        ApplyFilters();
    }

    partial void OnSelectedGameFilterChanged(string value)
    {
        _navigationGameId = null;
        ApplyFilters();
    }

    partial void OnSelectedResultFilterChanged(string value) => ApplyFilters();

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<HistoryRow> filtered = _allEntries;

        if (_navigationGameId is { } gameId) filtered = filtered.Where(row => row.Record.GameInstanceId == gameId);

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
        var completedToday = _allEntries.Count(row =>
            row.Record.Status == SyncJobStatus.Completed &&
            (row.Record.CompletedAtUtc ?? row.Record.StartedAtUtc).ToLocalTime().Date == DateTimeOffset.Now.Date);
        SystemStatus = Pick(
            $"显示 {Entries.Count} / 共 {_allEntries.Count} 条记录 • 今日完成 {completedToday} 项操作",
            $"Showing {Entries.Count} of {_allEntries.Count} records • {completedToday} completed today");
        if (_selectedRecordId is { } selectedId && !Entries.Any(row => row.Record.Id == selectedId))
        {
            ClearSelection();
        }
        IsEmptyState = _allEntries.Count == 0;
    }

    private void ClearSelection()
    {
        Interlocked.Increment(ref _selectionVersion);
        _selectedRecordId = null;
        SelectedEntry = null;
        RecordDetails = [];
        Snapshots = [];
        DetailRecordId = string.Empty;
        DetailStatus = string.Empty;
        DetailStatusVariant = "ready";
        DetailGameName = string.Empty;
        DetailSummarySubtitle = string.Empty;
        SnapshotGameLabel = string.Empty;
    }

    private HistoryRow CreateRow(SyncRecord record, string targetName)
    {
        var gameName = _instanceNames.GetValueOrDefault(record.GameInstanceId, Pick("未知游戏", "Unknown game"));
        var sourceName = ResolveSourceName(record.GameInstanceId);
        return new HistoryRow(
            UiFormat.TimeOfRecord(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
            gameName,
            UiFormat.DirectionText(record, _isChinese),
            UiFormat.StatusText(record.Status, _isChinese),
            UiFormat.StatusVariant(record.Status),
            targetName,
            sourceName,
            record.TotalFiles > 0 ? $"{record.ProcessedFiles}/{record.TotalFiles}" : "--")
        {
            Record = record,
        };
    }

    private string ResolveSourceName(Guid gameInstanceId)
    {
        return _instancesById.TryGetValue(gameInstanceId, out var instance)
            ? instance.SourceProviderId
            : Pick("未知来源", "Unknown source");
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
    string Source,
    string Files)
{
    public required SyncRecord Record { get; init; }
    public string DestinationSummary => $"{Direction} · {Target}";
    public override string ToString() => $"{Game} · {DestinationSummary} · {Time} · {Result}";
}

public sealed record SnapshotRow(
    string Name,
    string StateText,
    string Timestamp,
    int FileCount,
    string Size,
    bool IsChinese)
{
    public Guid SnapshotId { get; init; }

    public string Detail => string.IsNullOrWhiteSpace(StateText)
        ? (IsChinese ? $"{FileCount} 个文件 · {Size}" : $"{FileCount} {(FileCount == 1 ? "file" : "files")} · {Size}")
        : (IsChinese ? $"{StateText} · {FileCount} 个文件 · {Size}" : $"{StateText} · {FileCount} {(FileCount == 1 ? "file" : "files")} · {Size}");
}
