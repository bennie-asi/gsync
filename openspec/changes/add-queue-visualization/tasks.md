## 1. Core 模型与队列契约

- [x] 1.1 扩展 `SyncJob`（Models）：新增 `Id`（默认 `Guid.NewGuid()`）、`EnqueuedAtUtc`、`DisplayName`、队列维护的 `Status`（复用/扩展 `SyncJobStatus`，含 `Queued`/`Running`）
- [x] 1.2 扩展 `ISyncQueue`（Abstractions）：新增按顺序枚举等待任务的成员、活跃任务最新进度、`QueueChanged` 变更通知事件、`Remove(Guid id)`、`CancelActive(Guid id)`
- [x] 1.3 在 `SyncQueue` 中实现等待任务有序登记表 + 锁，`QueueAsync` 同时写通道与登记表并置状态 `Queued`，`TryBeginJob`/`Complete` 迁移活跃槽并更新状态
- [x] 1.4 在 `SyncQueue` 中为每个任务持有队列拥有的 `CancellationTokenSource`，实现 `CancelActive` 触发取消、任务终止时 `Dispose`
- [x] 1.5 实现 `Remove`：逻辑删除等待任务（拒绝移除活跃任务），并发出 `QueueChanged`
- [x] 1.6 接入活跃任务进度：将 `IProgress<SyncProgress>` 汇入队列进度字段，节流后触发 `QueueChanged`

## 2. SyncEngine 处理循环

- [x] 2.1 调整 `ProcessQueuedJobsAsync`：从通道取出后检查逻辑删除标记并跳过（不写历史），用队列拥有的 token 替代 `job.CancellationToken` 链接 app 生命周期 token
- [x] 2.2 处理任务状态迁移：取出置 `Running`，完成/失败/取消置对应终止态；保持取消时写入 `Cancelled` 终止历史记录（单任务取消/失败不退出处理循环，仅应用关闭时退出）
- [x] 2.3 确认 `ResolveConflictsAsync` 非队列直接执行路径不受 token 语义变更影响

## 3. 调用点更新

- [x] 3.1 更新 `GameDetailsViewModel`、`AddGameWizardViewModel`、`ConflictResolutionViewModel`、`LibraryPageViewModel` 中的 `new SyncJob { ... }`：传入已解析的 `DisplayName`（`QueueSyncForGameAsync` 新增可选 `displayName` 参数，`HomePage` 传入 `row.Name`）
- [x] 3.2 `dotnet build src/GSYNC.App`（x64）确认无遗漏调用点：0 错误

## 4. Queue 屏幕（App UI）

- [x] 4.1 新增 `QueuePageViewModel`：订阅 `QueueChanged`，在 `DispatcherQueue` 上维护活跃 + 等待任务的 `ObservableCollection`，暴露 Cancel/Remove 命令
- [x] 4.2 新增 `QueuePage.xaml`/`.cs`：复用 `ResizableTableView`、`Badge`、`ActivityFeed` 样式，遵循 UI 表格规范（左对齐、省略号、悬浮完整提示）；含空状态
- [x] 4.3 任务详情区：选中任务展示名称/方向/状态/入队时间/当前文件与进度
- [x] 4.4 操作交互：活跃任务「取消」走二次确认弹框（主题一致样式），等待任务「移除」直接生效
- [x] 4.5 在 `MainWindowViewModel` tag→PageType 映射注册 `"queue" => typeof(Pages.QueuePage)`，在 nav rail 增加入口，`ServiceCollectionExtensions` 注册 VM/Page
- [x] 4.6 外壳活动指示：将队列活跃/等待状态绑定到 nav Queue 项徽标，空队列时清零，点击进入 Queue 屏幕

## 5. 测试与验证

- [x] 5.1 更新/新增 `SyncQueueTests`：入队赋予身份、枚举顺序、移除等待任务后不执行、`CancelActive` 取消活跃任务、`QueueChanged` 触发（8 个用例）
- [x] 5.2 更新 `SyncEngineExecutionTests`：取消活跃任务写入 `Cancelled` 历史记录、被移除任务跳过且不写历史
- [x] 5.3 `dotnet build GSYNC.sln` + `dotnet test GSYNC.sln --no-build` 全绿（核心已绿 13/13、App 已编译；待 UI 完成后跑全量）
- [ ] 5.4 运行应用手动验证：入队多任务可见、活跃任务进度刷新、取消/移除生效、外壳指示随队列增减
