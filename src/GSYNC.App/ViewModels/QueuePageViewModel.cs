using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace GSYNC.App.ViewModels;

public partial class QueuePageViewModel : ObservableObject, IDisposable
{
    private readonly ISyncQueue _syncQueue;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly bool _isChinese;
    private bool _isDisposed;

    [ObservableProperty]
    private ObservableCollection<QueueRow> _jobs = new();

    [ObservableProperty]
    private QueueRow? _selectedJob;

    [ObservableProperty]
    private IReadOnlyList<UiPropertyRow> _jobDetails = Array.Empty<UiPropertyRow>();

    [ObservableProperty]
    private IReadOnlyList<UiActivityItem> _jobActivity = Array.Empty<UiActivityItem>();

    [ObservableProperty]
    private bool _isEmptyState = true;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _selectedJobIsActive;

    [ObservableProperty]
    private bool _selectedJobIsPending;

    [ObservableProperty]
    private string _selectedJobIdText = string.Empty;

    [ObservableProperty]
    private string _systemStatus = string.Empty;

    public QueuePageViewModel(ILocalizationService localizationService, ISyncQueue syncQueue)
    {
        _syncQueue = syncQueue;
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        PageTitle = Pick("同步队列", "Sync Queue");
        PageSubtitle = Pick("查看正在运行与等待中的同步任务", "View active and pending sync jobs");
        RefreshButtonText = Pick("刷新", "Refresh");
        TableTitle = Pick("队列任务", "Queue Jobs");
        TableSubtitle = Pick("实时显示当前活跃任务与等待中的同步操作", "Live view of the active job and pending sync operations");
        DetailTitle = Pick("任务详情", "Job Details");
        DetailSubtitle = Pick("选中任务的状态、进度与当前文件", "Status, progress, and current file for the selected job");
        DetailSummaryTitle = Pick("选中任务", "Selected Job");
        ActivityTitle = Pick("任务活动", "Task Activity");
        ActivitySubtitle = Pick("队列状态与最新进度会在这里同步更新", "Queue state and latest progress update here in real time");
        CancelButtonText = Pick("取消活跃任务", "Cancel Active Job");
        RemoveButtonText = Pick("移除等待任务", "Remove Pending Job");
        EmptyStateTitle = Pick("队列当前为空", "Queue is Empty");
        EmptyStateMessage = Pick("当你从 Library、详情页或冲突页发起同步后，任务会立即出现在这里。", "Jobs will appear here as soon as you queue syncs from Library, Details, or Conflict Resolution.");
        EmptyStateHint = Pick("你可以先返回 Library 发起同步，随后这里会自动刷新。", "Return to Library to queue a sync, then come back here to watch it update automatically.");
        EmptyStateActionText = Pick("前往 Library", "Go to Library");
        EmptyStateSecondaryActionText = Pick("立即刷新", "Refresh Now");
        EmptyInspectorTitle = Pick("尚未选择任务", "No Job Selected");
        EmptyInspectorMessage = Pick("从左侧列表选择一个任务，即可查看其当前状态、进度与处理路径。", "Select a job from the table to inspect its current state, progress, and processed path.");
        WaitingCurrentFileText = Pick("等待执行", "Waiting to start");
        RunningWithoutFileText = Pick("同步进行中，等待下一次进度回报", "Sync in progress, waiting for the next progress update");
        PendingProgressText = Pick("等待中", "Pending");
        RunningProgressText = Pick("进行中", "Running");

        _syncQueue.QueueChanged += SyncQueue_QueueChanged;
        Refresh();
    }

    public bool IsChinese => _isChinese;
    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string RefreshButtonText { get; }
    public string TableTitle { get; }
    public string TableSubtitle { get; }
    public string DetailTitle { get; }
    public string DetailSubtitle { get; }
    public string DetailSummaryTitle { get; }
    public string ActivityTitle { get; }
    public string ActivitySubtitle { get; }
    public string CancelButtonText { get; }
    public string RemoveButtonText { get; }
    public string EmptyStateTitle { get; }
    public string EmptyStateMessage { get; }
    public string EmptyStateHint { get; }
    public string EmptyStateActionText { get; }
    public string EmptyStateSecondaryActionText { get; }
    public string EmptyInspectorTitle { get; }
    public string EmptyInspectorMessage { get; }
    public string WaitingCurrentFileText { get; }
    public string RunningWithoutFileText { get; }
    public string PendingProgressText { get; }
    public string RunningProgressText { get; }

    public void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var selectedId = SelectedJob?.Id;
        var rows = BuildRows();
        Jobs = new ObservableCollection<QueueRow>(rows);
        IsEmptyState = rows.Count == 0;
        SystemStatus = BuildSystemStatus(rows);

        var nextSelection = selectedId is Guid id
            ? rows.FirstOrDefault(row => row.Id == id)
            : rows.FirstOrDefault();

