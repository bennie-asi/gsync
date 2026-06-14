## MODIFIED Requirements

### Requirement: 串行同步队列
SyncEngine SHALL 维护单一串行操作队列，任意时刻最多有一个 SyncJob 在执行。队列 SHALL 为每个任务维护稳定身份与状态（`Queued` → `Running` → 终止态），并使等待任务集合与活跃任务对外可枚举、可观测。

#### Scenario: 队列串行执行
- **WHEN** 用户在前一个同步任务未完成时发起第二个同步操作
- **THEN** 第二个操作 SHALL 进入等待队列，第一个操作完成后才开始执行

#### Scenario: 队列状态可观测
- **WHEN** SyncEngine 中有活跃任务或等待任务
- **THEN** UI SHALL 能实时获取活跃任务、按顺序排列的等待任务集合，以及队列变更通知

#### Scenario: 处理时维护任务状态
- **WHEN** SyncEngine 从队列取出一个任务开始执行
- **THEN** 该任务状态 SHALL 由 `Queued` 转为 `Running`，并在完成、失败或取消时转为对应终止态

### Requirement: 取消支持
所有 SyncJob SHALL 支持取消操作。取消令牌 SHALL 由队列为每个任务持有，使任务在交由引擎执行后仍可被取消。取消后 SHALL 不留下半完成的文件状态，已上传/下载的部分文件 SHALL 被回滚或清理。

#### Scenario: 用户取消进行中的同步
- **WHEN** 用户从队列 UI 取消当前活跃任务
- **THEN** 队列 SHALL 触发该任务队列拥有的取消令牌，SyncEngine SHALL 在下一个安全检查点停止操作，清理临时文件，将任务状态标记为 Cancelled 并写入终止历史记录

#### Scenario: 取消尚未执行的等待任务
- **WHEN** 用户从队列 UI 移除一个尚未开始的等待任务
- **THEN** 该任务 SHALL 从队列移除且永不执行，SHALL 不写入终止历史记录
