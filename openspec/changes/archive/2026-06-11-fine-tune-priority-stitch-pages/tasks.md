## 1. 细调 Game Details 页面

- [x] 1.1 对照 Stitch refined Game Details 屏幕，核对顶部标题层级、主操作区、内容项表格、底部历史/快照区与右侧检查区的最终细调目标
- [x] 1.2 调整 `GameDetailsPage.xaml`，让标题层级、底部历史/快照区和右侧检查区更一致地复用 `SectionHeader`、`ActivityFeed`、`SnapshotFeed`、`KeyValueList` 等 shared primitives
- [x] 1.3 视需要补充 `GameDetailsViewModel` 的适配属性，保持 `GameDetailsPage.xaml.cs` 中现有表格列初始化与跳转逻辑不变

## 2. 细调 Add Game Wizard 页面

- [x] 2.1 对照 Stitch Wizard Step 1-6 与 no-results 变体，核对步骤轨、步骤标题层级、重复卡片模板和恢复动作的细调目标
- [x] 2.2 调整 `AddGameWizardPage.xaml`，收敛 1/2/3/5 步重复出现的选项卡片结构，并清理页面中的占位式硬编码说明
- [x] 2.3 保持现有步骤切换逻辑与 `AddGameWizardPage.xaml.cs` / `AddGameWizardViewModel.cs` 中的状态切换方法不变，只细调 no-results 状态和步骤轨表达
- [x] 2.4 仅在页面内无法完成收敛时，最小范围扩展 `WizardStepRail` 以承接更稳定的步骤轨渲染职责

## 3. 细调 Conflict Resolution 页面

- [x] 3.1 对照 Stitch refined Conflict Resolution 屏幕，核对警告/风险提示、双栏对比区、基线区和底部动作区的最终细调目标
- [x] 3.2 调整 `ConflictResolutionPage.xaml`，用 `InfoCallout` 明确承载安全提示，并重新拉开本地/远端选择动作与保守动作的主次层级
- [x] 3.3 保持 `ConflictResolutionPage.xaml.cs` 中现有表格列初始化逻辑，只在 `ConflictResolutionViewModel` 中做必要的文案或语义支撑性补充

## 4. 验证 shared primitives 复用与设计稿对齐

- [x] 4.1 复查 `SectionHeader`、`InfoCallout`、`ActivityFeed`、`SnapshotFeed`、`KeyValueList`、`WizardStepRail` 在这三页中的复用是否减少了页面级重复结构
- [x] 4.2 逐项核对 Game Details、Conflict Resolution、Wizard Step 1-6 与 Add Game no-results 的结构、状态表达和按钮层级是否贴近对应 Stitch refined screens
- [x] 4.3 运行 WinUI 构建验证，确认这三页细调相关改动可编译通过且没有破坏既有页面行为
