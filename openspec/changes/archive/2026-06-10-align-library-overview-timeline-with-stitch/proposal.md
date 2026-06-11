## Why

用户明确指出 Library 页面右侧总览区域的下半部分应该采用**时间线方式**，而当前实现仍然把这部分做成普通的总览指标/属性列表堆叠，没有严格参照 Stitch refined Library 设计稿。这个问题非常具体，也直接影响主页面右侧信息层级是否像设计稿，因此需要单独把 Library 右侧“Overview 下方”的表现纠正回时间线式活动区。

## What Changes

- 调整 Library 页面右侧区域，使主操作区下方保持 Overview 指标区，而其下方改为时间线式活动展示。
- 让 Library 页面右侧总览下半部分直接参照 Stitch refined Library 稿，不再继续使用普通列表/静态摘要块代替活动时间线。
- 优先复用现有 `ActivityFeed` 和 `LibraryPageViewModel.Activity` 数据，避免在 Library 页面里重新发明一套时间线结构。
- 收敛 Library 右侧区域的层级与间距，使“主操作区 → Overview → 时间线活动区”的信息顺序更清晰。

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `library-and-details-ui`: 细化 Library 页面右侧总览区域的要求，明确 Overview 下方应为时间线式活动区，而不是普通属性列表或静态摘要块。

## Impact

- 影响 `src/GSYNC.App/Pages/HomePage.xaml` 与 `src/GSYNC.App/ViewModels/LibraryPageViewModel.cs` 中 Library 右侧区域的布局与文案。
- 可能影响 `src/GSYNC.App/Primitives/ActivityFeed.*` 的承载方式，但优先应保持为直接复用而非重写。
- 影响主 specs 中 `library-and-details-ui` 对 Library 右侧区域的 refined 要求，需要补充“总览下方为时间线活动区”的明确约束。
- 后续验证将以 Stitch refined Library `a1b9236eff004d55b51d032e23304614` 为主要基准。