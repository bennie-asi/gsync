## MODIFIED Requirements

### Requirement: 管理页面按 Stitch 落地
应用程序 SHALL 按 Stitch refined / normalized 设计稿实现 Sync Targets、Variables、History 与 Settings 页面，并保持高信息密度的桌面工具风格。对于 Sync Targets 页面，应用程序 SHALL 提供与 refined Sync Targets 设计稿一致的 split pane 管理结构，包括顶部工具区、左侧目标列表/表格区、右侧属性编辑区，以及与页面状态匹配的操作层级。

#### Scenario: 管理页面结构一致
- **WHEN** 用户打开 Sync Targets、Variables、History 或 Settings 页面
- **THEN** 页面 SHALL 保持与 Stitch 设计稿一致的 split pane、property sheet、toolbar 与状态表达

#### Scenario: Sync Targets 页面按 refined 稿显示
- **WHEN** 用户打开 Sync Targets 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的目标列表区、属性编辑区、顶部工具区和主要操作按钮，而不是退化为松散卡片页或不对称布局

## ADDED Requirements

### Requirement: Sync Targets 失败状态保持管理页壳层
应用程序 SHALL 按 Stitch 提供的 Sync Targets failure state 显示连接失败状态，并保留与正常 Sync Targets 页面相同的应用壳和 split pane 主结构。

#### Scenario: Sync Targets 连接失败状态显示
- **WHEN** 用户在 Sync Targets 页面中遇到目标连接测试失败
- **THEN** 应用程序 SHALL 在保留导航、顶部栏、底部状态栏和主 split pane 的前提下，显示符合 Stitch failure variant 的失败状态表达

### Requirement: Sync Targets 失败状态提供恢复动作
应用程序 SHALL 在 Sync Targets 失败状态中提供紧邻失败原因的恢复动作与诊断摘要，例如刷新、重试连接测试或编辑目标配置。

#### Scenario: Sync Targets 失败状态可恢复
- **WHEN** 某个同步目标处于失败状态
- **THEN** 用户 SHALL 能直接看到失败摘要和恢复动作，而不是只能看到静态错误提示
