## ADDED Requirements

### Requirement: 同步任务身份与元数据
每个 `SyncJob` SHALL 携带稳定的唯一标识与展示元数据，使其在队列中可被识别、排序与展示。`SyncJob` SHALL 包含 `Id`（Guid）、入队时间戳（UTC）、用于展示的名称（游戏/实例标题）、同步方向，以及由队列维护的状态。

#### Scenario: 入队时分配身份
- **WHEN** 一个 `SyncJob` 被加入队列
- **THEN** 队列 SHALL 确保该任务具有唯一的 `Id` 与入队时间戳，并将其初始状态标记为 `Queued`

#### Scenario: 元数据可供展示
- **WHEN** UI 请求队列中的某个任务
- **THEN** 该任务 SHALL 提供展示名称、方向、状态与入队时间，无需 UI 另行查询游戏实例

### Requirement: 可观测的队列
`ISyncQueue` SHALL 暴露当前活跃任务、按入队顺序排列的等待任务集合，以及活跃任务的最新进度，并在队列内容或活跃任务发生变化时发出变更通知。

#### Scenario: 枚举等待任务
- **WHEN** 队列中存在一个或多个等待任务
- **THEN** `ISyncQueue` SHALL 按入队先后顺序返回这些等待任务的快照

#### Scenario: 暴露活跃任务与进度
- **WHEN** 一个任务正在执行并通过 `IProgress<SyncProgress>` 报告进度
- **THEN** `ISyncQueue` SHALL 将其作为活跃任务暴露，并提供其最近一次报告的进度

#### Scenario: 变更通知
- **WHEN** 任务入队、开始执行、完成、被移除或被取消
- **THEN** `ISyncQueue` SHALL 发出一次队列变更通知，使订阅的 UI 能刷新展示

### Requirement: 移除等待中的任务
`ISyncQueue` SHALL 支持按 `Id` 移除尚未开始执行的等待任务；该任务 SHALL 不会被执行，且不写入终止历史记录。

#### Scenario: 移除等待任务
- **WHEN** 用户对一个状态为 `Queued` 的任务请求移除
- **THEN** 队列 SHALL 将其从等待集合中移除、发出变更通知，且该任务永不进入执行

#### Scenario: 拒绝移除活跃任务
- **WHEN** 用户对当前正在执行的活跃任务请求移除
- **THEN** 队列 SHALL 拒绝移除操作，活跃任务应改用取消流程处理

### Requirement: 取消活跃任务
队列 SHALL 为每个任务持有一个由队列拥有的 `CancellationTokenSource`，使活跃任务在交由引擎执行后仍可被取消，而不依赖调用方提供的 `CancellationToken`。

#### Scenario: 取消活跃任务
- **WHEN** 用户对当前活跃任务请求取消
- **THEN** 队列 SHALL 触发该任务队列拥有的取消令牌，引擎 SHALL 在下一个安全检查点停止该任务

#### Scenario: 取消等待中的任务
- **WHEN** 用户对一个等待任务请求取消
- **THEN** 队列 SHALL 将其作为移除处理（永不执行），且 SHALL 不写入额外的失败记录
