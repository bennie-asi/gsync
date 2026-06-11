using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class HistoryPageViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private string _selectedHistoryFilter;

    [ObservableProperty]
    private string _selectedGameFilter;

    [ObservableProperty]
    private string _selectedResultFilter;

    public HistoryPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("历史与快照", "History & Snapshots");
        PageSubtitle = Pick("精炼审计工作区，集中展示记录详情、恢复动作与空状态恢复入口。", "Refined audit workspace with record details, restore actions, and an empty-state recovery path.");
        SearchPlaceholder = Pick("搜索记录", "Search records");
        GameFilterPlaceholder = Pick("全部游戏", "All Games");
        ResultFilterPlaceholder = Pick("全部结果", "All Results");
        TimeRangePlaceholder = Pick("今天", "Today");
        RefreshButtonText = Pick("刷新", "Refresh");
        RestoreSnapshotButtonText = Pick("恢复快照", "Restore Snapshot");
        LogsButtonText = Pick("日志", "Logs");
        HelpButtonText = Pick("帮助", "Help");
        TableTitle = Pick("同步记录", "Sync Records");
        TableSubtitle = Pick("左侧主表格保持高密度审计视图，右侧持续显示选中记录和快照。", "The left table stays dense while the right pane keeps the selected record and snapshots visible.");
        DetailTitle = Pick("记录详情", "Record Details");
        DetailSubtitle = Pick("右侧持续保留选中记录、恢复动作与相关快照。", "The right pane keeps the selected record, restore actions, and related snapshots visible.");
        DetailSummaryTitle = Pick("选中记录", "Selected Record");
        DetailSummarySubtitle = Pick("时间、路径与关联快照在同一处查看。", "Time, paths, and the related snapshot stay together.");
        DetailRecordId = "REC-992a";
        DetailStatus = Pick("上传成功", "Upload Successful");
        DetailStatusVariant = "synced";
        DetailGameName = "Elden Ring";
        SystemStatus = Pick("系统就绪 • 已连接 WebDAV • 今日已同步 4.2 GB", "System Ready • Connected to WebDAV • 4.2GB Synced Today");
        SnapshotsTitle = Pick("可用快照", "Available Snapshots");
        SnapshotsSubtitle = Pick("最近快照保持紧凑显示，便于直接恢复。", "Recent snapshots stay compact and ready to restore.");
        SnapshotGameLabel = "Elden Ring";
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
            Pick("确认 WebDAV 与本地路径都保持可用。", "Confirm that both WebDAV and the local path stay available."),
            Pick("完成首次同步后回到此页检查记录与快照。", "Come back after the first sync to review records and snapshots."),
        ];

        GameFilters =
        [
            Pick("全部游戏", "All Games"),
            "Elden Ring",
            "Skyrim",
            "Hades",
            Pick("星露谷", "Stardew Valley"),
        ];
        ResultFilters =
        [
            Pick("全部结果", "All Results"),
            Pick("成功", "Success"),
            Pick("冲突", "Conflict"),
            Pick("已取消", "Cancelled"),
        ];
        TimeRangeFilters =
        [
            Pick("今天", "Today"),
            Pick("最近 7 天", "Last 7 Days"),
            Pick("最近 30 天", "Last 30 Days"),
            Pick("空状态预览", "Empty State Preview"),
        ];

        _selectedGameFilter = GameFilters[0];
        _selectedResultFilter = ResultFilters[0];
        _selectedHistoryFilter = TimeRangeFilters[0];
        _isEmptyState = false;

        Entries =
        [
            new(Pick("今天 14:32", "Today 14:32"), "Elden Ring", Pick("上传", "Upload"), Pick("成功", "Success"), "synced", "WebDAV Main", "DESKTOP-MAIN", "42.1 MB"),
            new(Pick("今天 12:15", "Today 12:15"), "Skyrim", Pick("上传", "Upload"), Pick("冲突", "Conflict"), "conflict", "Local NAS", "LAPTOP-PRO", "15.4 MB"),
            new(Pick("昨天 22:45", "Yesterday 22:45"), "Hades", Pick("下载", "Download"), Pick("成功", "Success"), "remote newer", "WebDAV Main", "DESKTOP-MAIN", "2.8 MB"),
            new(Pick("昨天 18:30", "Yesterday 18:30"), Pick("星露谷", "Stardew Valley"), Pick("同步", "Sync"), Pick("已取消", "Cancelled"), "disabled", "Local Folder", "DESKTOP-MAIN", "--"),
        ];

        RecordDetails =
        [
            new(Pick("时间", "Time"), Pick("今天 14:32:11", "Today 14:32:11")),
            new(Pick("耗时", "Duration"), "14s"),
            new(Pick("变更文件", "Changed Files"), "12"),
            new(Pick("总大小", "Total Size"), "42.1 MB"),
            new(Pick("目标路径", "Target Path"), "/WebDAV/Saves/EldenRing/76561198000000000/"),
            new(Pick("本地来源", "Local Source"), "C:/Users/Admin/AppData/Roaming/EldenRing/"),
            new(Pick("相关快照", "Related Snapshot"), "SN-8821"),
        ];

        Snapshots =
        [
            new("SN-8821", Pick("当前", "Current"), Pick("今天 14:32", "Today 14:32"), "DESKTOP-MAIN", "42.1 MB"),
            new("SN-8755", string.Empty, Pick("昨天 20:11", "Yesterday 20:11"), "LAPTOP-PRO", "41.8 MB"),
        ];
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
    public string DetailRecordId { get; }
    public string DetailStatus { get; }
    public string DetailStatusVariant { get; }
    public string DetailGameName { get; }
    public string SystemStatus { get; }
    public string SnapshotsTitle { get; }
    public string SnapshotsSubtitle { get; }
    public string SnapshotGameLabel { get; }
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
    public IReadOnlyList<HistoryRow> Entries { get; }
    public IReadOnlyList<UiPropertyRow> RecordDetails { get; }
    public IReadOnlyList<SnapshotRow> Snapshots { get; }
    public IReadOnlyList<string> GameFilters { get; }
    public IReadOnlyList<string> ResultFilters { get; }
    public IReadOnlyList<string> TimeRangeFilters { get; }
    public IReadOnlyList<string> EmptyStateRecoverySteps { get; }

    partial void OnSelectedHistoryFilterChanged(string value)
    {
        IsEmptyState = string.Equals(value, TimeRangeFilters[^1], StringComparison.Ordinal);
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
    string Size);

public sealed record SnapshotRow(
    string Name,
    string StateText,
    string Timestamp,
    string Device,
    string Size)
{
    public string Detail => string.IsNullOrWhiteSpace(StateText) ? $"{Device} · {Size}" : $"{StateText} · {Device} · {Size}";
}
