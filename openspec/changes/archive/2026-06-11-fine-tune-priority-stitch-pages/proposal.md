## Why

当前主壳层和大部分页面已经完成一轮 Stitch 对齐，但 `GameDetailsPage`、`AddGameWizardPage` 与 `ConflictResolutionPage` 仍然保留明显的页面级自定义布局痕迹，例如重复卡片模板、共享原语复用不足、按钮主次不清和安全提示层级不够稳定。现在继续收紧这三页，可以把“已经能用”的对齐状态推进到“结构和语义都更贴近 refined 稿”，避免核心流程页面继续成为与壳层语言脱节的例外。

## What Changes

- 继续细调 Game Details 页面，使标题层级、底部历史/快照区与右侧检查区更一致地复用共享原语。
- 收敛 Add Game Wizard 页面中的重复卡片模板、步骤轨表达和 no-results 状态表现，使六步流程更贴近 Stitch refined 稿。
- 细调 Conflict Resolution 页面的安全提示、双栏对比信息层级和动作主次表达，强化 safety-first 语义。
- 明确这三页应优先复用现有 shared primitives，而不是继续保留页面专属的 Border / StackPanel 组合分叉。

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `library-and-details-ui`: 细化 Game Details 页面在 refined 对齐后的标题层级、历史/快照区与右侧检查区要求，并补充 Add Game 主流程页面中的共享原语复用要求。
- `add-game-wizard-ui`: 细化 Add Game Wizard 六步流程中的步骤轨、重复卡片模板收敛与 no-results 状态表达要求。
- `conflict-resolution-ui`: 细化 Conflict Resolution 页面中安全提示、对比区、基线区与动作主次表达要求。
- `stitch-app-shell`: 强化这三页对统一壳层语言与 shared primitives 复用的约束，减少页面级自定义结构分叉。

## Impact

- 影响 `src/GSYNC.App/Pages/GameDetailsPage.*`、`AddGameWizardPage.*`、`ConflictResolutionPage.*` 及对应 ViewModel 的布局与展示模型。
- 可能影响 `src/GSYNC.App/Primitives/ActivityFeed.*`、`SnapshotFeed.*`、`KeyValueList.*`、`InfoCallout.*`、`WizardStepRail.*` 的复用方式或轻量扩展。
- 影响主 specs 中 `library-and-details-ui`、`add-game-wizard-ui`、`conflict-resolution-ui` 与 `stitch-app-shell` 的 refined 对齐要求，需要补充这一批页面的“细调”级约束。
- 后续验证将以 Stitch refined Game Details `0a1af726e04f4b56900569eff34aa148`、Conflict Resolution `2c5485592b864d9caa78302c82f60264`、Wizard Step 1-6 与 Add Game no-results `7f439bbb643e4f5a8e0600a7133e8304` 为主要基准。