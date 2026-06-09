using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class SyncTargetsPageViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private bool _showFailureState;

    [ObservableProperty]
    private string _selectedTargetFilter;

    public SyncTargetsPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("同步目标", "Sync Targets");
        PageSubtitle = Pick("精炼后的桌面目标管理布局，保留固定详情面板与批量操作。", "Static desktop-refined target management layout with persistent detail panel and bulk actions.");
        SearchPlaceholder = Pick("搜索目标", "Search targets");
        FilterPlaceholder = Pick("状态", "Status");
        TestSelectedText = Pick("测试所选", "Test Selected");
        AddTargetText = Pick("添加目标", "Add Target");
        ToolsText = Pick("工具", "Tools");
        TableTitle = Pick("已配置目标", "Configured targets");
        TableSubtitle = Pick("定义、名称与状态保持紧凑展示，同时右侧面板显示当前目标编辑器。", "Definition, name, and status stay compact while the right pane exposes the active target editor.");
        TargetPropertiesTitle = Pick("目标属性：WebDAV Main", "Target Properties: WebDAV Main");
        ConnectionDetailsText = Pick("连接详情", "Connection Details");
        ActiveBindingsText = Pick("当前绑定", "Active Bindings");
        ActiveBindingsSummaryText = Pick("3 个游戏 · 当前使用于：", "3 Games · Currently used by:");
        RemoveTargetText = Pick("移除目标", "Remove Target");
        OpenRootText = Pick("打开根目录", "Open Root");
        TestConnectionText = Pick("测试连接", "Test Connection");
        SaveChangesText = Pick("保存更改", "Save Changes");

        Filters = [Pick("全部目标", "All targets"), Pick("已连接", "Connected"), Pick("就绪", "Ready"), Pick("待处理", "Pending")];
        _selectedTargetFilter = Filters[0];

        SelectedTargetName = "WebDAV Main";
        SelectedTargetStatus = Pick("已连接 • 2 分钟前检查", "Connected • Checked 2 mins ago");
        TargetsSummary = Pick("已配置 3 个 · 最近检查：2 分钟前", "3 configured · Last check: 2m ago");

        Targets =
        [
            new("cloud", "WebDAV Main", "https://dav.gsync.local/main", Pick("已连接", "Connected")),
            new("folder", Pick("本地备份", "Local Backup"), "D:\\Saves\\GSYNC", Pick("就绪", "Ready")),
            new("cloud_circle", "OneDrive Preview", "/Apps/GSYNC", Pick("待处理", "Pending")),
        ];

        TargetProperties =
        [
            new(Pick("显示名称", "Display Name"), "WebDAV Main"),
            new(Pick("提供程序类型", "Provider Type"), "WebDAV"),
            new(Pick("端点 URL", "Endpoint URL"), "https://dav.gsync.local/main"),
            new(Pick("用户名", "Username"), "bennie@desktop"),
            new(Pick("基础路径", "Base Path"), "/desktop-main"),
            new(Pick("凭据存储", "Credential Storage"), Pick("系统钥匙串", "System Keychain")),
            new(Pick("默认目标", "Default Target"), Pick("当前活动", "Currently Active")),
        ];

        ActiveBindings =
        [
            new("Elden Ring", Pick("主存档 + 配置", "Primary saves + config")),
            new("Hades", Pick("云端镜像", "Cloud mirror")),
            new("Stardew Valley", Pick("跨设备归档", "Cross-device archive")),
        ];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string FilterPlaceholder { get; }
    public string TestSelectedText { get; }
    public string AddTargetText { get; }
    public string ToolsText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string TargetPropertiesTitle { get; }
    public string ConnectionDetailsText { get; }
    public string ActiveBindingsText { get; }
    public string ActiveBindingsSummaryText { get; }
    public string RemoveTargetText { get; }
    public string OpenRootText { get; }
    public string TestConnectionText { get; }
    public string SaveChangesText { get; }
    public bool IsChinese => _isChinese;
    public string SelectedTargetName { get; }
    public string SelectedTargetStatus { get; }
    public string TargetsSummary { get; }
    public IReadOnlyList<SyncTargetRow> Targets { get; }
    public IReadOnlyList<UiPropertyRow> TargetProperties { get; }
    public IReadOnlyList<UiBindingUsage> ActiveBindings { get; }
    public IReadOnlyList<string> Filters { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record SyncTargetRow(string DefinitionIcon, string Name, string Detail, string Status);
