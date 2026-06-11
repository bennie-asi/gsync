## MODIFIED Requirements

### Requirement: Game Details 界面落地
应用程序 SHALL 按 Stitch 的 refined Game Details 设计稿实现单游戏详情页面，支持内容项检查、路径查看、同步历史预览与上下文操作。Game Details 页面 SHALL 保持桌面工具式主次分区：顶部上下文与主要操作区、中部内容项表格区、底部历史/快照区，以及右侧属性/检查面板，并与 Library 主壳层保持一致的密度与信息层级。Game Details 页面中的底部历史/快照区与右侧检查区 SHALL 优先复用共享原语（如 ActivityFeed、SnapshotFeed、KeyValueList 或等价共享模式），而不是继续保留页面专属的重复模板结构。

#### Scenario: Game Details 页面显示
- **WHEN** 用户从 Library 进入某个游戏的详情界面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的内容项列表、属性区与相关操作区

#### Scenario: Game Details 分区符合 refined 稿
- **WHEN** 用户查看 Game Details 页面
- **THEN** 页面 SHALL 保持顶部上下文卡、内容项表格、历史/快照区与右侧详情面板的稳定分区，而不是将这些内容重新拼接为松散卡片或单列布局

#### Scenario: Game Details 优先复用共享原语
- **WHEN** 页面需要展示历史活动、快照信息或属性检查内容
- **THEN** 实现 SHALL 优先复用共享原语，而不是在页面中继续复制新的 Border、StackPanel 和局部模板组合

### Requirement: Add Game 主流程与空结果状态保持一致体验
应用程序 SHALL 在 Add Game 主流程中保持与 Stitch refined Wizard 屏幕一致的步骤感与操作节奏；当出现 no-results 状态时，界面 SHALL 继续保留相同的向导上下文和恢复路径，而不是跳转到脱离流程的独立空页面。Add Game 主流程中的重复卡片结构和步骤轨表达 SHALL 尽量收敛到共享模板或共享原语，避免同一页面内部重复定义多份等价布局。

#### Scenario: Add Game 无结果仍保留流程上下文
- **WHEN** 用户在 Add Game 主流程中遇到没有结果的状态
- **THEN** 应用程序 SHALL 保留相同的步骤上下文、返回路径和下一步恢复动作，而不是丢失当前向导骨架

#### Scenario: Add Game 重复卡片模板被收敛
- **WHEN** 多个步骤展示相同语义的选项卡片或步骤轨内容
- **THEN** 实现 SHALL 优先通过共享模板或共享原语复用这些结构，而不是在同一页面内重复定义多份相同布局
