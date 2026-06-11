## 1. 细调 History 页面

- [x] 1.1 对照 Stitch refined History 与 empty state，核对筛选区、主表格、右侧详情/快照区和空状态恢复动作的最终细调目标
- [x] 1.2 调整 `HistoryPage.xaml`，收紧筛选区、右侧详情/快照区和 empty state 的结构与文案表达，减少页面级局部模板和硬编码
- [x] 1.3 视需要补充 `HistoryPageViewModel` 的文案或状态适配字段，并保持 `HistoryPage.xaml.cs` 中现有表格列初始化逻辑不变
- [x] 1.4 仅在页面层无法合理收敛时，最小范围调整 `SnapshotFeed` 或相关 shared primitives 的承载方式

## 2. 细调 Settings 页面

- [x] 2.1 对照 Stitch refined Settings 屏幕，核对左侧分组导航、中心设置区和右侧辅助预览/检查区的最终细调目标
- [x] 2.2 调整 `SettingsPage.xaml`，收紧分组导航语义、中心设置区层级、按钮主次和右侧辅助区表达，减少占位式或数据页式写法
- [x] 2.3 视需要补充 `SettingsPageViewModel` 的标题、状态或 badge 适配字段，并保持 `SettingsPage.xaml.cs` 中现有初始化与本地化接线逻辑稳定
- [x] 2.4 仅在页面层无法完成收敛时，最小范围调整 `InspectorPanel`、`PropertySheet` 或相关 shared primitives 的复用方式

## 3. 验证 refined 对齐与 shared primitives 复用

- [x] 3.1 复查 `InfoCallout`、`PropertySheet`、`InspectorPanel`、`SnapshotFeed` 等 shared primitives 在 History 与 Settings 中的复用是否减少了页面级分叉
- [x] 3.2 逐项核对 History、History empty state 和 Settings 的结构、状态表达和按钮层级是否贴近对应 Stitch refined screens
- [x] 3.3 运行 WinUI 构建验证，确认这两页细调相关改动可编译通过且没有破坏既有页面行为