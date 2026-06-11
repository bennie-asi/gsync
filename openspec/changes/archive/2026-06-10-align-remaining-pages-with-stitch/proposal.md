## Why

当前导航 rail、Sync Targets 与 Variables 已开始向 Stitch refined 设计稿收敛，但其他核心页面仍未统一对齐，导致主壳层之下的页面层级、密度和状态表达继续出现不一致。现在补齐剩余页面，可以把桌面工具式 UI 从“部分页面对稿”推进到“整套主流程和管理页对稿”，避免后续实现继续建立在半完成的视觉基线上。

## What Changes

- 将 Game Details、Settings、History、Conflict Resolution 与 Add Game Wizard 按 Stitch refined / normalized 设计稿继续对齐，统一到现有桌面工具式壳层与共享原语之下。
- 明确 Add Game Wizard 六步流程在真实 UI 中的步骤结构、主次操作区和 no-results 变体要求。
- 明确 Conflict Resolution 对话界面的对比布局、风险提示和显式动作区要求，保持安全优先的桌面工具风格。
- 收敛 History 与 Settings 页面在信息层级、工具区、分组与高密度布局上的要求，使其与已对齐的管理页和主壳层保持一致。
- 补充剩余页面的状态变体与结构要求，确保空状态、错误状态或向导分支状态仍保持相同壳层与页面骨架。

## Capabilities

### New Capabilities
- `add-game-wizard-ui`: 定义 Add Game Wizard 六步流程、步骤导航、主操作区以及 no-results 状态的界面要求。
- `conflict-resolution-ui`: 定义 Conflict Resolution 界面的双栏对比、安全提示和显式解决动作要求。

### Modified Capabilities
- `library-and-details-ui`: 补充 Game Details 页面 refined 桌面工具式结构要求，并将 Add Game no-results 状态纳入主流程相关 UI 能力范围。
- `management-screens-ui`: 扩展 History 与 Settings 页面在高密度桌面管理页中的结构、分组、状态变体和设计稿对齐要求。
- `stitch-app-shell`: 明确剩余页面在统一壳层内复用相同 rail、title bar、content density 与状态栏约束，不允许各页自行偏离 Stitch 层级。

## Impact

- 影响 `src/GSYNC.App/Pages/GameDetailsPage.*`、`SettingsPage.*`、`HistoryPage.*`、`AddGameWizardPage.*`、`ConflictResolutionPage.*` 及相关 ViewModel 的布局与展示模型。
- 可能影响共享 UI 原语的复用范围，尤其是 wizard shell、property sheet、inspector panel、状态提示和冲突对比区。
- 影响主 specs 中主页面、管理页面、向导流程与冲突处理界面的定义，需要补充剩余 refined 屏幕和状态变体的明确要求。
- 后续验证将以 Stitch 项目 `13407775155513183369` 中的 refined Game Details、History、Settings、Conflict Resolution、Wizard Step 1-6 及 Add Game no-results 屏幕为主要基准。