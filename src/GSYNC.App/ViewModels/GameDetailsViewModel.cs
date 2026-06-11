using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private string _title = "Elden Ring";

    [ObservableProperty]
    private string _activeTarget;

    public GameDetailsViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        PageSubtitle = Pick("精炼后的详情工作区，固定展示游戏上下文、内容项、历史与右侧检查面板。", "Refined detail workspace with persistent game context, content items, history, and a right-hand inspection pane.");
        PrimaryActionLabel = Pick("快速操作", "Quick Actions");
        UploadText = Pick("上传", "Upload");
        DownloadText = Pick("下载", "Download");
        CompareText = Pick("比较", "Compare");
        RestoreText = Pick("恢复备份", "Restore Backup");
        TableSectionTitle = Pick("内容项", "Content Items");
        TableSectionSubtitle = Pick("存档、配置与附加内容保持在高密度表格中，右侧检查区负责属性与路径验证。", "Saves, config, and auxiliary content stay in a dense table while the right pane handles inspection and path validation.");
        AddItemText = Pick("添加条目", "Add Item");
        PropertiesTitle = Pick("检查器：主存档", "Inspector: Main Save");
        PropertiesSubtitle = Pick("所选内容项的路径模板、策略与可移植性检查结果。", "Path template, policy, and portability checks for the selected content item.");
        OpenFolderText = Pick("打开目录", "Open Folder");
        TestPathText = Pick("测试路径", "Test Path");
        HistorySectionTitle = Pick("历史与快照", "History & Snapshots");
        HistorySectionSubtitle = Pick("底部区域保留最近同步记录与可恢复快照。", "The bottom section keeps recent sync activity and recoverable snapshots visible.");
        HistoryTabText = Pick("最近同步历史", "Recent Sync History");
        SnapshotsTabText = Pick("快照", "Snapshots");
        HistoryFeedTitle = Pick("最近活动", "Recent Activity");
        SnapshotFeedTitle = Pick("可恢复快照", "Recoverable Snapshots");
        StudioText = "FromSoftware";
        AppIdText = "1245620";
        LastPlayedText = Pick("2 小时前", "2 hours ago");
        OverallStatusLabel = Pick("整体状态", "Overall Status");
        OverallStatusText = Pick("检测到冲突", "Conflicts Detected");
        OverallStatusVariant = "conflict";
        TotalSizeLabel = Pick("总大小", "Total Size");
        TotalSizeText = "142.5 MB";
        ActiveTargetLabel = Pick("当前目标", "Active Target");
        _activeTarget = Pick("WebDAV（主目标）", "WebDAV (Primary)");
        OverviewMetricsTitle = Pick("上下文摘要", "Context Summary");
        InspectorStatusTitle = Pick("检查状态", "Inspection Status");
        InspectorStatusMessage = Pick("此内容项仍保留在 refined 布局的右侧检查区中，便于在同步前核对模板与策略。", "This content item remains pinned in the refined inspector so you can validate templates and policy before syncing.");

        TableTitle = string.Empty;
        TableSubtitle = string.Empty;
        TableFooterText = Pick("4 个内容项 · 1 个冲突 · 1 个仅推送条目", "4 items · 1 conflict · 1 push-only item");

        ContentItems =
        [
            new(Pick("主存档", "Main Save"), Pick("存档", "Saves"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0000.sl2", Pick("双向", "Bidirectional"), Pick("已同步", "Synced"), "synced", true),
            new(Pick("角色槽位", "Character Slots"), Pick("存档", "Saves"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0001.sl2", Pick("双向", "Bidirectional"), Pick("本地较新", "Local Newer"), "local newer", true),
            new(Pick("用户配置", "User Config"), Pick("配置", "Config"), "~\\AppData\\Roaming\\EldenRing\\GraphicsConfig.xml", Pick("仅推送", "Push Only"), Pick("离线", "Offline"), "disabled", false),
            new(Pick("附加文件", "Extra Files"), Pick("模组/附加", "Mods/Addons"), "D:\\SteamLibrary\\steamapps\\common\\...", Pick("双向", "Bidirectional"), Pick("冲突", "Conflict"), "conflict", true),
        ];

        OverviewMetrics =
        [
            new(Pick("平台", "Platform"), "Steam"),
            new(Pick("活动配置", "Active Profile"), Pick("Windows 主桌面", "Windows Primary Desktop")),
            new(Pick("上次成功同步", "Last Successful Sync"), Pick("今天 10:15", "Today 10:15")),
            new(Pick("冲突策略", "Conflict Policy"), Pick("人工合并", "Manual Merge")),
        ];

        SelectedItemProperties =
        [
            new(Pick("条目名称", "Item Name"), Pick("主存档", "Main Save")),
            new(Pick("路径模板", "Path Template"), "%APPDATA%/EldenRing/<SteamId>/ER0000.sl2"),
            new(Pick("包含/排除模式", "Include/Exclude Patterns"), Pick("+ *.sl2\n- backup/*", "+ *.sl2\n- backup/*")),
            new(Pick("冲突策略", "Conflict Policy"), Pick("人工合并", "Manual Merge")),
            new(Pick("可移植性状态", "Portability Status"), Pick("通用 · Win/Lin/Mac 变量均可解析", "Universal · Variables resolve on Win/Lin/Mac")),
        ];

        RecentHistory =
        [
            new(Pick("14:32", "14:32"), Pick("上传", "Upload"), "synced", Pick("已推送到 WebDAV 云端", "Pushed to Cloud (WebDAV)"), "42.1 MB"),
            new(Pick("10:15", "10:15"), Pick("下载", "Download"), "remote newer", Pick("已从 WebDAV 云端拉取", "Pulled from Cloud (WebDAV)"), "1.2 MB"),
            new(Pick("昨天", "Yesterday"), Pick("上传", "Upload"), "synced", Pick("主存档再次推送到云端", "Pushed the main save again"), "41.8 MB"),
            new(Pick("昨天", "Yesterday"), Pick("失败", "Failed"), "conflict", Pick("同步失败：目标离线", "Sync Failed: Target Offline"), "--"),
        ];

        RecentActivityItems =
        [
            new(Pick("已上传", "Uploaded"), Pick("已推送到 WebDAV 云端 · 42.1 MB", "Pushed to Cloud (WebDAV) · 42.1 MB"), Pick("14:32", "14:32")),
            new(Pick("已下载", "Downloaded"), Pick("已从 WebDAV 云端拉取 · 1.2 MB", "Pulled from Cloud (WebDAV) · 1.2 MB"), Pick("10:15", "10:15")),
            new(Pick("再次上传", "Uploaded again"), Pick("主存档再次推送到云端 · 41.8 MB", "Pushed the main save again · 41.8 MB"), Pick("昨天", "Yesterday")),
            new(Pick("同步失败", "Sync failed"), Pick("目标离线，需要先检查连接状态", "Target offline. Check connectivity before syncing again."), Pick("昨天", "Yesterday"), true),
        ];

        Snapshots =
        [
            new("SN-8821", Pick("当前", "Current"), Pick("今天 14:32", "Today 14:32"), "DESKTOP-MAIN", "42.1 MB"),
            new("SN-8755", string.Empty, Pick("昨天 20:11", "Yesterday 20:11"), "LAPTOP-PRO", "41.8 MB"),
        ];
    }

    public string PageSubtitle { get; }
    public string PrimaryActionLabel { get; }
    public string UploadText { get; }
    public string DownloadText { get; }
    public string CompareText { get; }
    public string RestoreText { get; }
    public string TableSectionTitle { get; }
    public string TableSectionSubtitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string AddItemText { get; }
    public bool IsChinese => _isChinese;
    public string PropertiesTitle { get; }
    public string PropertiesSubtitle { get; }
    public string OpenFolderText { get; }
    public string TestPathText { get; }
    public string HistorySectionTitle { get; }
    public string HistorySectionSubtitle { get; }
    public string HistoryTabText { get; }
    public string SnapshotsTabText { get; }
    public string HistoryFeedTitle { get; }
    public string SnapshotFeedTitle { get; }
    public string StudioText { get; }
    public string AppIdText { get; }
    public string LastPlayedText { get; }
    public string OverallStatusLabel { get; }
    public string OverallStatusText { get; }
    public string OverallStatusVariant { get; }
    public string TotalSizeLabel { get; }
    public string TotalSizeText { get; }
    public string ActiveTargetLabel { get; }
    public string OverviewMetricsTitle { get; }
    public string InspectorStatusTitle { get; }
    public string InspectorStatusMessage { get; }
    public IReadOnlyList<UiPropertyRow> OverviewMetrics { get; }
    public IReadOnlyList<ContentItemRow> ContentItems { get; }
    public IReadOnlyList<UiPropertyRow> SelectedItemProperties { get; }
    public IReadOnlyList<GameHistoryRow> RecentHistory { get; }
    public IReadOnlyList<UiActivityItem> RecentActivityItems { get; }
    public IReadOnlyList<SnapshotRow> Snapshots { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record ContentItemRow(
    string Name,
    string Category,
    string ResolvedPath,
    string Policy,
    string Status,
    string StatusVariant,
    bool Enabled);

public sealed record GameHistoryRow(
    string Timestamp,
    string ActionText,
    string ActionVariant,
    string SummaryText,
    string SizeText)
{
    public string PrimaryText => ActionText;
    public string SecondaryText => $"{SummaryText} · {SizeText}";
}
