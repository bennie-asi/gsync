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
        SyncText = Pick("同步", "Sync");
        UploadText = Pick("上传", "Upload");
        DownloadText = Pick("下载", "Download");
        CompareText = Pick("比较", "Compare");
        RestoreText = Pick("恢复", "Restore");
        SummaryTitle = Pick("应用摘要", "Application summary");
        TableTitle = Pick("内容项", "Content Items");
        TableSubtitle = Pick("条目、分类、解析路径、策略、状态与开关都与精炼后的详情工作区保持一致。", "Item, category, resolved path, policy, status, and toggle mirror the refined detail workspace.");
        TableFooterText = Pick("即使行为尚未实现，添加条目操作仍保持可见。", "Add Item remains visible even before behavior is implemented.");
        AddItemText = Pick("添加条目", "Add Item");
        PropertiesTitle = Pick("属性：主存档", "Properties: Main Save");
        ActivityTitle = Pick("近期活动", "Recent activity");
        _activeTarget = Pick("WebDAV（主目标）", "WebDAV (Primary)");

        OverviewFacts =
        [
            new(Pick("应用 ID", "App ID"), "1245620"),
            new(Pick("工作室", "Studio"), "FromSoftware"),
            new(Pick("最近游玩", "Last Played"), Pick("2 小时前", "2 hours ago")),
            new(Pick("整体状态", "Overall Status"), Pick("检测到冲突", "Conflicts detected")),
            new(Pick("总大小", "Total Size"), "142.5 MB"),
            new(Pick("当前目标", "Active Target"), _activeTarget),
        ];

        ContentItems =
        [
            new(Pick("主存档", "Main Save"), Pick("存档", "Save"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0000.sl2", Pick("双向", "Bidirectional"), Pick("已同步", "Synced"), true),
            new(Pick("角色槽位", "Character Slots"), Pick("存档", "Save"), "~\\AppData\\Roaming\\EldenRing\\...\\ER0001.sl2", Pick("双向", "Bidirectional"), Pick("本地较新", "Local newer"), true),
            new(Pick("用户配置", "User Config"), Pick("配置", "Config"), "~\\AppData\\Roaming\\EldenRing\\GraphicsConfig.xml", Pick("仅推送", "Push only"), Pick("离线", "Offline"), true),
            new(Pick("模组/附加内容", "Mods/Addons"), Pick("扩展", "Extra"), "D:\\Games\\Elden Ring\\mods", Pick("人工审查", "Manual review"), Pick("冲突", "Conflict"), false),
        ];

        SelectedItemProperties =
        [
            new(Pick("路径模板", "Path Template"), "%APPDATA%/EldenRing/<SteamId>/ER0000.sl2"),
            new(Pick("包含/排除模式", "Include/Exclude Patterns"), Pick("包含 *.sl2 · 排除 backup/*", "Include *.sl2 · Exclude backup/*")),
            new(Pick("冲突策略", "Conflict Policy"), Pick("提示用户", "Prompt User")),
            new(Pick("可移植性状态", "Portability Status"), Pick("通过核心 Windows 变量实现可移植", "Portable using core Windows variables")),
            new(Pick("通用变量", "Universal Variables"), "%APPDATA%, %DOCUMENTS%, %STEAM_LIBRARY%"),
        ];

        RecentActivity =
        [
            new(Pick("快照已推送", "Snapshot pushed"), Pick("主存档已上传到 WebDAV 主目标", "Main Save uploaded to WebDAV primary target"), Pick("2 分钟前", "2m ago")),
            new(Pick("快照已拉取", "Snapshot pulled"), Pick("角色槽位已从远端存储刷新", "Character Slots refreshed from remote storage"), Pick("昨天", "Yesterday")),
            new(Pick("同步失败", "Sync failed"), Pick("配置校验期间目标离线", "Target offline during config verification"), Pick("2 天前", "2 days ago"), true),
        ];
    }

    public string PageSubtitle { get; }
    public string SyncText { get; }
    public string UploadText { get; }
    public string DownloadText { get; }
    public string CompareText { get; }
    public string RestoreText { get; }
    public string SummaryTitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TableFooterText { get; }
    public string AddItemText { get; }
    public bool IsChinese => _isChinese;
    public string PropertiesTitle { get; }
    public string ActivityTitle { get; }
    public IReadOnlyList<UiPropertyRow> OverviewFacts { get; }
    public IReadOnlyList<ContentItemRow> ContentItems { get; }
    public IReadOnlyList<UiPropertyRow> SelectedItemProperties { get; }
    public IReadOnlyList<UiActivityItem> RecentActivity { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record ContentItemRow(
    string Name,
    string Category,
    string ResolvedPath,
    string Policy,
    string Status,
    bool Enabled);
