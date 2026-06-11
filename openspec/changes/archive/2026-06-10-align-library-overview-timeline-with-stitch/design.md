## Context

用户明确指出：Library 页面右侧总览区域的下半部分应该采用**时间线方式**来表达，而不是继续停留在普通的静态指标/属性列表形式，并且这次必须直接参照 Stitch 设计稿。当前 `HomePage.xaml` 的右侧区域仍然只包含 `OverviewSheet`，其内容由 `OverviewMetrics` 和 `Stats` 两组键值对堆叠而成，没有体现出设计稿里更强调的时间线式活动表达。因此需要单独把 Library 右侧“Overview 下方”的表现方式纠正到 refined 稿方向。

这次变更范围应保持收敛：不重做整个 Library 页面，也不扩展到其他页面，只聚焦 Library 右侧总览区下半部分的结构和 shared primitive 复用方式。由于 `LibraryPageViewModel` 已经拥有 `Activity` 数据，并且仓库已有 `ActivityFeed` primitive，这次更像是把“已有数据 + 已有原语”真正接到设计稿要求的位置上，而不是发明新的结构。

## Goals / Non-Goals

**Goals:**
- 让 Library 页面右侧 Overview 下方改为时间线式展示，贴近 Stitch refined Library 稿。
- 复用现有 `ActivityFeed` 与 `LibraryPageViewModel.Activity`，避免继续在 Library 右侧保留纯键值块堆叠。
- 保持 Library 页面右侧“主操作区 + 总览 + 时间线”的层级更清晰。
- 只修正与该时间线区域直接相关的结构、文案和数据绑定。

**Non-Goals:**
- 不重做 Library 左侧主表格、顶部搜索工具区或整体壳层。
- 不扩展到 History、Settings、Wizard、Conflict Resolution 等其他页面。
- 不新增新的业务行为、同步逻辑或活动事件来源。
- 不在本轮把整个 Library 右侧区域完全重构为新容器体系。

## Decisions

### 决策 1：聚焦 Library 右侧总览区下半部分，不扩大到整个 Library 重做
- 决定：本 change 只修正 Library 页面右侧区域中“Overview 下方应该是时间线”的问题。
- 原因：用户反馈非常具体，说明这是一个设计稿对齐遗漏点，适合做精准修正。
- 备选方案：
  - 把整个 Library 页面再次细调：范围过大，不符合当前反馈的聚焦性。

### 决策 2：优先复用 `ActivityFeed` 和现有 `Activity` 数据
- 决定：时间线区域优先使用 `src/GSYNC.App/Primitives/ActivityFeed.xaml`，数据继续来自 `LibraryPageViewModel.Activity`。
- 原因：仓库里已经有合适的 primitive 和数据模型，不需要再创造新的时间线控件或活动记录结构。
- 备选方案：
  - 在 `HomePage.xaml` 再手写一套时间线模板：会制造新的页面级分叉。

### 决策 3：保留 Overview 指标，但把其下方明确拆出活动时间线区
- 决定：右侧区域仍保留 `Overview`，但在其下方显式增加 timeline/activity 区，而不是把所有信息继续混在一个 PropertySheet 中。
- 原因：用户明确说“总览下方应该是时间线”，这意味着设计稿中是层级分离，不是把时间线混入总览指标。
- 备选方案：
  - 仅把 `Stats` 替换成另一组键值：无法满足“时间线方式”的反馈。

### 决策 4：最小范围调整 ViewModel 命名和文案，避免改动行为逻辑
- 决定：优先补充标题/副标题/时间线说明类属性，继续复用现有 `Activity` 集合；如需要，只新增轻量文案字段，不修改 Library 的交互和导航逻辑。
- 原因：这次目标是视觉与层级对齐，不是改功能。
- 备选方案：
  - 重构 `LibraryPageViewModel` 的活动数据结构：风险高且收益低。

## Risks / Trade-offs

- [Library 右侧空间有限，增加时间线后可能显得拥挤] → 保持上方主操作区不变，控制 Overview 与 Activity 两块区域的高度与间距，优先匹配设计稿层级。
- [当前 `OverviewSheet` 结构过于一体化，拆开后需要微调容器比例] → 先做最小拆分，保留现有右侧列宽与总布局。
- [如果继续复用现有 `Activity` 数据，内容可能与设计稿文案不完全一致] → 优先通过 ViewModel 微调文案与标题，而不是更换数据模型。
- [再次在页面里手写时间线会造成重复结构] → 坚持优先复用 `ActivityFeed`，除非页面层确实无法承载设计稿要求。

## Migration Plan

1. 在 proposal 中锁定这次是 Library 右侧时间线区域的精准修正。
2. 在 specs 中补充 `library-and-details-ui` 对 Library 右侧总览下方时间线表达的要求。
3. 在 tasks 中按“确认设计目标 → 调整 HomePage → 调整 LibraryPageViewModel → 验证”拆分实施。
4. 实施时优先使用 `ActivityFeed` 和现有 `Activity` 数据，只做最小文案与结构补充。
5. 最后通过构建和 Stitch Library refined screen 对照验收。

## Open Questions

- 时间线区域是直接位于 `Overview` 下方，还是和 `Overview` 并列为两个独立 `PropertySheet` / `InspectorPanel` 容器，当前建议以设计稿为准并优先保持“上下分层”。
- 当前 `Activity` 数据是否需要按设计稿再压缩成更短的主副文案，还是现有结构即可直接映射到时间线。