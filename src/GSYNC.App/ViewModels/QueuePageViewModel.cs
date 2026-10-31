using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly bool _isChinese;
    private volatile bool _isDisposed;
    private int _refreshScheduled;

    [ObservableProperty] private bool _isEmptyState = true;
    [ObservableProperty] private bool _hasActiveJobs;
    [ObservableProperty] private bool _hasPendingJobs;
    [ObservableProperty] private string _systemStatus = string.Empty;
    [ObservableProperty] private string _pendingTitle = string.Empty;

    public QueuePageViewModel(ILocalizationService localizationService, ISyncQueue syncQueue)
    {
        _syncQueue = syncQueue;
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _refreshTimer = _dispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(150);
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += RefreshTimer_Tick;
        _syncQueue.QueueChanged += SyncQueue_QueueChanged;
        Refresh();
    }

    public bool IsChinese => _isChinese;
    public ObservableCollection<QueueRow> ActiveJobs { get; } = [];
    public ObservableCollection<QueueRow> PendingJobs { get; } = [];
    public string PageTitle => Pick("同步队列", "Sync Queue");
    public string PageSubtitle => Pick("查看正在运行和等待中的同步任务", "View active and pending sync jobs");
    public string QueueNote => Pick("任务按顺序执行。可取消运行中的任务或移除等待中的任务；完成后可在历史中查看结果。", "Jobs run in order. Cancel an active job or remove a pending job; finished results are available in history.");
    public string ActiveTitle => Pick("正在运行的任务", "Running job");
    public string EmptyTitle => Pick("队列当前为空", "Queue is empty");
    public string EmptyMessage => Pick("从游戏库、详情页或比较页面发起同步后，任务会出现在这里。", "Jobs appear here when started from Library, Details, or Compare.");
    public string LibraryText => Pick("前往游戏库", "Go to Library");
    public string HistoryTitle => Pick("查看已完成的任务", "View completed jobs");
    public string HistoryNote => Pick("任务结束后会从队列移除，同步结果保留在历史中。", "Jobs leave the queue when finished; their results remain in history.");
    public string HistoryText => Pick("查看历史", "View history");

    public void Refresh()
    {
        if (_isDisposed) return;

        var active = _syncQueue.ActiveJob;
        Reconcile(ActiveJobs, active is null ? [] : [CreateRow(active, true, _syncQueue.ActiveProgress ?? active.LatestProgress)]);
        Reconcile(PendingJobs, _syncQueue.PendingJobs.Select(job => CreateRow(job, false, job.LatestProgress)).ToArray());
        HasActiveJobs = ActiveJobs.Count > 0;
        HasPendingJobs = PendingJobs.Count > 0;
        IsEmptyState = !HasActiveJobs && !HasPendingJobs;
        PendingTitle = Pick($"等待中的任务 ({PendingJobs.Count})", $"Pending jobs ({PendingJobs.Count})");
        SystemStatus = Pick($"{ActiveJobs.Count} 个进行中 · {PendingJobs.Count} 个等待中", $"{ActiveJobs.Count} running · {PendingJobs.Count} pending");
    }

    private static void Reconcile(ObservableCollection<QueueRow> current, IReadOnlyList<QueueRow> desired)
    {
        for (var i = current.Count - 1; i >= 0; i--)
            if (!desired.Any(row => row.Id == current[i].Id)) current.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            var index = current.ToList().FindIndex(row => row.Id == desired[i].Id);
            if (index < 0) current.Insert(i, desired[i]);
            else
            {
                current[index].UpdateProgress(desired[i]);
                if (index != i) current.Move(index, i);
            }
        }
    }

    public bool Cancel(QueueRow row) => row.IsActive && _syncQueue.CancelActive(row.Id);
    public bool Remove(QueueRow row) => !row.IsActive && _syncQueue.Remove(row.Id);

    private void SyncQueue_QueueChanged(object? sender, EventArgs e)
    {
        if (_isDisposed || Interlocked.Exchange(ref _refreshScheduled, 1) != 0) return;
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed) Interlocked.Exchange(ref _refreshScheduled, 0);
                else _refreshTimer.Start();
            })) Interlocked.Exchange(ref _refreshScheduled, 0);
    }

    private void RefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        Interlocked.Exchange(ref _refreshScheduled, 0);
        Refresh();
    }

    private QueueRow CreateRow(SyncJob job, bool isActive, SyncProgress? progress)
    {
        var hasTotal = progress is { TotalFiles: > 0 };
        var percentage = hasTotal ? Math.Clamp(100d * progress!.ProcessedFiles / progress.TotalFiles, 0, 100) : 0;
        var progressText = hasTotal
            ? Pick($"已处理 {progress!.ProcessedFiles} / {progress.TotalFiles} 个文件", $"{progress!.ProcessedFiles} / {progress.TotalFiles} files processed")
            : Pick("正在准备文件", "Preparing files");
        var currentFile = string.IsNullOrWhiteSpace(progress?.CurrentFileName)
            ? Pick("等待进度更新", "Waiting for progress")
            : progress.CurrentFileName!;
        return new QueueRow(
            job.Id,
            string.IsNullOrWhiteSpace(job.DisplayName) ? Pick("未命名任务", "Unnamed job") : job.DisplayName!,
            UiFormat.DirectionText(job.Direction, _isChinese),
            isActive ? Pick("进行中", "Running") : Pick("等待中", "Pending"),
            isActive ? "ready" : "pending",
            Pick($"入队：{UiFormat.TimeOfRecord(job.EnqueuedAtUtc, true)}", $"Queued: {UiFormat.TimeOfRecord(job.EnqueuedAtUtc, false)}"),
            progressText,
            Pick($"当前文件：{currentFile}", $"Current file: {currentFile}"),
            isActive,
            percentage,
            isActive && !hasTotal,
            isActive ? Pick("取消任务", "Cancel job") : Pick("从队列移除", "Remove from queue"));
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _syncQueue.QueueChanged -= SyncQueue_QueueChanged;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
    }
}

public partial class QueueRow(
    Guid id, string displayName, string direction, string status, string statusVariant, string enqueuedText,
    string progressText, string currentFileText, bool isActive, double progressPercentage,
    bool isProgressIndeterminate, string actionText) : ObservableObject
{
    public Guid Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public string Direction { get; } = direction;
    public string Status { get; } = status;
    public string StatusVariant { get; } = statusVariant;
    public string EnqueuedText { get; } = enqueuedText;
    public bool IsActive { get; } = isActive;
    public string ActionText { get; } = actionText;
    [ObservableProperty] private string _progressText = progressText;
    [ObservableProperty] private string _currentFileText = currentFileText;
    [ObservableProperty] private double _progressPercentage = progressPercentage;
    [ObservableProperty] private bool _isProgressIndeterminate = isProgressIndeterminate;

    public void UpdateProgress(QueueRow next)
    {
        ProgressText = next.ProgressText;
        CurrentFileText = next.CurrentFileText;
        ProgressPercentage = next.ProgressPercentage;
        IsProgressIndeterminate = next.IsProgressIndeterminate;
    }
}
