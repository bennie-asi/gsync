## MODIFIED Requirements

### Requirement: Library 界面落地
Library SHALL show searchable game rows, actionable synchronization states, clear primary actions, and an empty first-run guide. Essential controls and row actions SHALL remain readable at supported window widths.

#### Scenario: Library 主界面显示
- **WHEN** 用户打开 Library 页面
- **THEN** 页面 SHALL 显示游戏、状态、目标、最近同步和可执行的操作，并允许搜索和筛选。

#### Scenario: Library 顶部工具区对齐设计稿
- **WHEN** 用户查看 Library 顶部工具区
- **THEN** 搜索、添加游戏和同步操作 SHALL 有清楚的层级和可辨认的名称。

#### Scenario: Library 右侧操作与总览区对齐设计稿
- **WHEN** 用户查看 Library 总览
- **THEN** 页面 SHALL 保留紧凑的摘要和活动入口，并在空间不足时优先显示主列表和操作。

#### Scenario: Library 操作列按钮文本可辨认
- **WHEN** 用户查看主表格中的操作列
- **THEN** 操作 SHALL 可见、可读且可点击，不得被裁切。

### Requirement: Game Details 界面落地
Game Details SHALL emphasize current status and safe synchronization, provide content inspection and history, and adapt its inspector to available width while reusing shared controls.

#### Scenario: Game Details 页面显示
- **WHEN** 用户进入游戏详情
- **THEN** 页面 SHALL 显示游戏状态、目标、内容项和同步操作。

#### Scenario: Game Details 分区符合 refined 稿
- **WHEN** 窗口宽度不足以容纳多个面板
- **THEN** 页面 SHALL 调整分区，使主要操作和内容保持可用。

#### Scenario: Game Details 优先复用共享原语
- **WHEN** 页面展示历史、快照或属性
- **THEN** 页面 SHALL 复用共享原语或等价共享模式。
