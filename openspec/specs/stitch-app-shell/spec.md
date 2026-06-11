## Purpose

定义 GSYNC 应用在第二阶段 UI 实现中的统一壳层、共享 UI 原语与 Stitch 设计稿约束。

## Requirements

### Requirement: 基于 Stitch 的统一应用壳
应用程序 SHALL 提供与 Stitch refined / normalized 屏幕一致的统一应用壳，包括紧凑左侧导航 rail、顶部标题/工具栏区域、主内容区与可选底部状态栏，并在各页面之间保持一致的桌面工具式布局。在所有页面中，左侧导航 rail 默认 SHALL 以较窄宽度显示图标优先的收起态；用户触发展开控制后，rail SHALL 增宽并显示导航文字标签。设置按钮 SHALL 与其他导航项保持一致的视觉语言，并位于 rail 底部区域。除 Library 外，Game Details、History、Settings、Add Game Wizard 与 Conflict Resolution 也 SHALL 在各自页面类型允许的范围内复用相同的壳层密度、边距节奏和共享原语，而不是形成与主壳层脱节的视觉分支。在对 History 与 Settings 继续细调时，应用程序 SHALL 优先通过共享原语、统一标题层级和一致的状态表达来收敛差异，而不是继续扩大页面级模板分叉。

#### Scenario: 主界面壳一致
- **WHEN** 用户在 Library、Sync Targets、Variables、History、Settings 之间切换
- **THEN** 应用程序 SHALL 保持相同的导航 rail、标题栏高度、内容边距与主布局结构

#### Scenario: 默认显示紧凑导航 rail
- **WHEN** 用户首次进入应用或打开任一主页面
- **THEN** 左侧导航 rail SHALL 以较窄宽度显示，仅突出图标而不默认展开完整文字

#### Scenario: 用户展开导航 rail
- **WHEN** 用户点击 rail 的展开控制按钮
- **THEN** 左侧导航 rail SHALL 增加宽度并显示导航文字，同时主内容区域按新的 rail 宽度重新布局

#### Scenario: 设置按钮与导航项一致
- **WHEN** 用户查看左侧 rail 底部的设置入口
- **THEN** 设置按钮 SHALL 使用与其他导航项一致的样式体系，而不是成为独立风格元素

#### Scenario: 剩余页面复用同一壳层语言
- **WHEN** 用户进入 Game Details、Add Game Wizard 或 Conflict Resolution 等剩余主流程页面
- **THEN** 页面 SHALL 继续复用与主壳层一致的桌面工具式边距、层级和共享原语，而不是切换为风格割裂的独立页面体系

#### Scenario: History 与 Settings 细调仍保持壳层一致
- **WHEN** 用户查看经过继续细调后的 History 或 Settings 页面
- **THEN** 页面 SHALL 继续保持与主壳层一致的边距、标题层级和状态表达，而不是因局部精修而变成新的风格分支

### Requirement: 基于 Stitch 的共享 UI 原语
应用程序 SHALL 提供可复用的 UI 原语以承载 Stitch 设计稿中的共享模式，包括 AppNavRail、AppTitleBar、StatusBar、DenseDataGrid、PropertySheet、InspectorPanel、ToolbarFilterRow、Badge 与 InfoCallout。对于 Add Game Wizard 与 Conflict Resolution，应用程序还 SHALL 复用向导步骤轨、对比面板、详情列表等共享模式，而不是为单页临时重造整套结构。对于 History 与 Settings 的后续细调，应用程序 SHALL 优先复用现有 shared primitives，而不是继续在页面中保留重复的局部模板和硬编码结构。

#### Scenario: 共享原语复用
- **WHEN** 两个或以上界面需要相同的导航、表格、属性面板或状态标记模式
- **THEN** 应用程序 SHALL 通过共享 UI 原语复用这些模式，而不是为每个页面单独重画结构

#### Scenario: 向导与冲突页复用共享模式
- **WHEN** Add Game Wizard 或 Conflict Resolution 需要展示步骤轨、属性摘要、状态提示或对比信息
- **THEN** 应用程序 SHALL 优先复用共享原语和一致的视觉节奏，而不是形成页面专属且不可复用的布局体系

#### Scenario: History 与 Settings 细调优先复用共享结构
- **WHEN** History 或 Settings 页面需要继续收敛空态、快照区、分组导航或辅助预览区
- **THEN** 实现 SHALL 优先利用现有 shared primitives 和统一文案绑定，而不是在页面内继续扩散新的局部模板分叉

### Requirement: Stitch 设计稿强约束
涉及界面实现时，应用程序 SHALL 以 Stitch 提供的 refined / normalized 屏幕为主要视觉与布局参考，不得在未说明的情况下自行重设计信息架构、页面布局或状态表达。

#### Scenario: 页面实现对齐设计稿
- **WHEN** 开发实现某个已经存在 Stitch 设计稿的页面或状态变体
- **THEN** 代码实现 SHALL 以对应 Stitch 设计稿的结构、层次、密度与状态表达为准
