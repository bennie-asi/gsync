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

        Title = Pick("比较并处理存档", "Compare and resolve saves");
        PageSubtitle = Pick("安全优先的双栏对比界面，明确保留动作、基线摘要与风险提示。", "Safety-first dual comparison workspace with explicit resolution actions, baseline context, and visible risk guidance.");
        WarningTitle = Pick("等待比较结果", "Waiting for comparison");
        WarningMessage = Pick("从游戏详情页执行”比较”后，这里会展示双端差异。", "Run Compare from a game's detail page to populate this view.");
        RiskHintTitle = Pick("操作前提示", "Before you resolve");
        RiskHintMessage = Pick("先检查基线与受影响文件。下载任何远端版本前都会先创建本地快照。", "Review the baseline and affected files first. A local snapshot is created before any remote overwrite.");
        ComparisonContext = Pick("对比本地与远端版本，确认基线后再执行保守或覆盖动作。", "Compare local and remote versions, confirm the baseline, then choose a conservative or overwrite action.");
        LocalTitle = Pick("本地版本", "Local Version");
        RemoteTitle = Pick("远端版本", "Remote Version");
        BaselineTitle = Pick("比较摘要", "Comparison summary");
        TableTitle = Pick("差异文件", "Differing files");
        TableSubtitle = Pick("为每个文件选择保留本地、保留远端或暂不处理。", "Choose whether to keep the local or remote version of each file, or leave it unchanged.");
        UseLocalText = Pick("全部保留本地", "Keep Local for All");
        UseRemoteText = Pick("全部保留远端", "Keep Remote for All");
        KeepBothText = Pick("仅处理已决文件", "Apply Decided Files Only");
        BackupRestoreText = Pick("应用所选决策", "Apply decisions");
        CancelText = Pick("返回", "Back");
        TableFooterText = string.Empty;
        LocalVersion = [];
        RemoteVersion = [];
        ConflictFiles = [];
        SyncBaseline = [];
    }

    public string PageSubtitle { get; }
    public string RiskHintTitle { get; }
    public string RiskHintMessage { get; }
    public string VersionDetailsTitle => Pick("查看版本详情", "View version details");
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
    public Guid? InstanceId => _context?.InstanceId;

    public void LoadFrom(ConflictNavigationContext context)
    {
        _context = context;
        Title = Pick($"比较并处理存档 · {context.GameName}", $"Compare and resolve saves · {context.GameName}");

        var entries = context.Comparison.Entries;
        var conflicts = entries.Where(entry => entry.ChangeKind == SyncChangeKind.Conflict).ToArray();
        var localOnly = entries.Where(entry => entry.ChangeKind is SyncChangeKind.AddedLocally or SyncChangeKind.UpdatedLocally).ToArray();
        var remoteOnly = entries.Where(entry => entry.ChangeKind is SyncChangeKind.AddedRemotely or SyncChangeKind.UpdatedRemotely).ToArray();
        var unchanged = entries.Count(entry => entry.ChangeKind == SyncChangeKind.Unchanged);

        WarningTitle = conflicts.Length == 0 && localOnly.Length == 0 && remoteOnly.Length == 0
            ? Pick("双端一致", "Both sides match")
            : conflicts.Length > 0 ? Pick("发现存档冲突", "Save conflicts found")
            : Pick("发现存档差异", "Save differences found");
        WarningMessage = conflicts.Length == 0 && localOnly.Length == 0 && remoteOnly.Length == 0
            ? Pick("本地与远端没有差异，无需进一步处理。", "Local and remote files match. No action is required.")
            : Pick(
                $"共 {entries.Count} 个文件：{conflicts.Length} 个冲突、{localOnly.Length} 个本地变化、{remoteOnly.Length} 个远端变化。",
                $"{entries.Count} files compared: {conflicts.Length} conflicts, {localOnly.Length} local changes, {remoteOnly.Length} remote changes.");

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
            new(Pick("冲突 / 本地变化 / 远端变化", "Conflicts / local changes / remote changes"), $"{conflicts.Length} / {localOnly.Length} / {remoteOnly.Length}"),
        ];

        ConflictFiles = entries
            .Where(entry => entry.ChangeKind != SyncChangeKind.Unchanged)
            .Select(entry => new ConflictFileRow(
                entry.RelativePath,
                DescribeLocalState(entry),
                DescribeRemoteState(entry),
                ConflictResolutionAction.Undecided,
                false) { IsChinese = _isChinese, CanKeepLocal = entry.LocalHash is not null, CanKeepRemote = entry.RemoteHash is not null })
            .ToArray();

        RefreshFooter(unchanged);
    }

    public void SetAllActions(ConflictResolutionAction action)
    {
        ConflictFiles = ConflictFiles.Select(row => row with { Action = IsActionAvailable(row.Filename, action) ? action : ConflictResolutionAction.Skip }).ToArray();
        RefreshFooter();
    }

    public void SetAction(string filename, ConflictResolutionAction action)
    {
        ConflictFiles = ConflictFiles.Select(row =>
            string.Equals(row.Filename, filename, StringComparison.OrdinalIgnoreCase)
                ? row with { Action = IsActionAvailable(row.Filename, action) ? action : ConflictResolutionAction.Skip }
                : row).ToArray();
        RefreshFooter();
    }

    private bool IsActionAvailable(string filename, ConflictResolutionAction action)
    {
        var entry = _context?.Comparison.Entries.FirstOrDefault(item => string.Equals(item.RelativePath, filename, StringComparison.OrdinalIgnoreCase));
        return action switch
        {
            ConflictResolutionAction.KeepLocal => entry?.LocalHash is not null,
            ConflictResolutionAction.KeepRemote => entry?.RemoteHash is not null,
            _ => true,
        };
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
                ExpectedConfigurationFingerprint = _context.Comparison.ConfigurationFingerprint,
                Decisions = ConflictFiles.Select(row => new ConflictResolutionDecision
                {
                    RelativePath = row.Filename,
                    Action = row.Action,
                    VerifyComparedVersions = true,
                    ExpectedLocalHash = _context.Comparison.Entries.Single(entry => entry.RelativePath == row.Filename).LocalHash,
                    ExpectedRemoteHash = _context.Comparison.Entries.Single(entry => entry.RelativePath == row.Filename).RemoteHash,
                }).ToArray(),
            };
            await _syncEngine.QueueAsync(new SyncJob
            {
                GameInstanceId = _context.InstanceId,
                Direction = SyncDirection.ResolveConflict,
                ConflictResolutionPlan = plan,
                DisplayName = _context.GameName,
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

    private string DescribeLocalState(SyncDiffEntry entry)
    {
        return entry.ChangeKind switch
        {
            SyncChangeKind.AddedLocally => Pick("仅本地存在", "Local only"),
            SyncChangeKind.AddedRemotely => Pick("本地缺失", "Missing locally"),
            SyncChangeKind.UpdatedLocally => Pick("本地已更新", "Updated locally"),
            SyncChangeKind.UpdatedRemotely => Pick("本地未更新", "Unchanged locally"),
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
            SyncChangeKind.UpdatedLocally => Pick("远端未更新", "Unchanged remotely"),
            SyncChangeKind.UpdatedRemotely => Pick("远端已更新", "Updated remotely"),
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
    public bool IsChinese { get; init; }
    public bool CanKeepLocal { get; init; }
    public bool CanKeepRemote { get; init; }
    public string KeepLocalText => IsChinese ? "保留本地" : "Keep local";
    public string KeepRemoteText => IsChinese ? "保留远端" : "Keep remote";
    public string SkipText => IsChinese ? "暂不处理" : "Skip";
    public string DecisionLabel => IsChinese ? $"{Filename}：{ActionText}" : $"{Filename}: {ActionText}";
    public string DisplayFilename
    {
        get
        {
            var normalized = Filename.Replace('\\', '/');
            var lastSeparator = normalized.LastIndexOf('/');
            return lastSeparator < 0 ? normalized : $"{normalized[(lastSeparator + 1)..]} — {normalized[..lastSeparator]}";
        }
    }
    public string ActionText => Action switch
    {
        ConflictResolutionAction.KeepLocal => IsChinese ? "保留本地" : "Keep local",
        ConflictResolutionAction.KeepRemote => IsChinese ? "保留远端" : "Keep remote",
        ConflictResolutionAction.Skip => IsChinese ? "暂不处理" : "Skip",
        _ => IsChinese ? "待选择" : "Undecided",
    };
}
