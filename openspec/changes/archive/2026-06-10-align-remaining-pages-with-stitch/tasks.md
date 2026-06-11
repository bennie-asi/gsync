## 1. 收敛剩余壳层页面结构

- [x] 1.1 对照 Stitch refined Game Details 屏幕，确认顶部上下文区、内容项表格区、历史/快照区与右侧详情面板的最终层级
- [x] 1.2 调整 `GameDetailsPage` 与相关 ViewModel，统一主操作区、内容项表格和右侧属性/检查区的桌面工具式布局
- [x] 1.3 对照 Stitch refined History 与 Settings 屏幕，确认 History 的筛选/详情/快照结构以及 Settings 的分组导航/中心编辑/右侧辅助区结构
- [x] 1.4 调整 `HistoryPage` 与相关 ViewModel，使顶部筛选区、主表格和右侧详情/快照区符合 refined 稿层级
- [x] 1.5 调整 `SettingsPage` 与相关 ViewModel，使分组导航、中心设置区和右侧预览/检查区符合 refined 稿层级

## 2. 落地剩余状态变体

- [x] 2.1 根据 Stitch refined empty state 收敛 History 空状态，保持与正常 History 页面相同的壳层和主结构
- [x] 2.2 校验 History 空状态与正常态之间的切换不会破坏筛选区、详情区和页面密度一致性
- [x] 2.3 复查 Settings、Game Details 与现有错误降级结构，确保剩余页面在失败或空状态下仍保留统一壳层语言

## 3. 完成 Add Game Wizard 对稿

- [x] 3.1 对照 Stitch Wizard Step 1-6 与 no-results 变体，确认六步流程的固定顺序、左侧步骤轨和底部操作条职责
- [x] 3.2 调整 `AddGameWizardPage` 与相关 ViewModel，使每一步主内容区、主次操作和步骤上下文符合 refined 稿
- [x] 3.3 落地 Add Game no-results 状态，确保其保留相同向导骨架并提供清晰恢复动作

## 4. 完成 Conflict Resolution 对稿

- [x] 4.1 对照 Stitch refined Conflict Resolution 屏幕，确认本地/远端双栏对比、基线摘要和冲突文件列表的最终层级
- [x] 4.2 调整 `ConflictResolutionPage` 与相关 ViewModel，使主要解决动作、辅助动作和风险提示符合 safety-first 的 refined 布局
- [x] 4.3 校验 Conflict Resolution 中的对比信息和动作分层不会退化为单栏信息页或模糊按钮集合

## 5. 统一壳层复用与验证

- [x] 5.1 复查 `MainWindow`、共享 primitives 与剩余页面，确保 Game Details、History、Settings、Wizard、Conflict Resolution 复用一致的边距、密度和共享原语
- [x] 5.2 逐项核对剩余页面与对应 Stitch refined screen / state variant 的结构和层级（Game Details、History、Settings、Wizard Step 1-6、Add Game no-results、Conflict Resolution）
- [x] 5.3 运行 WinUI 构建验证，确认剩余页面相关改动可编译通过且未破坏既有对稿页面
