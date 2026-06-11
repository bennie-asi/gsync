## Context

在高优先级三页完成细调之后，History 与 Settings 成为下一批最有价值的 Stitch 精修对象。它们已经具备正确的基本骨架，但仍然残留明显的页面级模板、硬编码标题/副标题、按钮区主次不够清晰，以及与 refined 稿相比还不够稳定的空态、快照区和辅助预览区表达。因为这两页都属于管理页体系中的“长期可见页面”，继续收紧它们可以让剩余管理页的质感与主壳层保持一致，而不是留下最后两块“结构差不多、细节还没收口”的区域。

这轮改动仍然应当是“细调”而不是重做架构：优先修正页面内的标题层级、分组语义、空态表达、重复模板与按钮层级；优先复用 `InfoCallout`、`PropertySheet`、`InspectorPanel`、`SnapshotFeed` 等已有原语；只有在页面内无法合理收敛时，才考虑对 shared primitives 做最小范围扩展。

## Goals / Non-Goals

**Goals:**
- 细调 History 的筛选区、主表格、右侧详情/快照区和 empty state。
- 细调 Settings 的左侧分组导航、中心设置区和右侧辅助预览/检查区。
- 收敛这两页中的页面级模板与硬编码说明，提高对 shared primitives 的一致复用。
- 保持这两页与既有 refined 管理页在密度、层级和状态表达上的一致性。

**Non-Goals:**
- 不重做主壳层、导航 rail、标题栏或全局主题系统。
- 不扩展新的同步功能、日志功能或设置业务行为。
- 不在本轮覆盖 Library、Game Details、Wizard、Conflict Resolution 等已经完成细调的页面。
- 不把这轮工作扩展成新的 primitives 体系重构。

## Decisions

### 决策 1：本轮只聚焦 History 与 Settings
- 决定：本 change 只覆盖 History 与 Settings。
- 原因：它们是“剩余管理页”中最自然的一批，范围清晰，也与上一轮高优先级三页细调形成连续批次。
- 备选方案：
  - 继续把其他页面重新纳入：会打断当前的批次节奏。

### 决策 2：History 先收敛 empty state 与快照区表达
- 决定：History 重点不是重做主表格，而是收紧 empty state、右侧详情/快照区和快照操作文案。
- 原因：当前骨架已经正确，最明显的问题在于状态表达和局部模板还不够 refined。
- 备选方案：
  - 重做整个 History 页面结构：收益有限，风险偏高。

### 决策 3：Settings 重点收敛分组导航语义和右侧辅助区
- 决定：Settings 重点处理左列被误用的“像数据表格的分组导航”、中间按钮主次，以及右侧辅助预览区的层级与 badge 语义。
- 原因：这页目前最大的问题不是布局大框架，而是“语义不够像 Settings refined 稿”。
- 备选方案：
  - 保持现状只改文案：无法解决分组导航与辅助区的层级问题。

### 决策 4：优先用页面内收敛解决问题
- 决定：优先在 `HistoryPage.xaml` / `SettingsPage.xaml` 和对应 ViewModel 内完成模板与文案收敛，不优先扩展 primitives。
- 原因：本轮是细调，保持低风险、低扩散最重要。
- 备选方案：
  - 先扩展 `SnapshotFeed` / `InspectorPanel` / `PropertySheet`：只有在页面层确实无法收口时再做。

## Risks / Trade-offs

- [History 的 empty state 已经存在，继续细调可能变成纯视觉微调] → 把重点放在与 refined 稿直接相关的结构和恢复动作，而不是无意义改色。
- [Settings 左列如果继续使用 `DenseDataGrid`，语义仍会偏数据页] → 本轮至少要把其标题、副标题和分组导航语义纠正到更像设置页；必要时再考虑替换容器。
- [右侧预览/辅助区的 badge 与按钮语义可能仍不统一] → 优先通过 ViewModel 字段与现有 Badge 变体对齐来收敛，不引入新的一套状态语义。
- [对 primitives 的修改范围若扩大，会拖慢这一轮] → 坚持“页面优先，原语最小扩展”的策略。

## Migration Plan

1. 先补 proposal，明确这是 History + Settings 的下一批细调，而不是全页面重开。
2. 在 specs 中补充 `management-screens-ui` 与 `stitch-app-shell` 对这两页细调的要求。
3. 在 tasks 中按 History → Settings → 验证 的顺序拆分实施。
4. 实施时先收紧页面内模板、标题与状态表达，再决定是否需要微调 shared primitives。
5. 最后通过构建与 Stitch refined screens 对照完成验收。

## Open Questions

- History 右侧快照区是否需要进一步切换为更纯粹的 `SnapshotFeed` 承载，而不是继续保留页面内局部模板。
- Settings 左侧分组导航是否在本轮就应从 `DenseDataGrid` 切到更语义化的容器，还是先在现有容器内收敛标题/层级即可。
- Settings 右侧辅助区中的按钮（Open / Folder 等）是否需要进一步按 refined 稿收紧主次和间距。