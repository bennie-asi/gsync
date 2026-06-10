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
        PageSubtitle = Pick("在真实行为接入之前，先按精炼后的游戏详情界面完成静态布局。", "Static layout pass matching the refined game details screen before real behavior is wired in.");
        UploadText = Pick("上传", "Upload");
        DownloadText = Pick("下载", "Download");
        CompareText = Pick("比较", "Compare");
        RestoreText = Pick("恢复备份", "Restore Backup");
        TableSectionTitle = Pick("内容项", "Content Items");
        AddItemText = Pick("添加条目", "Add Item");
        PropertiesTitle = Pick("属性：主存档", "Properties: Main Save");
        OpenFolderText = Pick("打开目录", "Open Folder");
        TestPathText = Pick("测试路径", "Test Path");
        HistoryTabText = Pick("最近同步历史", "Recent Sync History");
        SnapshotsTabText = Pick("快照", "Snapshots");
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

        TableTitle = string.Empty;
        TableSubtitle = string.Empty;
        TableFooterText = string.Empty;

        ContentItems =
        [
            new(Pick("主存档", "Main Save"), Pick("存档", "Saves"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0000.sl2", Pick("双向", "Bidirectional"), Pick("已同步", "Synced"), "synced", true),
            new(Pick("角色槽位", "Character Slots"), Pick("存档", "Saves"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0001.sl2", Pick("双向", "Bidirectional"), Pick("本地较新", "Local Newer"), "local newer", true),
            new(Pick("用户配置", "User Config"), Pick("配置", "Config"), "~\\AppData\\Roaming\\EldenRing\\GraphicsConfig.xml", Pick("仅推送", "Push Only"), Pick("离线", "Offline"), "disabled", false),
            new(Pick("附加文件", "Extra Files"), Pick("模组/附加", "Mods/Addons"), "D:\\SteamLibrary\\steamapps\\common\\...", Pick("双向", "Bidirectional"), Pick("冲突", "Conflict"), "conflict", true),
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
            new(Pick("今天 14:32", "Today, 14:32"), Pick("上传", "Upload"), "synced", Pick("已推送到 WebDAV 云端", "Pushed to Cloud (WebDAV)"), "42.1 MB"),
            new(Pick("今天 10:15", "Today, 10:15"), Pick("下载", "Download"), "remote newer", Pick("已从 WebDAV 云端拉取", "Pulled from Cloud (WebDAV)"), "1.2 MB"),
            new(Pick("昨天 22:40", "Yesterday, 22:40"), Pick("上传", "Upload"), "synced", Pick("主存档再次推送到云端", "Pushed to Cloud (WebDAV)"), "41.8 MB"),
            new(Pick("昨天 18:05", "Yesterday, 18:05"), Pick("失败", "Failed"), "conflict", Pick("同步失败：目标离线", "Sync Failed: Target Offline"), "--"),
        ];
    }

    public string PageSubtitle { get; }
    public string UploadText { get; }
    public string DownloadText { get; }
    public string CompareText { get; }
    public string RestoreText { get; }
    public string TableSectionTitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string AddItemText { get; }
    public bool IsChinese => _isChinese;
    public string PropertiesTitle { get; }
    public string OpenFolderText { get; }
    public string TestPathText { get; }
    public string HistoryTabText { get; }
    public string SnapshotsTabText { get; }
    public string StudioText { get; }
    public string AppIdText { get; }
    public string LastPlayedText { get; }
    public string OverallStatusLabel { get; }
    public string OverallStatusText { get; }
    public string OverallStatusVariant { get; }
    public string TotalSizeLabel { get; }
    public string TotalSizeText { get; }
    public string ActiveTargetLabel { get; }
    public IReadOnlyList<ContentItemRow> ContentItems { get; }
    public IReadOnlyList<UiPropertyRow> SelectedItemProperties { get; }
    public IReadOnlyList<GameHistoryRow> RecentHistory { get; }

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
    string SizeText);
