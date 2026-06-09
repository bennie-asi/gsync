## ADDED Requirements

### Requirement: 基于 Stitch 的统一应用壳
应用程序 SHALL 提供与 Stitch refined / normalized 屏幕一致的统一应用壳，包括紧凑左侧导航 rail、顶部标题/工具栏区域、主内容区与可选底部状态栏，并在各页面之间保持一致的桌面工具式布局。

#### Scenario: 主界面壳一致
- **WHEN** 用户在 Library、Sync Targets、Variables、History、Settings 之间切换
- **THEN** 应用程序 SHALL 保持相同的导航 rail、标题栏高度、内容边距与主布局结构

### Requirement: 基于 Stitch 的共享 UI 原语
应用程序 SHALL 提供可复用的 UI 原语以承载 Stitch 设计稿中的共享模式，包括 AppNavRail、AppTitleBar、StatusBar、DenseDataGrid、PropertySheet、InspectorPanel、ToolbarFilterRow、Badge 与 InfoCallout。

#### Scenario: 共享原语复用
- **WHEN** 两个或以上界面需要相同的导航、表格、属性面板或状态标记模式
- **THEN** 应用程序 SHALL 通过共享 UI 原语复用这些模式，而不是为每个页面单独重画结构

### Requirement: Stitch 设计稿强约束
涉及界面实现时，应用程序 SHALL 以 Stitch 提供的 refined / normalized 屏幕为主要视觉与布局参考，不得在未说明的情况下自行重设计信息架构、页面布局或状态表达。

#### Scenario: 页面实现对齐设计稿
- **WHEN** 开发实现某个已经存在 Stitch 设计稿的页面或状态变体
- **THEN** 代码实现 SHALL 以对应 Stitch 设计稿的结构、层次、密度与状态表达为准
