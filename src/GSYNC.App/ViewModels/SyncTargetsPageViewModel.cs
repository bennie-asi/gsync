using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Storage.Services;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class SyncTargetsPageViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly SyncTargetStore _syncTargetStore;
    private readonly IEnumerable<IStorageProvider> _storageProviders;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly Dictionary<Guid, (bool Success, string? Error, DateTimeOffset CheckedAt)> _testResults = [];

    private IReadOnlyList<SyncTargetRow> _allTargets = [];

    [ObservableProperty]
    private bool _showFailureState;

    [ObservableProperty]
    private string _selectedTargetFilter = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SyncTargetRow> _targets = [];

    [ObservableProperty]
    private string _targetsCountText = string.Empty;

    [ObservableProperty]
    private string _targetsFooterLeftText = string.Empty;

    [ObservableProperty]
    private string _targetsFooterRightText = string.Empty;

    [ObservableProperty]
    private string _paneTitle = string.Empty;

    [ObservableProperty]
    private string _selectedTargetName = string.Empty;

    [ObservableProperty]
    private string _selectedTargetStatus = string.Empty;

    [ObservableProperty]
    private string _selectedTargetStatusVariant = string.Empty;

    [ObservableProperty]
    private string _lastCheckedText = string.Empty;

    [ObservableProperty]
    private string _failureTitle = string.Empty;

    [ObservableProperty]
    private string _failureMessage = string.Empty;

    [ObservableProperty]
    private string _failureCodeText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _connectionFields = [];

    [ObservableProperty]
    private IReadOnlyList<UiBindingUsage> _activeBindings = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _storageFacts = [];

    public SyncTargetsPageViewModel(
        ILocalizationService localizationService,
        SyncTargetStore syncTargetStore,
        IEnumerable<IStorageProvider> storageProviders,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _syncTargetStore = syncTargetStore;
        _storageProviders = storageProviders;
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;

        PageTitle = Pick("同步目标", "Sync Targets");
        PageSubtitle = Pick("精炼后的桌面目标管理布局，保留固定详情面板与批量操作。", "Static desktop-refined target management layout with persistent detail panel and bulk actions.");
        SearchPlaceholder = Pick("搜索目标", "Search targets");
        FilterPlaceholder = Pick("状态", "Status");
        RefreshText = Pick("刷新", "Refresh");
        TestSelectedText = Pick("测试所选", "Test Selected");
        AddTargetText = Pick("添加目标", "Add Target");
        HelpText = Pick("帮助", "Help");

        ConnectionDetailsTitle = Pick("连接详情", "Connection Details");
        ActiveBindingsTitle = Pick("当前绑定", "Active Bindings");
        ActiveBindingsSummaryText = Pick("当前使用于：", "Currently used by:");
        TroubleshootingTitle = Pick("故障排查", "Troubleshooting");
        StorageTitle = Pick("存储占用", "Storage Usage");

        RemoveTargetText = Pick("移除目标", "Remove Target");
        OpenRootText = Pick("打开根目录", "Open Root");
        TestConnectionText = Pick("测试连接", "Test Connection");
        SaveChangesText = Pick("编辑目标", "Edit Target");
        EditCredentialsText = Pick("编辑凭据", "Edit Credentials");
        OpenLogsText = Pick("打开日志", "Open Logs");
        RetryConnectionText = Pick("重试连接", "Retry Connection");

        TroubleshootingSteps =
        [
            Pick("确认 WebDAV URL 正确，并且包含正确的协议前缀（https://）。", "Verify that the WebDAV URL is correct and includes the proper protocol (https://)."),
            Pick("检查本地网络连接，以及是否存在 VPN / 防火墙限制。", "Check your local network connection and any active VPN / Firewall rules."),
            Pick("重新输入凭据，确认不存在特殊字符编码问题。", "Re-enter your credentials to ensure no special character encoding issues."),
        ];

        Filters =
        [
            Pick("全部目标", "All targets"),
            Pick("已连接", "Connected"),
            Pick("失败", "Failed"),
            Pick("未测试", "Untested"),
        ];
        SelectedTargetFilter = Filters[0];
        SearchText = string.Empty;
        Targets = [];
        TargetsCountText = string.Empty;
        TargetsFooterLeftText = string.Empty;
        TargetsFooterRightText = string.Empty;
        PaneTitle = string.Empty;
        SelectedTargetName = string.Empty;
        SelectedTargetStatus = string.Empty;
        SelectedTargetStatusVariant = "ready";
        LastCheckedText = string.Empty;
        FailureTitle = string.Empty;
        FailureMessage = string.Empty;
        FailureCodeText = string.Empty;
        ConnectionFields = [];
        ActiveBindings = [];
        StorageFacts = [];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SearchPlaceholder { get; }
    public string FilterPlaceholder { get; }
    public string RefreshText { get; }
    public string TestSelectedText { get; }
    public string AddTargetText { get; }
    public string HelpText { get; }
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
    public IReadOnlyList<string> TroubleshootingSteps { get; }
    public IReadOnlyList<string> Filters { get; }

    public Guid? SelectedTargetId { get; private set; }

    public SyncTargetConfig? GetSelectedConfig()
    {
        return SelectedTargetId is { } id ? _syncTargetStore.Get(id) : null;
    }

    public async Task LoadAsync(bool testConnections = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var configs = _syncTargetStore.List();
            if (testConnections)
            {
                foreach (var config in configs)
                {
                    await TestTargetInternalAsync(config, cancellationToken);
                }
            }

            _allTargets = configs.Select(CreateRow).ToArray();
            ApplyFilters();

            TargetsCountText = Pick($"目标 ({configs.Count})", $"Targets ({configs.Count})");
            TargetsFooterLeftText = Pick($"已配置 {configs.Count} 个", $"{configs.Count} configured");
            var lastCheck = _testResults.Count > 0 ? _testResults.Values.Max(result => result.CheckedAt) : (DateTimeOffset?)null;
            TargetsFooterRightText = Pick(
                $"最近检查：{UiFormat.RelativeTime(lastCheck, true)}",
                $"Last check: {UiFormat.RelativeTime(lastCheck, false)}");

            var selected = _allTargets.FirstOrDefault(row => row.TargetId == SelectedTargetId) ?? _allTargets.FirstOrDefault();
            if (selected is not null)
            {
                await SelectTargetAsync(selected, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load sync targets page data.");
        }
    }

    public async Task SelectTargetAsync(SyncTargetRow row, CancellationToken cancellationToken = default)
    {
        var config = _syncTargetStore.Get(row.TargetId);
        if (config is null)
        {
            return;
        }

        SelectedTargetId = config.Id;
        SelectedTargetName = config.Name;
        PaneTitle = Pick($"目标属性：{config.Name}", $"Target Properties: {config.Name}");

        var (statusText, statusVariant) = DescribeTargetStatus(config.Id);
        SelectedTargetStatus = statusText;
        SelectedTargetStatusVariant = statusVariant;

        if (_testResults.TryGetValue(config.Id, out var result))
        {
            LastCheckedText = Pick(
                $"最近检查：{UiFormat.TimeOfRecord(result.CheckedAt, true)}",
                $"Last checked: {UiFormat.TimeOfRecord(result.CheckedAt, false)}");
            ShowFailureState = !result.Success;
            if (!result.Success)
            {
                FailureTitle = Pick("连接测试失败", "Connection test failed");
                FailureMessage = result.Error ?? Pick("未提供错误详情。", "No error details were provided.");
                FailureCodeText = config.ProviderId.ToUpperInvariant() + "_CONNECTION_FAILED";
            }
        }
        else
        {
            LastCheckedText = Pick("尚未测试连接", "Connection not tested yet");
            ShowFailureState = false;
        }

        ConnectionFields = BuildConnectionFields(config);
        StorageFacts = BuildStorageFacts(config);
        ActiveBindings = await BuildActiveBindingsAsync(config, cancellationToken);
    }

    public async Task<bool> TestSelectedAsync(CancellationToken cancellationToken = default)
    {
        var config = GetSelectedConfig();
        if (config is null)
        {
            return false;
        }

        var success = await TestTargetInternalAsync(config, cancellationToken);
        await LoadAsync(testConnections: false, cancellationToken);
        return success;
    }

    public async Task RemoveSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedTargetId is not { } id)
        {
            return;
        }

        _syncTargetStore.Remove(id);
        _testResults.Remove(id);
        SelectedTargetId = null;
        await LoadAsync(testConnections: false, cancellationToken);
    }

    public async Task SaveTargetAsync(SyncTargetConfig config, CancellationToken cancellationToken = default)
    {
        _syncTargetStore.Upsert(config);
        SelectedTargetId = config.Id;
        await LoadAsync(testConnections: false, cancellationToken);
    }

    /// <summary>
    /// Lists immediate child folders under <paramref name="relativePath"/> on the WebDAV server
    /// described by the ad-hoc endpoint/credentials so the target editor can browse remote paths.
    /// </summary>
    public async Task<IReadOnlyList<string>> BrowseWebDavFoldersAsync(
        string baseUrl,
        string username,
        string password,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _storageProviders.OfType<WebDavStorageProvider>().FirstOrDefault()
            ?? throw new InvalidOperationException(Pick("WebDAV 提供程序未注册。", "The WebDAV provider is not registered."));

        var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["baseUrl"] = baseUrl,
            ["username"] = username,
            ["password"] = password,
        };

        var entries = await provider.BrowseDirectoriesAsync(configuration, relativePath, cancellationToken);
        return entries.Select(entry => entry.Path).ToArray();
    }

    public string? GetSelectedRootPathOrUrl()
    {
        var config = GetSelectedConfig();
        if (config is null)
        {
            return null;
        }

        return config.ProviderId switch
        {
            "local-folder" => config.Settings.GetValueOrDefault("rootPath"),
            "webdav" => config.Settings.GetValueOrDefault("baseUrl"),
            _ => null,
        };
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedTargetFilterChanged(string value) => ApplyFilters();

    private async Task<bool> TestTargetInternalAsync(SyncTargetConfig config, CancellationToken cancellationToken)
    {
        var provider = _storageProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderId, config.ProviderId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            _testResults[config.Id] = (false, Pick($"存储提供程序 '{config.ProviderId}' 未注册。", $"Storage provider '{config.ProviderId}' is not registered."), DateTimeOffset.Now);
            return false;
        }

        try
        {
            var result = await provider.TestConnectionAsync(config.Settings, cancellationToken);
            _testResults[config.Id] = (result.IsSuccess, result.ErrorMessage, DateTimeOffset.Now);
            return result.IsSuccess;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Connection test threw for target {TargetName}.", config.Name);
            _testResults[config.Id] = (false, exception.Message, DateTimeOffset.Now);
            return false;
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<SyncTargetRow> filtered = _allTargets;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(row =>
                row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                row.Detail.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedTargetFilter) && SelectedTargetFilter != Filters[0])
        {
            filtered = filtered.Where(row => string.Equals(row.Status, SelectedTargetFilter, StringComparison.OrdinalIgnoreCase));
        }

        Targets = filtered.ToArray();
    }

    private SyncTargetRow CreateRow(SyncTargetConfig config)
    {
        var (statusText, statusVariant) = DescribeTargetStatus(config.Id);
        var detail = config.ProviderId switch
        {
            "webdav" => config.Settings.GetValueOrDefault("baseUrl", "--"),
            "local-folder" => config.Settings.GetValueOrDefault("rootPath", "--"),
            _ => config.ProviderId,
        };

        return new SyncTargetRow(
            config.ProviderId == "webdav" ? "W" : "L",
            config.Name,
            detail,
            statusText,
            statusVariant,
            config.Id == SelectedTargetId)
        {
            TargetId = config.Id,
            // Material Symbols glyph for the storage kind: cloud (0xF15C) for WebDAV,
            // folder (0xE2C7) for local. Built via ConvertFromUtf32 so no raw glyph
            // characters live in source.
            ProviderGlyph = config.ProviderId == "webdav"
                ? char.ConvertFromUtf32(0xF15C)
                : char.ConvertFromUtf32(0xE2C7),
        };
    }

    private (string Text, string Variant) DescribeTargetStatus(Guid targetId)
    {
        if (!_testResults.TryGetValue(targetId, out var result))
        {
            return (Pick("未测试", "Untested"), "pending");
        }

        return result.Success
            ? (Pick("已连接", "Connected"), "synced")
            : (Pick("失败", "Failed"), "conflict");
    }

    private IReadOnlyList<UiPropertyRow> BuildConnectionFields(SyncTargetConfig config)
    {
        var fields = new List<UiPropertyRow>
        {
            new(Pick("显示名称", "Display Name"), config.Name),
            new(Pick("提供程序类型", "Provider Type"), config.ProviderId == "webdav" ? "WebDAV" : Pick("本地文件夹", "Local Folder")),
        };

        if (config.ProviderId == "webdav")
        {
            fields.Add(new UiPropertyRow(Pick("端点 URL", "Endpoint URL"), config.Settings.GetValueOrDefault("baseUrl", "--")));
            fields.Add(new UiPropertyRow(Pick("用户名", "Username"), config.Settings.GetValueOrDefault("username", "--")));
            fields.Add(new UiPropertyRow(Pick("密码", "Password"), string.IsNullOrEmpty(config.Settings.GetValueOrDefault("password")) ? "--" : "••••••••"));
        }
        else
        {
            fields.Add(new UiPropertyRow(Pick("根目录", "Root Path"), config.Settings.GetValueOrDefault("rootPath", "--")));
        }

        var isDefault = _syncTargetStore.GetDefault()?.Id == config.Id;
        fields.Add(new UiPropertyRow(Pick("默认目标", "Default Target"), isDefault ? Pick("是", "Yes") : Pick("否", "No")));
        return fields;
    }

    private IReadOnlyList<UiPropertyRow> BuildStorageFacts(SyncTargetConfig config)
    {
        if (config.ProviderId != "local-folder")
        {
            return
            [
                new(Pick("提供程序", "Provider"), "WebDAV"),
                new(Pick("容量信息", "Capacity"), Pick("远端未提供", "Not reported by remote")),
            ];
        }

        try
        {
            var rootPath = config.Settings.GetValueOrDefault("rootPath");
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return [];
            }

            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(rootPath)) ?? rootPath);
            var usedBytes = Directory.Exists(rootPath)
                ? Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                : 0;
            return
            [
                new(Pick("目标占用", "Target Usage"), UiFormat.Bytes(usedBytes)),
                new(Pick("磁盘总容量", "Drive Capacity"), UiFormat.Bytes(drive.TotalSize)),
                new(Pick("磁盘可用", "Drive Free"), UiFormat.Bytes(drive.AvailableFreeSpace)),
                new(Pick("所在卷", "Volume"), drive.Name),
            ];
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to compute storage facts for target {TargetName}.", config.Name);
            return [];
        }
    }

    private async Task<IReadOnlyList<UiBindingUsage>> BuildActiveBindingsAsync(SyncTargetConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var usages = new List<UiBindingUsage>();
            var instances = await _gameInstanceRepository.ListAsync(cancellationToken);
            foreach (var instance in instances)
            {
                var bindings = await _storageBindingRepository.ListByGameInstanceAsync(instance.Id, cancellationToken);
                foreach (var binding in bindings)
                {
                    var matchesById = binding.Settings.TryGetValue("targetId", out var targetId) &&
                        Guid.TryParse(targetId, out var parsed) && parsed == config.Id;
                    var matchesByProvider = !binding.Settings.ContainsKey("targetId") &&
                        string.Equals(binding.StorageProviderId, config.ProviderId, StringComparison.OrdinalIgnoreCase);
                    if (matchesById || matchesByProvider)
                    {
                        usages.Add(new UiBindingUsage(instance.DisplayName, Pick($"命名空间：{binding.RemoteNamespace}", $"Namespace: {binding.RemoteNamespace}")));
                    }
                }
            }

            if (usages.Count == 0)
            {
                usages.Add(new UiBindingUsage(Pick("暂无绑定", "No bindings yet"), Pick("通过添加游戏向导把游戏绑定到此目标。", "Bind games to this target from the Add Game wizard.")));
            }

            return usages;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to list active bindings for target {TargetName}.", config.Name);
            return [];
        }
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record SyncTargetRow(
    string DefinitionLabel,
    string Name,
    string Detail,
    string Status,
    string StatusVariant,
    bool IsSelected)
{
    public Guid TargetId { get; init; }

    public string ProviderGlyph { get; init; } = string.Empty;
}
