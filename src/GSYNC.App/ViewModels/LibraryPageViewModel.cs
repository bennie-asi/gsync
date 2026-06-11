using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class LibraryPageViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private bool _isSyncInProgress;

    [ObservableProperty]
    private string _selectedStatus;

    public LibraryPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

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

        Games =
        [
            new("Elden Ring", "Steam", "4", Pick("已同步", "Synced"), Pick("已同步", "Synced"), Pick("2 分钟前", "2 mins ago"), "WebDAV-Main", true, "synced", "synced"),
            new("Stardew Valley", Pick("自定义", "Custom"), "12", Pick("本地较新", "Local newer"), Pick("远端较新", "Remote newer"), Pick("1 小时前", "1 hour ago"), "WebDAV-Main", false, "local newer", "remote newer"),
            new("Skyrim", "Epic", "1", Pick("冲突", "Conflict"), Pick("冲突", "Conflict"), Pick("昨天", "Yesterday"), "OneDrive", false, "conflict", "conflict"),
            new("Hades", "Steam", "6", Pick("已同步", "Synced"), Pick("已同步", "Synced"), Pick("5 小时前", "5 hours ago"), "WebDAV-Main", false, "synced", "synced"),
        ];

        OverviewMetrics =
        [
            new(Pick("游戏总数", "Total Games"), "42"),
            new(Pick("总大小", "Total Size"), "1.4 GB"),
            new(Pick("冲突数", "Conflicts"), "1"),
            new(Pick("待处理", "Pending"), "3"),
        ];

        Stats =
        [
            new(Pick("主要目标", "Primary target"), "WebDAV-Main"),
            new(Pick("最近同步窗口", "Last sync window"), Pick("今天 · 09:42", "Today · 09:42")),
            new(Pick("冲突策略", "Conflict policy"), Pick("覆盖前提示", "Prompt before overwrite")),
            new(Pick("保留期", "Retention"), Pick("90 天", "90 days")),
        ];

        Activity =
        [
            new(Pick("Elden Ring 已同步", "Elden Ring synced"), Pick("已传输 2 个文件", "2 files transferred"), Pick("2 分钟前", "2m ago")),
            new(Pick("Skyrim 冲突", "Skyrim conflict"), Pick("远端已分叉，需要人工解决", "Remote diverged and requires resolution"), Pick("12 分钟前", "12m ago")),
            new(Pick("Hades 已同步", "Hades synced"), Pick("未检测到变化", "No changes detected"), Pick("35 分钟前", "35m ago")),
            new(Pick("Stardew Valley 已上传", "Stardew Valley uploaded"), Pick("存档集已推送到 WebDAV-Main", "Save set pushed to WebDAV-Main"), Pick("1 小时前", "1h ago")),
            new(Pick("Cyberpunk 2077 已检查", "Cyberpunk 2077 reviewed"), Pick("等待远端元数据刷新", "Pending remote metadata refresh"), Pick("2 小时前", "2h ago")),
            new(Pick("Balatro 已同步", "Balatro synced"), Pick("配置与存档文件完全匹配", "Config and save files matched"), Pick("3 小时前", "3h ago")),
            new(Pick("Fallout 4 警告", "Fallout 4 warning"), Pick("覆盖前已创建快照", "Snapshot created before overwrite"), Pick("昨天", "Yesterday")),
            new(Pick("Slay the Spire 已同步", "Slay the Spire synced"), Pick("未检测到变化", "No changes detected"), Pick("昨天", "Yesterday")),
            new(Pick("Terraria 归档已更新", "Terraria archive updated"), Pick("远端目标保留策略已刷新", "Remote target retention refreshed"), Pick("2 天前", "2 days ago"), true),
        ];
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
    public IReadOnlyList<LibraryGameRow> Games { get; }
    public IReadOnlyList<LibraryOverviewMetric> OverviewMetrics { get; }
    public IReadOnlyList<LibraryStat> Stats { get; }
    public IReadOnlyList<UiActivityItem> Activity { get; }

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
    string RemoteStatusVariant = "ready");

public sealed record LibraryStat(string Label, string Value);
