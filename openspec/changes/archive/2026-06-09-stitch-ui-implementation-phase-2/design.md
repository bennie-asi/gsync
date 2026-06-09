## Context

当前仓库已经具备可编译、可测试的 WinUI 3 应用壳，以及 Core / Data / Manifest / Provider / Storage / SyncEngine 的基础实现，但 App 层仍主要停留在占位页面与基础导航阶段。与此同时，Stitch 侧已经完成 refined / normalized 的高保真桌面工具设计稿，并覆盖了 Library、Game Details、Sync Targets、Variables、History、Settings，以及关键 empty / error / no-results 状态。

这意味着下一阶段的工作重点已经不再是“搭一个能跑的壳”，而是把现有壳层推进为真正遵循 Stitch 设计稿的产品界面，并让页面结构、共享原语、状态变体与后续业务接线具有可持续扩展性。

关键约束：
- UI 实现必须强制遵循 Stitch 设计稿，不允许自由重设计
- 优先使用 refined / normalized 屏幕作为主参考
- 保持现有 WinUI 3 + CommunityToolkit.Mvvm 技术路线
- 尽量把 UI 状态逻辑下沉到 ViewModel 或共享 UI primitives，避免页面代码后置膨胀

## Goals / Non-Goals

**Goals:**
- 将当前 App 壳推进为与 Stitch 一致的统一桌面工具壳
- 建立共享 UI primitives，减少页面重复实现
- 落地主流程主界面：Library、Game Details、Sync Targets、Variables、History、Settings
- 实现 Stitch 已提供的关键 empty / error 状态变体
- 为后续 Add Game 向导、Conflict Resolution、Snapshot restore 等流程预留稳定接入点
- 保持 UI 实现可测试、可扩展，并与现有 Core / Data / SyncEngine 对接

**Non-Goals:**
- 本次不实现 Add Game 六步向导的完整行为逻辑
- 本次不完整实现 Conflict Resolution 交互流
- 本次不处理动画、美术微调或与设计稿无关的“风格发挥”
- 本次不重新选择 UI 技术栈，也不切换离开 WinUI 3

## Decisions

### 决策 1：以 Stitch refined / normalized 屏幕作为唯一主要参考
**选择：** 开发时优先对齐 Stitch refined / normalized 设计稿及其状态变体，禁止在页面层面自行改变导航关系、信息层级和布局结构。

**原因：**
- 用户已明确要求 UI 实现必须强制遵循 Stitch 设计稿
- 设计稿已完成统一化处理（rail 宽度、标题栏高度、内容边距、split-pane 间距）
- 如果允许实现阶段随意发挥，会导致壳层与页面快速偏离既定桌面工具方向

**替代方案：**
- 以当前占位页为基础边做边调整 → 拒绝，容易在没有统一约束时偏离设计稿
- 只参考设计稿视觉风格，不强约束布局 → 拒绝，不符合用户要求

### 决策 2：先做共享 UI primitives，再做主页面
**选择：** Phase 1 先建立 `AppNavRail`、`AppTitleBar`、`StatusBar`、`DenseDataGrid`、`PropertySheet`、`InspectorPanel`、`ToolbarFilterRow`、`Badge`、`InfoCallout` 等共享原语，再基于这些原语落地主页面。

**原因：**
- `implementation-notes.md` 已明确建议“shell and shared components first”
- Library、History、Sync Targets、Variables、Game Details 都共享高密度桌面工具布局模式
- 先抽共用原语可以避免每个页面重复画壳、重复处理 spacing / borders / badges

**替代方案：**
- 逐页直接写页面，再事后抽公共组件 → 可行但返工大，且易产生多个不一致的页面壳实现

### 决策 3：UI 页面按 Views + ViewModels + primitives 组织
**选择：** 在 `GSYNC.App` 中明确分为：
- `Views/`：页面与对话框
- `ViewModels/`：页面状态逻辑
- `Controls/` 或 `Primitives/`：共享 UI 原语
- `Navigation/` / `Shell/`：壳层与路由组织

