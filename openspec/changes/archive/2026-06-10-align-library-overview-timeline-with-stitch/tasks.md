## 1. 对齐 Library 右侧时间线区域的设计目标

- [x] 1.1 对照 Stitch refined Library 屏幕，确认右侧主操作区、Overview 指标区和其下方时间线区域的最终层级
- [x] 1.2 核对当前 `HomePage.xaml` 中右侧 `OverviewSheet` 与设计稿差异，明确需要保留的指标区和需要替换成时间线的下半部分

## 2. 落地 Library 总览下方的时间线方式

- [x] 2.1 调整 `HomePage.xaml`，让右侧区域从“单一 OverviewSheet + 指标/属性堆叠”收敛为“Overview 指标区 + 下方时间线活动区”的结构
- [x] 2.2 优先复用 `ActivityFeed` 来承载时间线式活动展示，而不是在 Library 页面中重新手写一套时间线模板
- [x] 2.3 视需要补充 `LibraryPageViewModel` 中的标题、副标题或时间线说明文案，但保持现有 `Activity` 数据来源与页面行为逻辑不变

## 3. 验证设计稿对齐与构建

- [x] 3.1 逐项核对 Library 右侧总览区下方是否已经按设计稿改为时间线方式，而不是普通列表表达
- [x] 3.2 校验 Overview 指标区、活动时间线区和顶部主操作区之间的层级与间距符合 refined Library 稿
- [x] 3.3 运行 WinUI 构建验证，确认 Library 页面相关改动可编译通过且没有破坏现有主页面行为
