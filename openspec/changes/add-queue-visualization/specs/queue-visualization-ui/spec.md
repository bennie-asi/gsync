## ADDED Requirements

### Requirement: 队列任务列表视图
应用 SHALL 提供一个 Queue（队列）屏幕，列出当前活跃任务与所有等待任务。每一行 SHALL 展示任务名称、同步方向、状态徽标、以及活跃任务的进度（已处理/总数）。列表 SHALL 在队列发生变更时实时刷新。

#### Scenario: 展示活跃与等待任务
- **WHEN** 用户打开 Queue 屏幕且队列中存在活跃任务与等待任务
- **THEN** 屏幕 SHALL 将活跃任务置于显著位置并展示其进度，并按入队顺序列出等待任务

#### Scenario: 空队列状态
- **WHEN** 用户打开 Queue 屏幕且没有任何活跃或等待任务
- **THEN** 屏幕 SHALL 展示空状态提示，而非空白列表

#### Scenario: 实时刷新
- **WHEN** 队列中任务入队、开始、完成或被取消
- **THEN** Queue 屏幕 SHALL 在无需用户手动刷新的情况下更新列表

### Requirement: 查看任务详情
用户 SHALL 能选择某个队列任务以查看其详情,包括展示名称、方向、状态、入队时间,以及活跃任务的当前文件与进度。

#### Scenario: 选中任务查看详情
- **WHEN** 用户在 Queue 列表中选择一个任务
- **THEN** 屏幕 SHALL 展示该任务的详细信息

### Requirement: 队列任务操作
Queue 屏幕 SHALL 为活跃任务提供「取消」操作,为等待任务提供「移除」操作。取消活跃任务 SHALL 先进行二次确认。操作完成后列表 SHALL 反映新状态。

#### Scenario: 取消活跃任务
- **WHEN** 用户对活跃任务点击取消并确认
- **THEN** 应用 SHALL 通过队列触发该任务取消,且 UI SHALL 随后将其反映为已取消并从活跃位置移除

#### Scenario: 移除等待任务
- **WHEN** 用户对一个等待任务点击移除
- **THEN** 应用 SHALL 通过队列移除该任务,且该任务 SHALL 从列表中消失且永不执行

### Requirement: 外壳级队列活动指示
应用外壳 SHALL 在有同步任务进行或等待时展示队列活动指示（如导航项徽标或活动数量），使 Queue 屏幕在同步进行时可被发现。指示 SHALL 提供进入 Queue 屏幕的入口。

#### Scenario: 同步进行时显示指示
- **WHEN** 队列中存在活跃任务或等待任务
- **THEN** 外壳 SHALL 展示队列活动指示,点击后导航至 Queue 屏幕

#### Scenario: 队列清空后隐藏指示
- **WHEN** 队列中不再有活跃或等待任务
- **THEN** 外壳 SHALL 隐藏或清零活动指示
