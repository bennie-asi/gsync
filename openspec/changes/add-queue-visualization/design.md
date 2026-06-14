## Context

同步任务经由 `ISyncEngine.QueueAsync` 写入 `SyncQueue`（一个 `Channel<SyncJob>` 无界通道），由 `App.xaml.cs` 启动的后台 `SyncEngine.ProcessQueuedJobsAsync` 串行消费。当前实现的局限：

- `SyncQueue` 仅暴露 `ActiveJob` 和 `int QueueDepth`（一个计数器）。等待中的任务被通道封装，无法枚举、无法移除。
- `SyncJob` 无 `Id`、无入队时间、无展示名称、无队列维护的状态；取消依赖调用方在构造时传入的 `CancellationToken`（多数调用点传的是默认值），任务交给引擎后无法再被外部取消。
- `ProcessQueuedJobsAsync` 用 `CreateLinkedTokenSource(cancellationToken, job.CancellationToken)` 派生 token，取消能力锁死在调用方一侧。
- UI 侧无队列屏幕；导航在 `MainWindowViewModel` 的 `tag → PageType` 映射中注册；`docs/ui/prototype-inventory.md` 没有 Queue 屏幕的 Stitch 设计稿。

本变更引入可观测/可操作队列（core）与 Queue 屏幕（app）。

## Goals / Non-Goals

**Goals:**
- 队列可枚举等待任务、可观测活跃任务与进度、可发出变更通知。
- 支持移除等待任务、取消活跃任务（取消令牌由队列拥有）。
- 提供 Queue 屏幕用于查看与操作任务，并在外壳显示队列活动指示。
- 保持现有串行执行语义与历史记录写入行为。

**Non-Goals:**
- 不引入并发/并行执行或优先级调度。
- 不引入任务持久化（应用重启后队列不恢复）。
- 不改变上传/下载/比较/冲突解决的具体 IO 逻辑。
- 不重排队列顺序（拖拽排序留待后续）。

## Decisions

### 决策 1：保留 `Channel` 作为执行管道，叠加可观测的等待集合
在 `SyncQueue` 内部除 `Channel<SyncJob>` 外，再维护一个有序结构（`List`/`Dictionary<Guid, SyncJob>` + 锁）记录等待任务，`QueueAsync` 同时写通道与登记表，`Start`/`Complete` 在登记表与活跃槽间迁移任务。移除等待任务时从登记表删除并打「已取消」标记，消费循环取出后若发现已标记则跳过（通道本身无法随机删除元素，故采用「逻辑删除 + 跳过」）。
- **替代方案**：完全替换为自管理的 `List` + `SemaphoreSlim` 信号。否决：改动面更大、易引入消费循环回归；逻辑删除方案对 `ProcessQueuedJobsAsync` 改动最小。

### 决策 2：取消令牌由队列拥有
队列为每个任务持有 `CancellationTokenSource`。`SyncJob` 暴露队列拥有的 token（替代调用方注入语义），`ProcessQueuedJobsAsync` 链接 app 生命周期 token 与队列 token。`CancelActive(Guid id)` 触发对应 CTS。
- **替代方案**：保留调用方 token。否决：调用点（VM）生命周期短，无法在任务执行期间持有并触发取消。
- **影响**：`SyncJob.CancellationToken` 语义变化属 BREAKING；五处 `new SyncJob { ... }` 调用点需更新（多数本就传默认值，改动轻微）。`ResolveConflictsAsync` 走的是非队列直接执行路径，单独保留其 token 处理。

### 决策 3：`SyncJob` 增加身份/元数据，但保持构造点轻量
新增 `Id`（默认 `Guid.NewGuid()`）、`EnqueuedAtUtc`、`DisplayName`、队列维护的 `Status`。`DisplayName` 由调用方传入已解析的游戏/实例标题（VM 已持有该数据），队列不反查仓储。`Status` 由队列写入，构造时为 `Queued`。

### 决策 4：变更通知用事件 + UI 线程编组
`ISyncQueue` 暴露 `event EventHandler QueueChanged`（或 `IObservable`）。`QueuePageViewModel` 订阅并在 WinUI `DispatcherQueue` 上刷新 `ObservableCollection`。进度通过现有 `IProgress<SyncProgress>` 汇入队列的活跃任务进度字段，一并触发通知（节流以避免每文件刷新 UI）。

### 决策 5：Queue 屏幕作为一等导航页
在 `MainWindowViewModel` 的 `tag → PageType` 映射新增 `"queue" => typeof(Pages.QueuePage)`，新增 `QueuePage` + `QueuePageViewModel`，在 `ServiceCollectionExtensions` 注册，在 nav rail 增加入口。布局复用既有表格基元（`ResizableTableView`）、`Badge`、`ActivityFeed` 样式，遵循 UI 表格规范（左对齐、省略号、悬浮提示）。外壳活动指示复用 `QueueDepth`/活跃状态绑定到 nav 项徽标。

## Risks / Trade-offs

- [无 Stitch Queue 设计稿] → 见 Open Questions；倾向复用 `Library - Sync In-Progress` 与 `ActivityFeed` 既有视觉语言，避免临时自创布局违反「遵循 Stitch」约束。
- [通道逻辑删除导致已移除任务仍被消费循环取出] → 取出后立即检查删除标记并跳过、不写历史；以单元测试覆盖「移除后不执行」。
- [并发访问登记表/CTS] → 用锁保护登记表与活跃槽迁移；CTS 在任务终止时统一 `Dispose`。
- [BREAKING 改 `SyncJob` token 语义] → 集中更新五处调用点 + `SyncEngineExecutionTests`/`SyncQueueTests`，构建即可暴露遗漏。
- [进度高频通知抖动 UI] → 对 `QueueChanged` 做节流（进度更新合并）。

## Migration Plan

1. Core：扩展 `SyncJob`、`ISyncQueue`、`SyncQueue`，调整 `ProcessQueuedJobsAsync`；更新核心测试。
2. 调用点：更新五处 `new SyncJob { ... }` 传入 `DisplayName`，移除对注入 token 的依赖。
3. App：新增 `QueuePage`/`QueuePageViewModel`、DI、导航与外壳指示。
4. 无数据迁移（队列不持久化）；回滚仅需撤销代码变更。

## Open Questions

- ~~Queue 屏幕是否需要先在 Stitch 生成正式设计稿~~ **已决议（2026-06-13）**：先在 Stitch（项目 `13407775155513183369`，设计系统 `GSYNC Desktop Dark`）生成正式 Queue 设计稿，再严格照稿实现。UI 实现（任务组 4）在拿到设计稿前暂缓。
- ~~已完成/失败任务是否在 Queue 屏幕保留近期完成区~~ **已决议（2026-06-13）**：Queue 仅展示等待 + 活跃任务；终态（完成/失败/取消）完全交由 History 屏幕。
- 活跃指示放在 nav rail 的 Queue 项徽标，还是同时在标题栏/状态栏展示？（实现时按 Stitch 设计稿确定）
