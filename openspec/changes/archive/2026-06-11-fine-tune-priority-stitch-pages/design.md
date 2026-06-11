## Context

当前主壳层、导航 rail 和大部分管理页已经完成一轮 Stitch 对齐，但高频核心流程中的三页——`GameDetailsPage`、`AddGameWizardPage`、`ConflictResolutionPage`——仍保留明显的页面级自定义痕迹：部分结构虽然已经接近 refined 稿，但在标题层级、重复模板、共享原语复用、动作主次和安全提示表达上还不够稳定。由于这三页直接覆盖“查看详情 → 添加游戏 → 解决冲突”三段关键路径，现在继续细调它们，可以把 UI 从“已经基本对齐”推进到“核心流程更统一、更像设计稿”。

本次变更不是重新设计页面，也不是重做导航或全局壳层；它更像是一轮精修：把已经存在的页面骨架继续压回 `SectionHeader`、`InfoCallout`、`ActivityFeed`、`SnapshotFeed`、`KeyValueList`、`WizardStepRail` 等 shared primitives 上，并清理仍然残留在页面里的重复 Border / StackPanel 组合和占位式表达。

## Goals / Non-Goals

**Goals:**
- 细化 Game Details 页面在标题层级、底部历史/快照区和右侧检查区上的 refined 对齐。
- 细化 Add Game Wizard 的步骤轨、重复卡片模板与 no-results 状态表达。
- 细化 Conflict Resolution 的安全警告、对比区和动作主次表达。
- 强化这三页对共享原语的复用，减少页面级分叉。
- 保持本轮改动为“细调”而非大规模架构重构。

**Non-Goals:**
- 不重新定义主壳层、导航 rail 或全局主题系统。
- 不重做向导步骤切换机制或页面路由模型。
- 不扩展新的业务行为、冲突算法或同步规则。
- 不在本轮覆盖 History / Settings 等其余页面；它们可以留到下一批细调。

## Decisions

### 决策 1：先聚焦三页，不扩大到全部剩余页面
- 决定：本 change 只覆盖 Game Details、Add Game Wizard、Conflict Resolution。
- 原因：这三页细调价值最高，且用户已明确选定“高优先级三页”；控制范围可以更快完成一轮高质量细调，而不把 History / Settings 也拖进来。
- 备选方案：
  - 一次性把 History / Settings 一并纳入：范围过大，更像新一轮全页面对齐而不是“下一批细调”。

### 决策 2：优先收敛页面内重复，而不是先扩展 primitives
- 决定：能在页面内用共享模板和现有 primitives 收敛的，优先在页面层完成；只有在页面内仍无法逼近 Stitch 时，才最小范围扩展 primitive。
- 原因：这是最稳妥的“细调”策略，避免把局部 UI 问题升级成 primitives 重构。
- 备选方案：
  - 先扩展 `WizardStepRail` / `SnapshotFeed` / `ActivityFeed` 再回填页面：风险更高，容易把细调变成结构重写。

### 决策 3：Game Details 优先压回 shared primitives 路线
- 决定：Game Details 的底部历史/快照区和右侧检查区优先改成 `ActivityFeed`、`SnapshotFeed`、`KeyValueList` 组合，而不是继续保留页面内联模板。
- 原因：该页目前最容易继续“自己画一套”；优先压回 shared primitives 才能保证它与其他 refined 页保持统一节奏。
- 备选方案：
  - 仅做 spacing / 文案微调：无法解决结构层面的分叉。

### 决策 4：Add Game Wizard 本轮只做低风险收敛
- 决定：本轮只收敛重复卡片模板、标题/副标题绑定和 no-results 表达；步骤切换机制保持现有模式，不重做为新的内容交换架构。
- 原因：向导流程牵涉 step 状态与按钮事件，低风险细调更符合当前目标。
- 备选方案：
  - 直接把步骤切换改成全新容器模式：收益有限，但风险偏高。

### 决策 5：Conflict Resolution 强化 safety-first 语义
- 决定：使用 `InfoCallout` 承载警告/风险提示，并重新区分本地/远端选择动作与保守动作的视觉权重。
- 原因：该页最核心的不是“漂亮”，而是用户能否在压力场景下看懂风险、看懂动作。
- 备选方案：
  - 保持当前普通副标题 + 平铺按钮：难以稳定体现 safety-first。

## Risks / Trade-offs

- [对 shared primitives 的扩展过早扩大改动范围] → 先在页面内完成模板收敛，只在必要时做最小扩展。
- [向导步骤轨可能仍与 Stitch 有细节差异] → 本轮先收敛重复模板与 no-results，若仍有明显偏差，再单独处理 `WizardStepRail`。
- [Conflict Resolution 按钮主次可能与设计稿强调存在解读差异] → 以对应 Stitch refined screen 为最终判断依据，不凭主观偏好重设计。
- [Game Details 改用 shared primitives 后，ViewModel 数据形状需要轻微适配] → 保留现有表格列和导航逻辑，只添加适配型属性，不动核心行为。

## Migration Plan

1. 先补 proposal，明确这是“高优先级三页细调”而不是新一轮全页面对齐。
2. 在 specs 中对 `library-and-details-ui`、`add-game-wizard-ui`、`conflict-resolution-ui`、`stitch-app-shell` 增加“细调”级约束。
3. 在 tasks 中按 Game Details → Wizard → Conflict Resolution → 验证 的顺序拆分实施。
4. 实施时优先复用现有 shared primitives，并尽量保持 code-behind / ViewModel 逻辑稳定。
5. 最后通过构建 + Stitch refined 屏幕对照完成验收。

## Open Questions

- `GameDetailsPage` 底部历史/快照区是否需要继续保留当前“类似 tab”的按钮表达，还是直接明确成双区并列展示。
- `AddGameWizardPage` 是否需要在本轮顺手把步骤轨渲染职责下沉到 `WizardStepRail`，还是仅做页面内模板收敛即可。
- `ConflictResolutionPage` 中本地/远端两个选择动作在 refined 稿里是否具有明确主次偏向，当前设计先只要求层级清楚，不预设偏向。