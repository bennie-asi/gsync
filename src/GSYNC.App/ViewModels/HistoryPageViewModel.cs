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

    public HistoryPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("历史与快照", "History & Snapshots");
        PageSubtitle = Pick("精炼审计工作区，集中展示记录详情与恢复操作。", "Static refined audit workspace with persistent record details and restore actions.");
        SearchPlaceholder = Pick("搜索历史", "Search history");
        FilterPlaceholder = Pick("时间范围", "Time range");
        RefreshButtonText = Pick("刷新", "Refresh");
        RestoreSnapshotButtonText = Pick("恢复快照", "Restore Snapshot");
        HelpButtonText = Pick("帮助", "Help");
        TableTitle = Pick("最近操作", "Recent operations");
        TableSubtitle = Pick("时间、游戏、方向、结果、目标、设备与大小保持在同一审计表面内可见。", "Time, game, direction, result, target, device, and size remain visible in one audit surface.");
        DetailTitle = Pick("记录详情", "Record Details");
        SnapshotsTitle = Pick("可用快照", "Available snapshots");

        Filters = [Pick("全部事件", "All events"), Pick("上传", "Upload"), Pick("下载", "Download"), Pick("冲突", "Conflicts"), Pick("最近 7 天", "Last 7 Days")];
        _selectedHistoryFilter = Filters[0];
        DetailRecordId = "REC-2026-06-09-1042";
        DetailStatus = Pick("上传成功", "Upload Successful");
        SystemStatus = Pick("系统就绪 • 已连接 WebDAV • 今日已同步 4.2 GB", "System Ready • Connected to WebDAV • 4.2GB Synced Today");

        Entries =
        [
            new("09:42", "Elden Ring", Pick("上传", "Upload"), Pick("成功", "Success"), "WebDAV Main", "Desktop-A", "320 MB"),
            new("08:15", "Skyrim", Pick("上传", "Upload"), Pick("冲突", "Conflict"), "WebDAV Main", "Desktop-B", "1.2 GB"),
            new(Pick("昨天", "Yesterday"), "Hades", Pick("下载", "Download"), Pick("成功", "Success"), Pick("本地备份", "Local Backup"), "Deck", "42 MB"),
            new(Pick("昨天", "Yesterday"), "Cyberpunk 2077", Pick("同步", "Sync"), Pick("已取消", "Cancelled"), "NAS Archive", "Desktop-A", "18 MB"),
        ];

        RecordDetails =
        [
            new(Pick("游戏", "Game"), "Elden Ring"),
            new(Pick("时间", "Time"), Pick("今天 · 09:42", "Today · 09:42")),
            new(Pick("耗时", "Duration"), "24s"),
            new(Pick("变更文件", "Changed Files"), "2"),
            new(Pick("总大小", "Total Size"), "320 MB"),
            new(Pick("目标路径", "Target Path"), "https://dav.gsync.local/main/elden-ring"),
            new(Pick("本地来源", "Local Source"), "C:/Users/Bennie/AppData/Roaming/EldenRing"),
            new(Pick("相关快照", "Related Snapshot"), "elden-ring-pre-upload-20260609.zip"),
        ];

        Snapshots =
        [
            new(Pick("上传前快照", "Pre-upload snapshot"), Pick("Desktop-A · 318 MB", "Desktop-A · 318 MB"), Pick("今天 · 09:41", "Today · 09:41")),
            new(Pick("冲突审查快照", "Conflict review snapshot"), Pick("Desktop-B · 1.1 GB", "Desktop-B · 1.1 GB"), Pick("昨天", "Yesterday")),
            new(Pick("每日归档", "Daily archive"), "NAS Archive · 40 MB", Pick("昨天", "Yesterday")),
        ];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string FilterPlaceholder { get; }
    public string RefreshButtonText { get; }
    public string RestoreSnapshotButtonText { get; }
    public string HelpButtonText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string DetailTitle { get; }
    public string SnapshotsTitle { get; }
    public bool IsChinese => _isChinese;
    public string DetailRecordId { get; }
    public string DetailStatus { get; }
    public string SystemStatus { get; }
    public IReadOnlyList<HistoryRow> Entries { get; }
    public IReadOnlyList<UiPropertyRow> RecordDetails { get; }
    public IReadOnlyList<SnapshotRow> Snapshots { get; }
    public IReadOnlyList<string> Filters { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record HistoryRow(string Time, string Game, string Direction, string Result, string Target, string Device, string Size);
public sealed record SnapshotRow(string Name, string Detail, string Timestamp);
