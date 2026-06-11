## Context

当前项目已经完成 Library、Game Details 与 History 三个 refined 页面的一轮 Stitch 对稿，管理类页面中只剩 Sync Targets、Variables、Settings 还未全部收口。Sync Targets 是其中最关键的一页，因为它不仅是典型的 split pane 管理页面，还承担了 WebDAV、本地目录与未来扩展目标类型的状态展示、连接测试、属性编辑和失败恢复入口。

当前仓库中已有 `management-screens-ui` 对管理页高密度桌面工具风格提出原则性要求，但还没有把 refined Sync Targets 的具体结构和 failure state 明确成可执行约束。此次变更需要把“正常管理页结构”和“连接失败状态变体”一起定义清楚，确保实现时不会再退回到泛化卡片页或信息层级失衡的版本。

## Goals / Non-Goals

**Goals:**
- 明确 Sync Targets 页面对齐 Stitch refined 屏幕的最终结构，作为实现和验收基准。
- 明确 Sync Targets 的顶部工具区、左侧目标列表/表格、右侧属性编辑器和主要动作布局。
- 明确连接失败状态如何在不破坏主壳层和 split pane 结构的前提下呈现。
- 让 Sync Targets 与已经对齐过的 Library / History 页面在密度、边距、属性面板风格上保持统一。

**Non-Goals:**
- 不扩展新的存储提供器能力或后端连接逻辑。
- 不改变 Sync engine、Storage provider 或变量解析的架构规则。
- 不在本次设计中定义 VariablesPage / SettingsPage 的详细布局。
- 不要求本次设计完成真实网络调用，只约束 UI 布局、状态语义和失败态表达。

## Decisions

### 决策 1：Sync Targets 保持典型的管理页 split pane
- 决定：页面采用左侧目标列表/表格，右侧属性编辑/详情面板的 split pane 结构。
- 原因：这与管理页通用模式一致，也最适合承载多目标选择和单目标属性编辑。
- 备选方案：
  - 纯卡片流式布局：不利于高信息密度管理，不符合 refined 稿方向。
  - 顶部卡片 + 底部详情折叠区：会削弱桌面工具式编辑效率。

### 决策 2：顶部工具区聚焦于搜索、过滤、刷新和新增目标
- 决定：顶部工具区只保留对目标列表直接起作用的动作，如搜索、筛选、刷新、Add Target 或 Test Connection，而不把编辑动作全部塞进全局顶栏。
- 原因：工具栏应服务列表浏览，具体字段编辑和危险操作应下沉到右侧属性面板。
- 备选方案：
  - 把保存/删除/测试全部放到页顶：会让工具栏拥挤，并打散主要操作层级。

### 决策 3：右侧属性面板使用统一 property sheet 表达
- 决定：右侧面板采用与已完成页面一致的属性面板风格，展示目标名称、类型、地址、根路径、认证/连接参数、状态摘要与操作按钮。
- 原因：这样可以复用已有 primitives 与视觉节奏，并和 Game Details / History 的 Inspector 风格保持统一。
- 备选方案：
  - 独立设计一套更像设置表单的大型卡片：会破坏跨页面一致性。

### 决策 4：连接失败状态保留壳层与 pane 结构
- 决定：当目标连接失败时，只替换受影响的列表项状态与右侧主面板内容，保留导航 rail、顶部栏、底部状态栏和 split pane 主体。
- 原因：failure variant 应该是诊断型、可恢复型，而不是整个页面重设计。
- 备选方案：
  - 全页错误页：与 Stitch state variant 指导相悖，也会破坏桌面工具感。

### 决策 5：失败态必须贴近恢复动作
- 决定：失败态中需要明确展示失败原因摘要、最近一次检查时间，以及紧邻的恢复动作（如 Refresh / Retry Test / Edit Target）。
- 原因：Sync Targets 的失败不是信息展示问题，而是需要用户立即处理的管理问题。
- 备选方案：
  - 只用红色状态标签提示失败：不够可操作，用户无法快速恢复。

## Risks / Trade-offs

- [共享原语不足以完全还原 refined Sync Targets] → 优先用现有表格、PropertySheet、Badge、InfoCallout 复用结构，缺口只在必要时做小范围补齐。
- [失败态与正常态共存时信息密度过高] → 保持 split pane 不变，只让右侧主卡内容切换为诊断/恢复视图，避免新增多层嵌套。
- [列表与属性面板宽度比例可能与不同窗口尺寸冲突] → 规范只锁定结构职责与相对层级，不在设计文档中写死像素值，交给实现阶段做范围内微调。
- [未来目标类型扩展可能增加字段差异] → 先定义通用 property sheet 骨架，把具体字段集视作实现层可配置内容，不把本次变更绑死在 WebDAV 单一字段集上。

## Migration Plan

1. 先通过 OpenSpec 补齐 Sync Targets refined 页面和 failure state 的规范。
2. 实现时先重排顶部工具区和 split pane 主体，再收敛右侧属性面板。
3. 之后再补 failure variant，确保失败态建立在同一壳层和 pane 结构上。
4. 最后按 Stitch refined 正常态与 failure state 两个目标屏幕做逐项验收。

## Open Questions

- refined Sync Targets 页面中顶部工具区是否需要同时暴露“测试连接”按钮，还是仅在右侧详情面板中提供。
- failure state 是否要求保留最近一次成功连接时间/失败时间两类时间戳，当前设计文档只先锁定“最近一次检查摘要”。
- 右侧属性面板是否需要显式分组 WebDAV / Local 两类目标的字段差异，还是继续用统一字段骨架 + 条件显示。