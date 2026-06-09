## Why

当前仓库已经完成架构基线、核心模块骨架以及可编译的 WinUI 应用壳，但 UI 仍然主要停留在占位页与基础导航层级。下一阶段需要把已经确认过的 Stitch 设计稿真正落地为高保真的桌面工具界面，并以设计稿作为唯一视觉与布局参考，避免实现过程中偏离既定的桌面 utility 方向。

## What Changes

- 将现有占位式 App 壳推进为符合 Stitch 设计稿的高保真桌面工具界面
- 实现严格遵循 Stitch 设计稿的共享 UI 原语与应用壳（导航 rail、标题栏、状态栏、表格/属性面板等）
- 先落地主流程主界面：Library、Game Details、Sync Targets、Variables、History、Settings
- 按 Stitch 已提供的状态变体实现关键空状态和错误状态，而不是临时拼接占位 UI
- 为后续 Add Game 向导、Conflict Resolver、快照恢复流预留结构与导航接入点
- 将 UI 实现约束明确为：涉及界面时必须以 Stitch refined / normalized 屏幕为准，不自行重设计

## Capabilities

### New Capabilities
- `stitch-app-shell`: 基于 Stitch 设计稿的统一应用壳、导航、标题栏、状态栏与共享 UI 原语
- `library-and-details-ui`: 基于 Stitch 设计稿的 Library 与 Game Details 主界面
- `management-screens-ui`: 基于 Stitch 设计稿的 Sync Targets、Variables、History、Settings 管理界面与状态变体

### Modified Capabilities
- `tech-stack`: 明确下一阶段 UI 实现必须遵循 Stitch 设计稿，并以现有 WinUI 3 技术栈继续推进界面落地
- `solution-structure`: 增补 App 层 ViewModels、Views、共享 UI primitives 的组织约束

## Impact

- 主要影响代码：`src/GSYNC.App/**`
- 可能补充少量 ViewModel / UI 支撑模型到 `src/GSYNC.Core/**`
- 参考文档：`docs/ui/implementation-notes.md`、`docs/ui/prototype-inventory.md`、`docs/implementation-handoff.md`
- 设计来源：Stitch 项目 `13407775155513183369`，设计系统 `assets/3772949807639653402`
- 后续实现必须以 refined / normalized Stitch 屏幕为准，不能自由改动信息架构和视觉布局
