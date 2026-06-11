## Context

当前仓库已经把导航 rail、Sync Targets 与 Variables 向 Stitch refined 方向收敛，但剩余页面仍分散在不同成熟度的布局实现中：Game Details 已有桌面工具骨架但层级仍需锁定；History 与 Settings 仍需补齐 refined 稿中的结构与状态要求；Add Game Wizard 与 Conflict Resolution 已有静态页面原型，但尚未在 OpenSpec 中形成清晰、可执行的界面契约。若不先把这些页面的规格补齐，后续实现会继续出现“局部对稿、整体失真”的问题。

本次变更的目标不是引入新业务能力，而是把剩余主流程页面全部收拢到同一套 Stitch 驱动的桌面工具式 UI 体系中：统一壳层、统一信息密度、统一状态变体处理方式，并尽量复用现有共享原语。这样后续 `/opsx:apply` 时可以按照稳定的屏幕契约逐页推进，而不是边做边猜。

## Goals / Non-Goals

**Goals:**
- 明确 Game Details、History、Settings、Add Game Wizard、Conflict Resolution 的 refined 结构与状态要求。
- 把 Add Game Wizard 六步流程与 no-results 变体整理成单独能力，避免向导页继续依赖临时布局判断。
- 把 Conflict Resolution 的双栏对比、安全提示和显式动作整理成单独能力，保证冲突处理保持 safety-first。
- 让 History / Settings 与已完成的 Sync Targets / Variables 一样，成为稳定的高密度桌面管理页。
- 明确剩余页面如何复用统一壳层与共享原语，减少实现阶段的页面级分叉。

**Non-Goals:**
- 不新增同步引擎、变量解析、存储提供器或游戏清单相关后端能力。
- 不在本次设计中重新定义导航 rail 的交互模型或主题系统。
- 不要求本次设计确定每个像素值、字体级别或图标资源文件，只锁定结构、层级和状态表达。
- 不将 Conflict Resolution 改造成全新的独立流程；它仍是现有桌面工具体验中的聚焦诊断/解决界面。

## Decisions

### 决策 1：剩余页面按“壳层页 / 向导页 / 冲突页”三类收敛
- 决定：将剩余页面划分为三类进行规范化：
  1. 壳层内页面：Game Details、History、Settings；
  2. 主流程向导：Add Game Wizard 六步与 no-results；
  3. 聚焦处理界面：Conflict Resolution。
- 原因：这三类页面复用模式不同。壳层页强调统一 rail / title bar / density；向导页强调步骤轨与底部主次操作；冲突页强调对比与风险控制。分层定义比把所有页面硬塞进单一 spec 更清晰。
- 备选方案：
  - 继续全部挂在 `management-screens-ui` 下：会让向导与冲突页的要求变得模糊。
  - 每个页面各建单独 capability：粒度过碎，后续维护成本高。

### 决策 2：优先复用现有共享原语，而不是为剩余页面重新发明骨架
- 决定：实现层应尽量复用 `DenseDataGrid`、`PropertySheet`、`InspectorPanel`、`WizardStepRail`、`InfoCallout`、`ResizableTableView`、`Badge` 等现有 primitives；仅在设计稿明确缺口时做小范围补齐。
- 原因：已有页面已经围绕这些 primitives 建立了桌面工具式节奏，继续复用能降低页面之间的漂移风险。
- 备选方案：
  - 每页按设计稿单独拼布局：短期更快，但会把相同模式做出多个变体。

### 决策 3：所有正常态与变体态都必须保留相同页面骨架
- 决定：History empty、Add Game no-results 以及未来错误/空态都应保持相同壳层或向导骨架，只替换焦点区域内容。
- 原因：仓库文档已明确要求空态和失败态不能把桌面工具页变成营销页或全屏异常页。
- 备选方案：
  - 为每种状态单独做全页重排：会破坏用户对页面结构的认知连续性。

### 决策 4：Game Details、History、Settings 继续沿用高密度桌面页节奏
- 决定：这三页继续使用“顶部操作/筛选 + 主表格或主体内容 + 右侧详情/分组面板”的桌面工具式层级，不回退到大卡片或松散分段。
- 原因：这与 Library、Sync Targets、Variables 的方向一致，也符合 implementation notes 对管理页的要求。
- 备选方案：
  - 将 Settings 做成宽松卡片中心页：会与其余页面明显脱节。
  - 将 History 做成只有日志表的单栏页：会丢失诊断与快照详情层。

### 决策 5：Add Game Wizard 必须明确六步结构与底部操作条职责
- 决定：Wizard spec 单独规定六个步骤的固定顺序、左侧步骤轨、右侧主内容、底部 Cancel/Back/Next 或 Finish 操作条，以及 no-results 变体的呈现方式。
- 原因：向导最容易在实现时被“合并步骤”或“把空态塞到普通列表里”，单独成 spec 可以避免主流程漂移。
- 备选方案：
  - 仅在设计文档里描述：约束力不够，apply 阶段容易走样。

### 决策 6：Conflict Resolution 保持双栏对比 + 基线摘要 + 显式动作
- 决定：Conflict Resolution 单独定义双栏版本对比、基线摘要、受影响文件列表和底部显式解决动作，并强调 backup-first 与风险提示。
- 原因：这是产品 safety-first 定位最敏感的界面，必须避免被实现成普通信息页或随意排布的按钮集合。
- 备选方案：
  - 将其视为普通详情页的一种：会削弱冲突处理的风险控制语义。

## Risks / Trade-offs

- [剩余页面类型较多，单次变更范围偏大] → 通过 capability 拆分（wizard / conflict / shell-page）控制复杂度，并在 tasks 中按页面簇推进。
- [已有页面原型与 refined 稿存在细节差距] → 先在 specs 锁定结构和层级，再在实现阶段按 screen ID 做逐项收口。
- [共享原语可能不足以完整覆盖向导或冲突页] → 优先复用原语骨架；若确有缺口，仅新增最小必要的展示原语而不重造壳层。
- [Settings 与 History 容易被做成“普通表单页”或“普通日志页”] → 在 `management-screens-ui` 中明确其必须保持高密度桌面工具分区与右侧详情/预览语义。
- [Conflict Resolution 动作过多会导致界面噪音] → 通过“对比区域 + 基线区 + 底部动作条”固定层级，把危险动作与辅助动作分开。

## Migration Plan

1. 在 proposal 中锁定新增 capability 与需修改的既有 capability。
2. 在 specs 中补齐 Wizard、Conflict Resolution 以及既有壳层/管理页能力的 delta 要求。
3. 在 tasks 中按 Game Details、History/Settings、Wizard、Conflict Resolution 和验证五个批次组织实施。
4. apply 阶段优先复用现有 primitives 与页面骨架，逐页对照 Stitch refined screens 做实现。
5. 完成后按 screen ID 与 state variant 做逐项对稿，并通过 WinUI 构建验证回归。

## Open Questions

- Settings refined 稿中右侧预览/检查区最终是更偏实时预览还是更偏状态检查，当前先锁定为“辅助检查/预览面板”。
- Add Game no-results 状态是否需要单独强调“返回上一步”和“改用自定义路径”两类恢复动作，当前先锁定为保留同一向导骨架与主次操作区。
- Conflict Resolution 是否需要把“Keep both”设为次级动作而非主要动作，当前设计先只要求显式分层，不写死按钮强调级别。