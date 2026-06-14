## Why

The sync queue (`SyncQueue` / `SyncEngine.ProcessQueuedJobsAsync`) runs entirely in the background with no visibility: users can enqueue uploads, downloads, and compares but cannot see what is waiting, what is running, or its progress, and there is no way to cancel a job once queued. The queue today exposes only `ActiveJob` and an `int QueueDepth` — queued jobs are anonymous, carry no identity or metadata, and cannot be removed. We need a queue visualization module so users can view, inspect, and operate on (notably cancel) sync tasks.

## What Changes

- **BREAKING** Enrich `SyncJob` with stable identity and display metadata: a `Guid Id`, an enqueue timestamp, a resolved display name (game/instance), and a queue-managed status. Existing call sites that construct `SyncJob` (in `GameDetailsViewModel`, `AddGameWizardViewModel`, `ConflictResolutionViewModel`, `LibraryPageViewModel`, `SyncEngine.ResolveConflictsAsync`) are updated.
- Extend `ISyncQueue` so the queue is observable and operable: enumerate pending jobs in order, expose the active job plus latest progress, raise change notifications, and support removing a pending job and requesting cancellation of the active job (queue owns a per-job `CancellationTokenSource`).
- Move per-job cancellation ownership into the queue/engine instead of relying on the caller-supplied `SyncJob.CancellationToken`, so a job can be cancelled from the queue UI after it has been handed off.
- Add a **Queue** screen + view model (page, nav entry, DI registration) that lists pending and active tasks with direction, status, progress, and timing; supports selecting a task to view details; and exposes cancel (active) and remove (pending) actions with appropriate confirmation.
- Surface live queue depth / activity in the app shell so the Queue screen is discoverable while syncs run.

## Capabilities

### New Capabilities
- `sync-queue-management`: Observable, operable sync queue — job identity/metadata, ordered enumeration of pending jobs, active-job tracking with progress, change notifications, removal of pending jobs, and cancellation of the active job.
- `queue-visualization-ui`: Queue screen showing pending/active sync tasks with per-task view + operate (cancel/remove) interactions and a shell-level activity indicator.

### Modified Capabilities
- `sync-engine-behavior`: Queue processing now records queue-owned job status transitions and honors queue-driven cancellation of pending and active jobs (cancelled jobs still write terminal `Cancelled` history records).

## Impact

- Core: `SyncJob` (Models), `ISyncQueue` (Abstractions), `SyncQueue` and `SyncEngine.ProcessQueuedJobsAsync` (Services/Sync); `SyncQueueTests`, `SyncEngineExecutionTests`.
- App: new `QueuePage` + `QueuePageViewModel`, navigation registration in `MainWindowViewModel` and the nav rail, DI in `ServiceCollectionExtensions`, queue worker wiring in `App.xaml.cs`; updates to the five `new SyncJob { ... }` call sites.
- UI: no existing Stitch mockup for a Queue screen — see design.md for the reference-screen decision (reuse activity/in-progress patterns vs. generate a new Stitch screen).
