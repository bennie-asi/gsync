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

    [ObservableProperty]
    private string _searchText = string.Empty;

    public SyncTargetsPageViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("同步目标", "Sync Targets");
        PageSubtitle = Pick("精炼后的桌面目标管理布局，保留固定详情面板与批量操作。", "Static desktop-refined target management layout with persistent detail panel and bulk actions.");
        SearchPlaceholder = Pick("搜索目标", "Search targets");
        FilterPlaceholder = Pick("状态", "Status");
        RefreshText = Pick("刷新", "Refresh");
        TestSelectedText = Pick("测试所选", "Test Selected");
        AddTargetText = Pick("添加目标", "Add Target");
        HelpText = Pick("帮助", "Help");
        TargetsCountText = Pick("目标 (3)", "Targets (3)");
        TargetsFooterLeftText = Pick("已配置 3 个", "3 configured");
        TargetsFooterRightText = Pick("最近检查：2 分钟前", "Last check: 2m ago");

        PaneTitle = Pick("目标属性：WebDAV Main", "Target Properties: WebDAV Main");
        SelectedTargetName = "WebDAV Main";
        SelectedTargetStatus = Pick("连接失败", "Connection Failed");
        SelectedTargetStatusVariant = "conflict";
        LastCheckedText = Pick("最近检查：今天 14:22", "Last checked: Oct 24, 2023 14:22");

        FailureTitle = Pick("认证失败或端点不可达", "Authentication failed or endpoint unreachable");
        FailureMessage = Pick("远端服务拒绝了凭据，或网络握手超时。请确认端点可访问、凭据正确，并检查当前网络/VPN/防火墙状态。", "The remote server rejected the credentials or the network handshake timed out. Ensure the endpoint is accessible from your current network.");
        FailureCodeText = "AUTH_401_UNREACHABLE";

        ConnectionDetailsTitle = Pick("连接详情", "Connection Details");
        ActiveBindingsTitle = Pick("当前绑定", "Active Bindings");
        ActiveBindingsSummaryText = Pick("当前使用于：", "Currently used by:");
        TroubleshootingTitle = Pick("故障排查", "Troubleshooting");
        StorageTitle = Pick("最近一次已知存储占用", "Storage Usage (Last Known)");

        RemoveTargetText = Pick("移除目标", "Remove Target");
        OpenRootText = Pick("打开根目录", "Open Root");
        TestConnectionText = Pick("测试连接", "Test Connection");
        SaveChangesText = Pick("保存更改", "Save Changes");
        EditCredentialsText = Pick("编辑凭据", "Edit Credentials");
        OpenLogsText = Pick("打开日志", "Open Logs");
        RetryConnectionText = Pick("重试连接", "Retry Connection");

        Filters =
        [
            Pick("全部目标", "All targets"),
            Pick("已连接", "Connected"),
            Pick("就绪", "Ready"),
            Pick("失败", "Failed"),
        ];
        _selectedTargetFilter = Filters[0];
        _showFailureState = true;

        Targets =
        [
            new("W", "WebDAV Main", "https://dav.gsync.local/main", Pick("失败", "Failed"), "conflict", true),
            new("L", Pick("本地备份", "Local Backup"), "D:\\Saves\\GSYNC", Pick("就绪", "Ready"), "ready", false),
            new("O", "OneDrive Preview", "/Apps/GSYNC", Pick("离线", "Offline"), "disabled", false),
        ];

        ConnectionFields =
        [
            new(Pick("显示名称", "Display Name"), "WebDAV Main"),
            new(Pick("提供程序类型", "Provider Type"), "WebDAV"),
            new(Pick("端点 URL", "Endpoint URL"), "https://dav.gsync.local/main"),
            new(Pick("用户名", "Username"), "gsync_service"),
            new(Pick("基础路径", "Base Path"), "/sync_data/saves"),
            new(Pick("凭据存储", "Credential Storage"), Pick("系统钥匙串", "System Keychain")),
            new(Pick("默认目标", "Default Target"), Pick("当前活动", "Currently Active")),
        ];

        ActiveBindings =
        [
            new("Elden Ring", Pick("主存档 + 配置", "Primary saves + config")),
            new("Hades", Pick("云端镜像", "Cloud mirror")),
            new(Pick("星露谷", "Stardew Valley"), Pick("跨设备归档", "Cross-device archive")),
        ];

        TroubleshootingSteps =
        [
            Pick("确认 WebDAV URL 正确，并且包含正确的协议前缀（https://）。", "Verify that the WebDAV URL is correct and includes the proper protocol (https://)."),
            Pick("检查本地网络连接，以及是否存在 VPN / 防火墙限制。", "Check your local network connection and any active VPN / Firewall rules."),
            Pick("重新输入凭据，确认不存在特殊字符编码问题。", "Re-enter your credentials to ensure no special character encoding issues."),
        ];

        StorageFacts =
        [
            new(Pick("已用空间", "Used Space"), "184.2 GB"),
            new(Pick("总容量", "Total Capacity"), "2.4 TB"),
            new(Pick("可用空间", "Available"), "2.22 TB"),
            new(Pick("主节点", "Primary Node"), "US-East-1"),
        ];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string FilterPlaceholder { get; }
    public string RefreshText { get; }
    public string TestSelectedText { get; }
    public string AddTargetText { get; }
    public string HelpText { get; }
    public string TargetsCountText { get; }
    public string TargetsFooterLeftText { get; }
    public string TargetsFooterRightText { get; }
    public string PaneTitle { get; }
    public string SelectedTargetName { get; }
    public string SelectedTargetStatus { get; }
    public string SelectedTargetStatusVariant { get; }
    public string LastCheckedText { get; }
    public string FailureTitle { get; }
    public string FailureMessage { get; }
    public string FailureCodeText { get; }
    public string ConnectionDetailsTitle { get; }
    public string ActiveBindingsTitle { get; }
    public string ActiveBindingsSummaryText { get; }
    public string TroubleshootingTitle { get; }
    public string StorageTitle { get; }
    public string RemoveTargetText { get; }
    public string OpenRootText { get; }
    public string TestConnectionText { get; }
    public string SaveChangesText { get; }
    public string EditCredentialsText { get; }
    public string OpenLogsText { get; }
    public string RetryConnectionText { get; }
    public bool IsChinese => _isChinese;
    public IReadOnlyList<SyncTargetRow> Targets { get; }
    public IReadOnlyList<UiPropertyRow> ConnectionFields { get; }
    public IReadOnlyList<UiBindingUsage> ActiveBindings { get; }
    public IReadOnlyList<string> TroubleshootingSteps { get; }
    public IReadOnlyList<UiPropertyRow> StorageFacts { get; }
    public IReadOnlyList<string> Filters { get; }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record SyncTargetRow(
    string DefinitionLabel,
    string Name,
    string Detail,
    string Status,
    string StatusVariant,
    bool IsSelected);
