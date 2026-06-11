## MODIFIED Requirements

### Requirement: 基于 Stitch 的统一应用壳
应用程序 SHALL 提供与 Stitch refined / normalized 屏幕一致的统一应用壳，包括紧凑左侧导航 rail、顶部标题/工具栏区域、主内容区与可选底部状态栏，并在各页面之间保持一致的桌面工具式布局。除 Library 外，Game Details、History、Settings、Add Game Wizard 与 Conflict Resolution 也 SHALL 在各自页面类型允许的范围内复用相同的壳层密度、边距节奏和共享原语，而不是形成与主壳层脱节的视觉分支。

#### Scenario: 主界面壳一致
- **WHEN** 用户在 Library、Sync Targets、Variables、History、Settings 之间切换
- **THEN** 应用程序 SHALL 保持相同的导航 rail、标题栏高度、内容边距与主布局结构

#### Scenario: 剩余页面复用同一壳层语言
- **WHEN** 用户进入 Game Details、Add Game Wizard 或 Conflict Resolution 等剩余主流程页面
- **THEN** 页面 SHALL 继续复用与主壳层一致的桌面工具式边距、层级和共享原语，而不是切换为风格割裂的独立页面体系

### Requirement: 基于 Stitch 的共享 UI 原语
应用程序 SHALL 提供可复用的 UI 原语以承载 Stitch 设计稿中的共享模式，包括 AppNavRail、AppTitleBar、StatusBar、DenseDataGrid、PropertySheet、InspectorPanel、ToolbarFilterRow、Badge 与 InfoCallout。对于 Add Game Wizard 与 Conflict Resolution，应用程序还 SHALL 复用向导步骤轨、对比面板、详情列表等共享模式，而不是为单页临时重造整套结构。

#### Scenario: 共享原语复用
- **WHEN** 两个或以上界面需要相同的导航、表格、属性面板或状态标记模式
- **THEN** 应用程序 SHALL 通过共享 UI 原语复用这些模式，而不是为每个页面单独重画结构

#### Scenario: 向导与冲突页复用共享模式
- **WHEN** Add Game Wizard 或 Conflict Resolution 需要展示步骤轨、属性摘要、状态提示或对比信息
- **THEN** 应用程序 SHALL 优先复用共享原语和一致的视觉节奏，而不是形成页面专属且不可复用的布局体系