**原因：**
- 主 specs 中 `solution-structure` 已要求 App 层 UI 组织不能堆在单个窗口文件
- 这能减少 MainWindow 成为“巨型容器类”
- 有利于以后把状态逻辑放到 ViewModel，在非 UI 测试中验证

**替代方案：**
- 保持所有页面和状态都堆在 `MainWindow.xaml` / `.cs` 中 → 拒绝，维护成本高且不符合结构约束

### 决策 4：Library 作为第一批高保真页面，Game Details 同步跟进
**选择：** 第一批优先落地：
1. App shell + shared primitives
2. Library
3. Game Details
4. Sync Targets / Variables / History / Settings
5. 状态变体

**原因：**
- `implementation-handoff.md` 已把这组顺序列为强烈建议
- Library 是全局入口，最能验证 nav rail、title bar、status bar、dense grid 是否成型
- Game Details 是典型 master-detail 页面，能验证 property sheet 与 inspector panel 方案

**替代方案：**
- 先做 Settings / History 等简单页面 → 可以更快出页面，但无法验证主结构设计是否正确

### 决策 5：状态变体使用同壳不同焦点区，而不是单独“营销式”页面
**选择：** 对 Library 空状态、Sync Targets 失败、Variables 模板错误、History 空状态等，统一保留主壳结构，只替换焦点区域内容。

**原因：**
- `implementation-notes.md` 明确要求 empty/error state 仍保留相同壳层
- 能避免产品在 first-run 或错误状态下退化成“另一套页面”
- 更符合桌面工具的一致性预期

**替代方案：**
- 每个空状态单独做独立页面 → 拒绝，会破坏壳层一致性

### 决策 6：先接入现有业务服务，再逐步细化交互行为
**选择：** UI 层先接入现有的 ManifestService、Repositories、SyncEngine 等服务，为主页面提供真实数据入口；更深的向导/冲突交互等复杂流程延后。

**原因：**
- 当前基础服务已经存在，可支撑 Library / History / Variables / Targets 的真实数据绑定
- 可以让高保真 UI 较早进入“真实数据驱动”状态
- 避免一次性同时推进 shell、所有交互流与复杂业务逻辑，导致变更失控

**替代方案：**
- 长期使用纯占位数据直到所有页面都完成 → 拒绝，这会让 UI 代码和业务服务长期脱节

## Risks / Trade-offs

- **[UI 与设计稿偏差]** → 每个页面实现前先以 Stitch refined / normalized Screen ID 为主参考，必要时在变更文档中明确对应关系。
- **[共享 primitive 抽象过早]** → 先只抽 Stitch 多页重复出现的稳定结构（rail、title、status、grid、property sheet、toolbar row、badge），避免抽象未稳定的交互细节。
- **[页面状态逻辑散落到 code-behind]** → 约束页面只保留视图装配和导航 glue，筛选、选中、状态变体、工具栏状态优先放入 ViewModel。
- **[与现有服务对接时 UI 实现过深]** → 先暴露只读/轻交互状态，复杂写操作在后续 change 中分层推进。
- **[Stitch 页面覆盖范围广，阶段过大]** → 明确分阶段顺序：壳层 → 主页面 → 状态变体 → 后续向导/冲突流。

## Migration Plan

1. 在 `GSYNC.App` 中建立共享壳层与 primitives 的目录结构
2. 将当前 `MainWindow` 的基础 NavigationView 壳替换为 Stitch 对齐版本
3. 落地 Library 与 Game Details 的高保真页面结构
4. 落地 Sync Targets、Variables、History、Settings
5. 为关键页面加入对应 Stitch 状态变体
6. 保持每一阶段都能 `build + test` 通过，再进入下一批页面

## Open Questions

- 当前 `GSYNC.App` 中共享 UI 原语是放在 `Controls/` 还是 `Primitives/` 目录更合适？
- Game Details 页面是否在本 change 中同时引入真实导航入口，还是先只实现页面与 ViewModel 结构？
- Add Game 向导和 Conflict Resolution 是否在本次 change 中只预留导航壳与占位入口，等待后续 change 再做完整交互？
