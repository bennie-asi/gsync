using CommunityToolkit.Mvvm.ComponentModel;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Serilog;

namespace GSYNC.App.ViewModels;

public partial class ConflictResolutionViewModel : ObservableObject
{
    private readonly bool _isChinese;
    private readonly ISyncEngine _syncEngine;

    private ConflictNavigationContext? _context;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _comparisonContext = string.Empty;

    [ObservableProperty]
    private string _warningTitle = string.Empty;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private string _tableFooterText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _localVersion = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _remoteVersion = [];

    [ObservableProperty]
    private IReadOnlyList<ConflictFileRow> _conflictFiles = [];

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _syncBaseline = [];

    public ConflictResolutionViewModel(ILocalizationService localizationService, ISyncEngine syncEngine)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _syncEngine = syncEngine;

        _title = Pick("解决冲突", "Resolve Conflict");
        PageSubtitle = Pick("安全优先的双栏对比界面，明确保留动作、基线摘要与风险提示。", "Safety-first dual comparison workspace with explicit resolution actions, baseline context, and visible risk guidance.");
        _warningTitle = Pick("等待比较结果", "Waiting for comparison");
        _warningMessage = Pick("从游戏详情页执行“比较”后，这里会展示双端差异。", "Run Compare from a game's detail page to populate this view.");
        RiskHintTitle = Pick("操作前提示", "Before you resolve");
        RiskHintMessage = Pick("先检查基线与受影响文件。下载任何远端版本前都会先创建本地快照。", "Review the baseline and affected files first. A local snapshot is created before any remote overwrite.");
        _comparisonContext = Pick("对比本地与远端版本，确认基线后再执行保守或覆盖动作。", "Compare local and remote versions, confirm the baseline, then choose a conservative or overwrite action.");
        LocalTitle = Pick("本地版本", "Local Version");
        RemoteTitle = Pick("远端版本", "Remote Version");
        BaselineTitle = Pick("比较摘要", "Comparison summary");
        TableTitle = Pick("差异文件", "Differing files");
        TableSubtitle = Pick("列表中可按文件设置保留本地、保留远端或暂不处理。", "Use the list to choose keep-local, keep-remote, or leave undecided per file.");
        UseLocalText = Pick("全部保留本地", "Keep Local for All");
        UseRemoteText = Pick("全部保留远端", "Keep Remote for All");
        KeepBothText = Pick("仅处理已决文件", "Apply Decided Files Only");
        BackupRestoreText = Pick("批量设为远端", "Bulk Remote");
        CancelText = Pick("返回", "Back");
    }

    public string PageSubtitle { get; }
    public string RiskHintTitle { get; }
    public string RiskHintMessage { get; }
    public string LocalTitle { get; }
    public string RemoteTitle { get; }
    public string BaselineTitle { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string UseLocalText { get; }
    public string UseRemoteText { get; }
    public string KeepBothText { get; }
    public string BackupRestoreText { get; }
    public string CancelText { get; }
    public bool IsChinese => _isChinese;

    public bool HasContext => _context is not null;

    public void LoadFrom(ConflictNavigationContext context)
    {
        _context = context;
        Title = Pick($"解决冲突 · {context.GameName}", $"Resolve Conflict · {context.GameName}");

        var entries = context.Comparison.Entries;
        var conflicts = entries.Where(entry => entry.ChangeKind == SyncChangeKind.Conflict).ToArray();
        var localOnly = entries.Where(entry => entry.ChangeKind == SyncChangeKind.AddedLocally).ToArray();
        var remoteOnly = entries.Where(entry => entry.ChangeKind == SyncChangeKind.AddedRemotely).ToArray();
        var unchanged = entries.Count(entry => entry.ChangeKind == SyncChangeKind.Unchanged);

        WarningTitle = conflicts.Length == 0 && localOnly.Length == 0 && remoteOnly.Length == 0
            ? Pick("双端一致", "Both sides match")
            : Pick("检测到双端差异", "Divergent versions detected");
        WarningMessage = conflicts.Length == 0 && localOnly.Length == 0 && remoteOnly.Length == 0
            ? Pick("本地与远端没有差异，无需进一步处理。", "Local and remote files match. No action is required.")
            : Pick(
                $"共 {entries.Count} 个文件：{conflicts.Length} 个冲突、{localOnly.Length} 个仅本地、{remoteOnly.Length} 个仅远端。",
                $"{entries.Count} files compared: {conflicts.Length} conflicts, {localOnly.Length} local-only, {remoteOnly.Length} remote-only.");

        ComparisonContext = Pick(
            $"游戏：{context.GameName} · 比较时间：{UiFormat.TimeOfRecord(DateTimeOffset.Now, true)}",
            $"Game: {context.GameName} · Compared: {UiFormat.TimeOfRecord(DateTimeOffset.Now, false)}");

        LocalVersion =
        [
            new(Pick("设备", "Device"), Pick($"{Environment.MachineName} · 当前设备", $"{Environment.MachineName} · current device")),
            new(Pick("差异文件", "Differing files"), Pick($"{conflicts.Length + localOnly.Length} 个", $"{conflicts.Length + localOnly.Length}")),
        ];
        RemoteVersion =
        [
            new(Pick("来源", "Source"), Pick("同步目标 · 远端", "Sync target · remote")),
            new(Pick("差异文件", "Differing files"), Pick($"{conflicts.Length + remoteOnly.Length} 个", $"{conflicts.Length + remoteOnly.Length}")),
        ];
        SyncBaseline =
        [
            new(Pick("比较文件总数", "Total files compared"), entries.Count.ToString()),
            new(Pick("一致文件", "Unchanged files"), unchanged.ToString()),
            new(Pick("冲突 / 仅本地 / 仅远端", "Conflicts / local-only / remote-only"), $"{conflicts.Length} / {localOnly.Length} / {remoteOnly.Length}"),
        ];

        ConflictFiles = entries
            .Where(entry => entry.ChangeKind != SyncChangeKind.Unchanged)
            .Select(entry => new ConflictFileRow(
                entry.RelativePath,
                DescribeLocalState(entry),
                DescribeRemoteState(entry),
                ChooseInitialAction(entry),
                false))
            .ToArray();

        RefreshFooter(unchanged);
    }

    public void SetAllActions(ConflictResolutionAction action)
    {
        ConflictFiles = ConflictFiles.Select(row => row with { Action = action }).ToArray();
        RefreshFooter();
    }

    public void SetAction(string filename, ConflictResolutionAction action)
    {
        ConflictFiles = ConflictFiles.Select(row =>
            string.Equals(row.Filename, filename, StringComparison.OrdinalIgnoreCase)
                ? row with { Action = action }
                : row).ToArray();
        RefreshFooter();
    }

    public async Task<string?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_context is null)
        {
            return Pick("没有可处理的比较结果。", "No comparison context to act on.");
        }

        try
        {
            var plan = new ConflictResolutionPlan
            {
                GameInstanceId = _context.InstanceId,
                Decisions = ConflictFiles.Select(row => new ConflictResolutionDecision
                {
                    RelativePath = row.Filename,
                    Action = row.Action,
                }).ToArray(),
            };
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = _context.InstanceId,
                Direction = SyncDirection.ResolveConflict,
                ConflictResolutionPlan = plan,
            }, cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to queue conflict resolution plan.");
            return exception.Message;
        }
    }

    private void RefreshFooter(int unchanged = -1)
    {
        var decided = ConflictFiles.Count(row => row.Action is ConflictResolutionAction.KeepLocal or ConflictResolutionAction.KeepRemote);
        var undecided = ConflictFiles.Count(row => row.Action == ConflictResolutionAction.Undecided);
        if (unchanged >= 0)
        {
            TableFooterText = Pick(
                $"{ConflictFiles.Count} 个差异文件 · {decided} 个已决 · {undecided} 个未决 · {unchanged} 个一致文件未列出",
                $"{ConflictFiles.Count} differing files · {decided} decided · {undecided} undecided · {unchanged} unchanged hidden");
        }
        else
        {
            TableFooterText = Pick(
                $"{ConflictFiles.Count} 个差异文件 · {decided} 个已决 · {undecided} 个未决",
                $"{ConflictFiles.Count} differing files · {decided} decided · {undecided} undecided");
        }
    }

    private ConflictResolutionAction ChooseInitialAction(SyncDiffEntry entry)
    {
        return entry.ChangeKind switch
        {
            SyncChangeKind.AddedLocally => ConflictResolutionAction.KeepLocal,
            SyncChangeKind.AddedRemotely => ConflictResolutionAction.KeepRemote,
            _ => ConflictResolutionAction.Undecided,
        };
    }

    private string DescribeLocalState(SyncDiffEntry entry)
    {
        return entry.ChangeKind switch
        {
            SyncChangeKind.AddedLocally => Pick("仅本地存在", "Local only"),
            SyncChangeKind.AddedRemotely => Pick("本地缺失", "Missing locally"),
            SyncChangeKind.Conflict => entry.LocalModifiedAtUtc is { } modified
                ? Pick($"本地修改于 {UiFormat.TimeOfRecord(modified, true)}", $"Modified {UiFormat.TimeOfRecord(modified, false)}")
                : Pick("内容不同", "Different content"),
            _ => Pick("一致", "Unchanged"),
        };
    }

    private string DescribeRemoteState(SyncDiffEntry entry)
    {
        return entry.ChangeKind switch
        {
            SyncChangeKind.AddedLocally => Pick("远端缺失", "Missing remotely"),
            SyncChangeKind.AddedRemotely => Pick("仅远端存在", "Remote only"),
            SyncChangeKind.Conflict => entry.RemoteModifiedAtUtc is { } modified
                ? Pick($"远端修改于 {UiFormat.TimeOfRecord(modified, true)}", $"Modified {UiFormat.TimeOfRecord(modified, false)}")
                : Pick("内容不同", "Different content"),
            _ => Pick("一致", "Unchanged"),
        };
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record ConflictFileRow(
    string Filename,
    string LocalStatus,
    string RemoteStatus,
    ConflictResolutionAction Action,
    bool IsSelected)
{
    public string ActionText => Action switch
    {
        ConflictResolutionAction.KeepLocal => "Keep Local",
        ConflictResolutionAction.KeepRemote => "Keep Remote",
        ConflictResolutionAction.Skip => "Skip",
        _ => "Undecided",
    };
}
