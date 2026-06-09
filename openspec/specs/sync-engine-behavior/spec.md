## Purpose

定义 GSYNC 同步引擎的队列、取消、进度与快照保护行为。

## Requirements

### Requirement: 串行同步队列
SyncEngine SHALL 维护单一串行操作队列，任意时刻最多有一个 SyncJob 在执行。

#### Scenario: 队列串行执行
- **WHEN** 用户在前一个同步任务未完成时发起第二个同步操作
- **THEN** 第二个操作 SHALL 进入等待队列，第一个操作完成后才开始执行

#### Scenario: 队列状态可观测
- **WHEN** SyncEngine 中有活跃任务或等待任务
- **THEN** UI SHALL 能实时获取 `ActiveJob`（当前任务）和 `QueueDepth`（等待数量）

### Requirement: 取消支持
所有 SyncJob SHALL 支持通过 CancellationToken 取消操作。取消后 SHALL 不留下半完成的文件状态，已上传/下载的部分文件 SHALL 被回滚或清理。

#### Scenario: 用户取消进行中的同步
- **WHEN** 用户点击取消按钮，CancellationToken 被触发
- **THEN** SyncEngine SHALL 在下一个安全检查点停止操作，清理临时文件，将 SyncJob 状态标记为 Cancelled

### Requirement: 进度报告
SyncJob SHALL 通过 `IProgress<SyncProgress>` 向 UI 报告进度，SyncProgress SHALL 包含当前文件名、已处理文件数、总文件数。

#### Scenario: 进度更新频率
- **WHEN** 同步操作处理每个文件完成后
- **THEN** SHALL 调用一次 IProgress.Report，更新已处理文件数

### Requirement: 快照前置保护
执行 Download（覆盖本地文件）操作前，SyncEngine SHALL 自动对本地文件创建快照备份。

#### Scenario: 下载前自动快照
- **WHEN** SyncEngine 执行 Download 方向的 SyncJob
- **THEN** SHALL 在覆盖任何本地文件之前，先将受影响文件打包为 ZIP 快照并记录到 SyncRecord