        SelectedJob = nextSelection;
    }

    public void SelectRow(QueueRow? row)
    {
        SelectedJob = row;
    }

    [RelayCommand(CanExecute = nameof(CanCancelSelected))]
    private void CancelSelected(QueueRow? row)
    {
        if (row is not { IsActive: true })
        {
            return;
        }

        _syncQueue.CancelActive(row.Id);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected(QueueRow? row)
    {
        if (row is not { IsActive: false })
        {
            return;
        }

        _syncQueue.Remove(row.Id);
    }

    private bool CanCancelSelected(QueueRow? row) => row is { IsActive: true };

    private bool CanRemoveSelected(QueueRow? row) => row is { IsActive: false };

    partial void OnSelectedJobChanged(QueueRow? value)
    {
        HasSelection = value is not null;
        SelectedJobIsActive = value?.IsActive == true;
        SelectedJobIsPending = value is { IsActive: false };
        SelectedJobIdText = value is null ? string.Empty : $"JOB-{value.Id.ToString("N")[..8]}";
        JobDetails = value is null ? Array.Empty<UiPropertyRow>() : BuildJobDetails(value);
        JobActivity = value is null ? Array.Empty<UiActivityItem>() : BuildJobActivity(value);
        CancelSelectedCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private void SyncQueue_QueueChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(Refresh);
    }

    private List<QueueRow> BuildRows()
    {
        var rows = new List<QueueRow>();

        if (_syncQueue.ActiveJob is { } activeJob)
        {
            rows.Add(CreateRow(activeJob, true, _syncQueue.ActiveProgress ?? activeJob.LatestProgress));
        }

        foreach (var job in _syncQueue.PendingJobs)
        {
            rows.Add(CreateRow(job, false, job.LatestProgress));
        }

        return rows;
    }

    private QueueRow CreateRow(SyncJob job, bool isActive, SyncProgress? progress)
    {
        var progressText = BuildProgressText(isActive, progress);
        var currentFile = string.IsNullOrWhiteSpace(progress?.CurrentFileName)
            ? (isActive ? RunningWithoutFileText : WaitingCurrentFileText)
            : progress.CurrentFileName!;

        return new QueueRow(
            job.Id,
            string.IsNullOrWhiteSpace(job.DisplayName) ? Pick("未命名任务", "Unnamed Job") : job.DisplayName!,
            UiFormat.DirectionText(job.Direction, _isChinese),
            UiFormat.StatusText(job.Status, _isChinese),
            ResolveStatusVariant(job.Status),
            UiFormat.TimeOfRecord(job.EnqueuedAtUtc, _isChinese),
            progressText,
            currentFile,
            isActive);
    }

    private IReadOnlyList<UiPropertyRow> BuildJobDetails(QueueRow row)
    {
        return
        [
            new(Pick("任务 ID", "Job ID"), SelectedJobIdText),
            new(Pick("名称", "Name"), row.DisplayName),
            new(Pick("方向", "Direction"), row.Direction),
            new(Pick("状态", "Status"), row.Status),
            new(Pick("入队时间", "Enqueued"), row.EnqueuedAt),
            new(Pick("进度", "Progress"), row.Progress),
            new(Pick("当前文件", "Current File"), row.CurrentFile),
        ];
    }

    private IReadOnlyList<UiActivityItem> BuildJobActivity(QueueRow row)
    {
        var items = new List<UiActivityItem>
        {
            new(
                Pick("已加入队列", "Queued"),
                Pick($"{row.DisplayName} 已进入同步队列。", $"{row.DisplayName} has entered the sync queue."),
                row.EnqueuedAt,
                "pending")
        };

        if (row.IsActive)
        {
            items.Add(new UiActivityItem(
                Pick("正在执行", "Running"),
                Pick($"当前文件：{row.CurrentFile}", $"Current file: {row.CurrentFile}"),
                row.Progress,
                "ready",
                true));
        }
        else
        {
            items.Add(new UiActivityItem(
                Pick("等待开始", "Waiting"),
                Pick("该任务会在前序任务完成后自动执行。", "This job will start automatically after earlier jobs complete."),
                row.Progress,
                "pending",
                true));
        }

        return items;
    }

    private string BuildProgressText(bool isActive, SyncProgress? progress)
    {
        if (progress is { TotalFiles: > 0 })
        {
            return $"{progress.ProcessedFiles}/{progress.TotalFiles}";
        }

        return isActive ? RunningProgressText : PendingProgressText;
    }

    private string ResolveStatusVariant(SyncJobStatus status)
    {
        return status switch
        {
            SyncJobStatus.Running => "ready",
            SyncJobStatus.Queued => "pending",
            _ => UiFormat.StatusVariant(status),
        };
    }

    private string BuildSystemStatus(IReadOnlyCollection<QueueRow> rows)
    {
        var activeCount = rows.Count(row => row.IsActive);
        var pendingCount = rows.Count - activeCount;
        if (rows.Count == 0)
        {
            return Pick("队列为空 · 当前没有待处理的同步任务", "Queue empty · no sync jobs are waiting");
        }

        return Pick(
            $"活跃 {activeCount} · 等待 {pendingCount}",
            $"Active {activeCount} · Pending {pendingCount}");
    }

    private string Pick(string zhCn, string enUs) => _isChinese ? zhCn : enUs;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _syncQueue.QueueChanged -= SyncQueue_QueueChanged;
    }
}

public sealed record QueueRow(
    Guid Id,
    string DisplayName,
    string Direction,
    string Status,
    string StatusVariant,
    string EnqueuedAt,
    string Progress,
    string CurrentFile,
    bool IsActive);