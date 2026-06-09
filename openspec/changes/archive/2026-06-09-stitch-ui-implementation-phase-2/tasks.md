## 1. App 壳与共享 UI primitives

- [x] 1.1 将当前 `MainWindow`/壳层调整为与 Stitch normalized shell 一致的导航 rail、标题栏、内容区与状态栏结构
- [x] 1.2 在 `GSYNC.App` 中建立 Views、ViewModels、共享 UI primitives 的目录与命名组织
- [x] 1.3 实现共享 primitives：`AppNavRail`、`AppTitleBar`、`StatusBar`、`SectionHeader`
- [x] 1.4 实现共享 data presentation primitives：`DenseDataGrid`、`PropertySheet`、`InspectorPanel`、`ToolbarFilterRow`、`Badge`、`InfoCallout`
- [x] 1.5 统一全局 spacing、边框、标题栏高度、rail 宽度和状态色语义，使之对齐 Stitch refined / normalized 屏幕

## 2. Library 与 Game Details

- [x] 2.1 按 Stitch refined Library 设计稿实现 Library 主页面结构与布局
- [x] 2.2 为 Library 页面接入真实数据入口（游戏列表、状态、筛选/搜索占位逻辑）
- [x] 2.3 按 Stitch refined Game Details 设计稿实现 Game Details 页面结构与 master-detail 布局
- [x] 2.4 为 Game Details 页面接入内容项、路径、历史预览等 ViewModel 状态
- [x] 2.5 按 Stitch 状态变体实现 Library first-run / empty state 与 sync in-progress state

## 3. 管理页面与状态变体

- [x] 3.1 按 Stitch refined 设计稿实现 Sync Targets 页面与 split pane / property editor 结构
- [x] 3.2 按 Stitch refined 设计稿实现 Variables 页面与 path template tester 区域
- [x] 3.3 按 Stitch refined 设计稿实现 History 页面与高密度历史/快照列表结构
- [x] 3.4 按 Stitch refined 设计稿实现 Settings 页面与分组式设置布局
- [x] 3.5 按 Stitch 状态变体实现 Sync Targets failure、Variables parse error、History empty state

## 4. 壳层路由、状态与后续扩展点

- [x] 4.1 将现有 NavigationView 路由升级为可维护的页面导航与选中状态管理
- [x] 4.2 将页面状态逻辑下沉到 ViewModel / 共享 UI primitives，减少 code-behind 特例逻辑
- [x] 4.3 为 Add Game 向导、Conflict Resolution、Snapshot Restore 等后续流程预留导航入口与壳层挂载点
- [x] 4.4 检查所有已实现页面与 Stitch 设计稿的一致性，修正任何擅自偏离布局/信息层级的实现
- [x] 4.5 为新增 UI 状态逻辑补充可在非 UI 测试中验证的测试覆盖，并确保 `dotnet build` / `dotnet test` 继续通过
