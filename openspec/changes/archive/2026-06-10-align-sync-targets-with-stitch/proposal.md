## Why

当前我们已经按 Stitch refined 设计稿推进了 Library、Game Details 与 History 页面，下一步需要把 Sync Targets 页面也收敛到同一套桌面工具式结构。现在推进这一页，可以避免管理类页面在 split pane、属性编辑器、失败状态和信息密度上继续出现不一致。

## What Changes

- 将 Sync Targets 页面按 Stitch refined Sync Targets 设计稿重新对齐，保持与现有壳层一致的桌面工具式 split pane 结构。
- 调整 Sync Targets 页面的顶部工具区、左侧目标列表/表格区、右侧属性编辑区和操作按钮布局，使其与 Stitch 页面层级一致。
- 明确 Sync Targets 页面中 WebDAV、本地目录和预留目标类型的状态表达与信息密度要求。
- 将 Sync Targets 的连接失败状态按 Stitch 提供的 failure variant 落地，确保失败状态不破坏既有壳层与管理页面结构。
- 收敛 Sync Targets 页面与已完成 Library/History 页面对齐后的视觉密度、边距和属性面板表达方式。

## Capabilities

### New Capabilities
- `sync-targets-failure-state-ui`: 定义 Sync Targets 页面连接失败场景下的专用状态变体与恢复动作展示要求。

### Modified Capabilities
- `management-screens-ui`: 调整 Sync Targets 页面在管理类页面中的结构、工具栏、split pane、属性编辑区和失败状态要求。

## Impact

- 影响 `src/GSYNC.App/Pages/SyncTargetsPage.*` 与对应 ViewModel 的静态布局和展示模型。
- 可能影响共享原语在管理页中的使用方式，尤其是表格、属性面板、状态表达和错误/失败态展示。
- 影响主 specs 中管理页面与状态变体的定义，需要补充 Sync Targets refined 页面与 failure state 的明确要求。
- 后续验证将以 Stitch 项目 `13407775155513183369` 中 refined Sync Targets 屏幕 `ee786c958cf24700b1ddb593a37697d2` 和 failure state `99e89eaae486405b962d54f3e258a17f` 为主要基准。