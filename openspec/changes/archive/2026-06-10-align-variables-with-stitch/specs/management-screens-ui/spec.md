## MODIFIED Requirements

### Requirement: 管理页面按 Stitch 落地
应用程序 SHALL 按 Stitch refined / normalized 设计稿实现 Sync Targets、Variables、History 与 Settings 页面，并保持高信息密度的桌面工具风格。对于 Variables 页面，应用程序 SHALL 提供与 refined Variables 设计稿一致的 split pane 管理结构，包括顶部工具区、左侧变量列表/表格区、右侧详情与模板测试区，以及与页面状态匹配的操作层级。

#### Scenario: 管理页面结构一致
- **WHEN** 用户打开 Sync Targets、Variables、History 或 Settings 页面
- **THEN** 页面 SHALL 保持与 Stitch 设计稿一致的 split pane、property sheet、toolbar 与状态表达

#### Scenario: Variables 页面按 refined 稿显示
- **WHEN** 用户打开 Variables 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的变量列表区、详情/测试区、顶部工具区和主要操作按钮，而不是退化为松散卡片页或不对称布局

## ADDED Requirements

### Requirement: Variables 解析错误状态保持管理页壳层
应用程序 SHALL 按 Stitch 提供的 Variables parse error state 显示模板解析错误状态，并保留与正常 Variables 页面相同的应用壳和 split pane 主结构。

#### Scenario: Variables 模板解析错误状态显示
- **WHEN** 用户在 Variables 页面中遇到模板解析失败
- **THEN** 应用程序 SHALL 在保留导航、顶部栏、底部状态栏和主 split pane 的前提下，显示符合 Stitch parse error variant 的错误状态表达

### Requirement: Variables 错误状态提供修复动作
应用程序 SHALL 在 Variables 解析错误状态中提供紧邻错误原因的修复动作与诊断摘要，例如刷新解析、编辑变量、复制模板或重试测试。

#### Scenario: Variables 错误状态可恢复
- **WHEN** 某个变量或路径模板处于解析失败状态
- **THEN** 用户 SHALL 能直接看到错误摘要和修复动作，而不是只能看到静态错误提示
