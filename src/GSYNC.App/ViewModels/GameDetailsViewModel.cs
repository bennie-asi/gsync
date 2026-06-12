using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;
using Serilog;

namespace GSYNC.App.ViewModels;

public sealed record ConflictNavigationContext(Guid InstanceId, string GameName, SyncComparison Comparison);

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly IGameInstanceRepository _gameInstanceRepository;
    private readonly IStorageBindingRepository _storageBindingRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IManifestService _manifestService;
    private readonly ISyncEngine _syncEngine;
    private readonly IEnumerable<ISourceProvider> _sourceProviders;
    private readonly IDefinitionOverrideStore _definitionOverrideStore;
    private readonly PathResolver _pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider;

    private GameInstance? _instance;
    private StorageBinding? _binding;
    private GameContentDefinition? _definition;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _activeTarget = string.Empty;

    [ObservableProperty]
    private string _appIdText = string.Empty;

    [ObservableProperty]
    private string _studioText = string.Empty;

    [ObservableProperty]
    private string _lastPlayedText = string.Empty;

    [ObservableProperty]
    private string _overallStatusText = string.Empty;

    [ObservableProperty]
    private string _overallStatusVariant = "pending";

    [ObservableProperty]
    private string _totalSizeText = "--";

    [ObservableProperty]
    private string _tableFooterText = string.Empty;

    [ObservableProperty]
    private string _propertiesTitle = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _overviewMetrics = [];

    [ObservableProperty]
    private IReadOnlyList<ContentItemRow> _contentItems = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _selectedItemProperties = [];

    [ObservableProperty]
    private IReadOnlyList<UiActivityItem> _recentActivityItems = [];

    [ObservableProperty]
    private IReadOnlyList<SnapshotRow> _snapshots = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInspectorVisible = true;

    public GameDetailsViewModel(
        ILocalizationService localizationService,
        IGameInstanceRepository gameInstanceRepository,
        IStorageBindingRepository storageBindingRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IManifestService manifestService,
        ISyncEngine syncEngine,
        IEnumerable<ISourceProvider> sourceProviders,
        IDefinitionOverrideStore definitionOverrideStore,
        PathResolver pathResolver,
        SystemVariableProvider systemVariableProvider)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _gameInstanceRepository = gameInstanceRepository;
        _storageBindingRepository = storageBindingRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _manifestService = manifestService;
        _syncEngine = syncEngine;
        _sourceProviders = sourceProviders;
        _definitionOverrideStore = definitionOverrideStore;
        _pathResolver = pathResolver;
        _systemVariableProvider = systemVariableProvider;

        _title = Pick("游戏详情", "Game Details");
        PageSubtitle = Pick("精炼后的详情工作区，固定展示游戏上下文、内容项、历史与右侧检查面板。", "Refined detail workspace with persistent game context, content items, history, and a right-hand inspection pane.");
        PrimaryActionLabel = Pick("快速操作", "Quick Actions");
        UploadText = Pick("上传", "Upload");
        DownloadText = Pick("下载", "Download");
        CompareText = Pick("比较", "Compare");
        RestoreText = Pick("恢复备份", "Restore Backup");
        TableSectionTitle = Pick("内容项", "Content Items");
        TableSectionSubtitle = Pick("存档、配置与附加内容保持在高密度表格中，右侧检查区负责属性与路径验证。", "Saves, config, and auxiliary content stay in a dense table while the right pane handles inspection and path validation.");
        AddItemText = Pick("添加条目", "Add Item");
        _propertiesTitle = Pick("检查器", "Inspector");
        PropertiesSubtitle = Pick("所选内容项的路径模板、策略与可移植性检查结果。", "Path template, policy, and portability checks for the selected content item.");
        OpenFolderText = Pick("打开目录", "Open Folder");
        TestPathText = Pick("测试路径", "Test Path");
        HistorySectionTitle = Pick("历史与快照", "History & Snapshots");
        HistorySectionSubtitle = Pick("底部区域保留最近同步记录与可恢复快照。", "The bottom section keeps recent sync activity and recoverable snapshots visible.");
        HistoryTabText = Pick("最近同步历史", "Recent Sync History");
        SnapshotsTabText = Pick("快照", "Snapshots");
        HistoryFeedTitle = Pick("最近活动", "Recent Activity");
        SnapshotFeedTitle = Pick("可恢复快照", "Recoverable Snapshots");
        OverallStatusLabel = Pick("整体状态", "Overall Status");
        TotalSizeLabel = Pick("本地大小", "Local Size");
        ActiveTargetLabel = Pick("当前目标", "Active Target");
        OverviewMetricsTitle = Pick("上下文摘要", "Context Summary");
        InspectorStatusTitle = Pick("检查状态", "Inspection Status");
        InspectorStatusMessage = Pick("在同步前先核对所选内容项的解析路径与启用状态。", "Validate the resolved path and enabled state of the selected content item before syncing.");

        TableTitle = string.Empty;
        TableSubtitle = string.Empty;
        _overallStatusText = Pick("未同步", "Not synced");
        _activeTarget = Pick("未绑定", "Unbound");
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
    public string AddItemText { get; }
    public bool IsChinese => _isChinese;
    public string PropertiesSubtitle { get; }
    public string OpenFolderText { get; }
    public string TestPathText { get; }
    public string HistorySectionTitle { get; }
    public string HistorySectionSubtitle { get; }
    public string HistoryTabText { get; }
    public string SnapshotsTabText { get; }
    public string HistoryFeedTitle { get; }
    public string SnapshotFeedTitle { get; }
    public string OverallStatusLabel { get; }
    public string TotalSizeLabel { get; }
    public string ActiveTargetLabel { get; }
    public string OverviewMetricsTitle { get; }
    public string InspectorStatusTitle { get; }
    public string InspectorStatusMessage { get; }

    public Guid? InstanceId => _instance?.Id;

    public ContentItemRow? SelectedContentItem { get; private set; }

    public async Task LoadAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            _instance = await _gameInstanceRepository.GetAsync(instanceId, cancellationToken);
            if (_instance is null)
            {
                Title = Pick("未找到游戏实例", "Game instance not found");
                return;
            }

            Title = _instance.DisplayName;
            AppIdText = _instance.PlatformInstanceId ?? _instance.GameId;
            StudioText = DescribeSource(_instance.SourceProviderId);
            LastPlayedText = Pick(
                $"添加于 {UiFormat.RelativeTime(_instance.CreatedAtUtc, true)}",
                $"Added {UiFormat.RelativeTime(_instance.CreatedAtUtc, false)}");

            var bindings = await _storageBindingRepository.ListByGameInstanceAsync(instanceId, cancellationToken);
            _binding = bindings.FirstOrDefault();
            ActiveTarget = _binding is null
                ? Pick("未绑定", "Unbound")
                : _binding.Settings.GetValueOrDefault("targetName", _binding.StorageProviderId);

            _definition = await _manifestService.GetDefinitionAsync(_instance.GameId, cancellationToken);

            var records = await _syncHistoryRepository.ListRecordsAsync(instanceId, cancellationToken);
            var lastRecord = records.FirstOrDefault();
            (OverallStatusText, OverallStatusVariant) = lastRecord is null
                ? (Pick("未同步", "Not synced"), "pending")
                : lastRecord.Status switch
                {
                    SyncJobStatus.Completed => (Pick("已同步", "Synced"), "synced"),
                    SyncJobStatus.Failed => (Pick("失败", "Failed"), "conflict"),
                    SyncJobStatus.Cancelled => (Pick("已取消", "Cancelled"), "disabled"),
                    _ => (Pick("进行中", "In progress"), "pending"),
                };

            OverviewMetrics =
            [
                new(Pick("平台", "Platform"), DescribeSource(_instance.SourceProviderId)),
                new(Pick("安装目录", "Install Directory"), _instance.InstallDirectory ?? "--"),
                new(Pick("上次同步", "Last Sync"), UiFormat.RelativeTime(lastRecord?.CompletedAtUtc ?? lastRecord?.StartedAtUtc, _isChinese)),
                new(Pick("同步记录数", "Sync Records"), records.Count.ToString()),
            ];

            BuildContentItems();

            RecentActivityItems = records
                .Take(8)
                .Select(record => new UiActivityItem(
                    $"{UiFormat.DirectionText(record.Direction, _isChinese)} · {UiFormat.StatusText(record.Status, _isChinese)}",
                    record.ErrorMessage ?? record.Summary ?? "--",
                    UiFormat.RelativeTime(record.CompletedAtUtc ?? record.StartedAtUtc, _isChinese),
                    record.Status == SyncJobStatus.Failed))
                .ToArray();

            var snapshots = await _syncHistoryRepository.ListSnapshotsAsync(instanceId, cancellationToken);
            Snapshots = snapshots
                .Select((snapshot, index) => new SnapshotRow(
                    $"SN-{snapshot.Id.ToString("N")[..8]}",
                    index == 0 ? Pick("最新", "Latest") : string.Empty,
                    UiFormat.TimeOfRecord(snapshot.CreatedAtUtc, _isChinese),
                    Environment.MachineName,
                    UiFormat.Bytes(snapshot.TotalBytes))
                {
                    SnapshotId = snapshot.Id,
                })
                .ToArray();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load game details for instance {InstanceId}.", instanceId);
        }
    }

    public void SelectContentItem(ContentItemRow row)
    {
        SelectedContentItem = row;
        IsInspectorVisible = true;
        PropertiesTitle = Pick($"检查器：{row.Name}", $"Inspector: {row.Name}");
        var properties = new List<UiPropertyRow>
        {
            new(Pick("条目名称", "Item Name"), row.Name),
            new(Pick("分类", "Category"), row.Category),
            new(Pick("路径模板", "Path Template"), row.PathTemplate),
            new(Pick("解析路径", "Resolved Path"), row.ResolvedPath),
            new(Pick("启用状态", "Enabled"), row.Enabled ? Pick("已启用", "Enabled") : Pick("已禁用", "Disabled")),
        };
        if (!string.IsNullOrWhiteSpace(row.Patterns))
        {
            properties.Add(new UiPropertyRow(Pick("包含/排除模式", "Include/Exclude Patterns"), row.Patterns));
        }

        SelectedItemProperties = properties;
    }

    public void HideInspector()
    {
        IsInspectorVisible = false;
    }

    public async Task<string?> AddCustomContentItemAsync(string name, string pathTemplate, CancellationToken cancellationToken = default)
    {
        if (_definition is null)
        {
            return Pick("当前游戏没有可编辑的内容定义。", "No editable content definition is available for this game.");
        }

        try
        {
            var definitions = (await _definitionOverrideStore.GetDefinitionsAsync(cancellationToken)).ToList();
            var targetDefinition = definitions.FirstOrDefault(definition => string.Equals(definition.GameId, _definition.GameId, StringComparison.OrdinalIgnoreCase));
            if (targetDefinition is null)
            {
                targetDefinition = new GameContentDefinition
                {
                    GameId = _definition.GameId,
                    DisplayName = _definition.DisplayName,
                    Description = _definition.Description,
                    SourceHints = _definition.SourceHints,
                    ContentItems = _definition.ContentItems,
                };
                definitions.Add(targetDefinition);
            }

            var updatedItems = targetDefinition.ContentItems.ToList();
            updatedItems.Add(new ContentItem
            {
                ContentId = SlugifyContentId(name),
                Category = ContentCategory.Extra,
                PathTemplates = [pathTemplate.Trim().Replace('\\', '/')],
                DefaultEnabled = true,
                Description = name.Trim(),
            });

            targetDefinition = new GameContentDefinition
            {
                GameId = targetDefinition.GameId,
                DisplayName = targetDefinition.DisplayName,
                Description = targetDefinition.Description,
                SourceHints = targetDefinition.SourceHints,
                ContentItems = updatedItems,
            };

            definitions = definitions
                .Select(definition => string.Equals(definition.GameId, targetDefinition.GameId, StringComparison.OrdinalIgnoreCase) ? targetDefinition : definition)
                .ToList();
            await _definitionOverrideStore.SaveDefinitionsAsync(definitions, cancellationToken);
            _definition = targetDefinition;
            BuildContentItems();
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to add custom content item.");
            return exception.Message;
        }
    }

    public (bool Exists, string Message) TestSelectedPath()
    {
        if (SelectedContentItem is null)
        {
            return (false, Pick("尚未选择内容项。", "No content item selected."));
        }

        var path = SelectedContentItem.ResolvedPath;
        if (File.Exists(path) || Directory.Exists(path))
        {
            return (true, Pick($"路径存在：{path}", $"Path exists: {path}"));
        }

        return (false, Pick($"路径不存在：{path}", $"Path does not exist: {path}"));
    }

    public async Task<string?> QueueSyncAsync(SyncDirection direction, CancellationToken cancellationToken = default)
    {
        if (_instance is null)
        {
            return Pick("游戏实例尚未加载。", "Game instance is not loaded.");
        }

        if (_binding is null)
        {
            return Pick("该游戏尚未绑定同步目标。", "This game has no storage binding yet.");
        }

        try
        {
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = _instance.Id,
                Direction = direction,
            }, cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue {Direction} job.", direction);
            return exception.Message;
        }
    }

    public async Task<(ConflictNavigationContext? Context, string? Error)> CompareAsync(CancellationToken cancellationToken = default)
    {
        if (_instance is null || _binding is null)
        {
            return (null, Pick("该游戏尚未绑定同步目标。", "This game has no storage binding yet."));
        }

        IsBusy = true;
        try
        {
            var comparison = await _syncEngine.CompareAsync(new SyncJob
            {
                GameInstanceId = _instance.Id,
                Direction = SyncDirection.Compare,
            }, cancellationToken);

            return (new ConflictNavigationContext(_instance.Id, _instance.DisplayName, comparison), null);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Compare failed for instance {InstanceId}.", _instance.Id);
            return (null, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<string?> RestoreLatestSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var latest = Snapshots.FirstOrDefault();
        if (latest is null)
        {
            return Pick("还没有可恢复的快照。下载远端数据时会自动创建快照。", "No snapshots to restore yet. Snapshots are created automatically before downloads.");
        }

        try
        {
            await _syncEngine.RestoreSnapshotAsync(latest.SnapshotId, cancellationToken);
            if (_instance is not null)
            {
                await LoadAsync(_instance.Id, cancellationToken);
            }

            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Restore failed for instance {InstanceId}.", _instance?.Id);
            return exception.Message;
        }
    }

    private void BuildContentItems()
    {
        if (_instance is null || _definition is null)
        {
            ContentItems = [];
            TableFooterText = Pick("没有可用的内容定义。", "No content definition available.");
            return;
        }

        var systemVariables = _systemVariableProvider.GetVariables();
        var sourceProvider = _sourceProviders.FirstOrDefault(provider =>
            string.Equals(provider.ProviderId, _instance.SourceProviderId, StringComparison.OrdinalIgnoreCase));
        var sourceVariables = sourceProvider?.ResolveVariables(_instance)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bindingSettings = _binding?.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var rows = new List<ContentItemRow>();
        long totalBytes = 0;
        foreach (var item in _definition.ContentItems)
        {
            var enabled = IsContentEnabled(item);
            foreach (var template in item.PathTemplates)
            {
                var resolved = _pathResolver.Resolve(template, systemVariables, sourceVariables, _instance.Variables, bindingSettings);
                var (statusText, statusVariant, size) = DescribeLocalPath(resolved, enabled);
                totalBytes += size;
                rows.Add(new ContentItemRow(
                    item.ContentId,
                    DescribeCategory(item.Category),
                    resolved,
                    Pick("双向", "Bidirectional"),
                    statusText,
                    statusVariant,
                    enabled)
                {
                    PathTemplate = template,
                    Patterns = BuildPatternText(item),
                });
            }
        }

        ContentItems = rows;
        TotalSizeText = UiFormat.Bytes(totalBytes);
        var enabledCount = rows.Count(row => row.Enabled);
        TableFooterText = Pick(
            $"{rows.Count} 个内容项 · {enabledCount} 个已启用",
            $"{rows.Count} items · {enabledCount} enabled");

        if (rows.Count > 0)
        {
            SelectContentItem(rows[0]);
        }
    }

    private bool IsContentEnabled(ContentItem item)
    {
        if (_binding is null || _binding.ContentSelections.Count == 0)
        {
            return item.DefaultEnabled;
        }

        return _binding.ContentSelections.Any(selection =>
            string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && selection.IsEnabled);
    }

    private (string Text, string Variant, long SizeBytes) DescribeLocalPath(string resolvedPath, bool enabled)
    {
        if (!enabled)
        {
            return (Pick("已禁用", "Disabled"), "disabled", 0);
        }

        try
        {
            if (File.Exists(resolvedPath))
            {
                return (Pick("就绪", "Ready"), "ready", new FileInfo(resolvedPath).Length);
            }

            if (Directory.Exists(resolvedPath))
            {
                var size = Directory.EnumerateFiles(resolvedPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
                return (Pick("就绪", "Ready"), "ready", size);
            }
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to inspect path {Path}.", resolvedPath);
        }

        return (Pick("本地缺失", "Missing locally"), "pending", 0);
    }

    private string DescribeCategory(ContentCategory category)
    {
        return category switch
        {
            ContentCategory.Save => Pick("存档", "Saves"),
            ContentCategory.Config => Pick("配置", "Config"),
            ContentCategory.Extra => Pick("附加", "Extra"),
            _ => category.ToString(),
        };
    }

    private static string BuildPatternText(ContentItem item)
    {
        var parts = new List<string>();
        parts.AddRange(item.IncludePatterns.Select(pattern => $"+ {pattern}"));
        parts.AddRange(item.ExcludePatterns.Select(pattern => $"- {pattern}"));
        return string.Join('\n', parts);
    }

    private static string SlugifyContentId(string name)
    {
        var buffer = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(character);
            }
            else if ((char.IsWhiteSpace(character) || character is '-' or '_') && (buffer.Length == 0 || buffer[^1] != '-'))
            {
                buffer.Append('-');
            }
        }

        return buffer.Length == 0 ? "custom-item" : buffer.ToString().Trim('-');
    }

    private string DescribeSource(string sourceProviderId)
    {
        return sourceProviderId.ToLowerInvariant() switch
        {
            "steam" => "Steam",
            "epic" => "Epic",
            "custom" => Pick("自定义", "Custom"),
            _ => sourceProviderId,
        };
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record ContentItemRow(
    string Name,
    string Category,
    string ResolvedPath,
    string Policy,
    string Status,
    string StatusVariant,
    bool Enabled)
{
    public string PathTemplate { get; init; } = string.Empty;

    public string Patterns { get; init; } = string.Empty;

    public string EnabledText => Enabled ? "On" : "Off";

    public string EnabledVariant => Enabled ? "synced" : "disabled";
}
