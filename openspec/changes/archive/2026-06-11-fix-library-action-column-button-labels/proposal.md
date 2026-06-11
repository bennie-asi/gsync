## Why

用户反馈 Library 主表格的操作列按钮已经连文字都看不见了，这直接影响主页面最核心的“打开 / 更多”操作可用性。这个问题比纯视觉偏差更严重，因为它已经影响可用性和可发现性，因此需要单独快速修正。

## What Changes

- 修复 Library 主表格操作列中按钮文字不可见或被裁切的问题。
- 调整操作列的宽度、单元格布局或按钮样式，使“打开 / 更多”类按钮在当前表格密度下仍能清晰显示。
- 保持 Library 主表格的整体密度与 Stitch refined 稿一致，不因为修按钮文字而让整列退化成过宽或不协调的结构。
- 在必要时微调 `ResizableTableView` 或 Library 页面中的操作列模板，但尽量保持改动聚焦在操作列本身。

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `library-and-details-ui`: 补充 Library 主表格操作列的可读性要求，明确操作按钮文本不能被裁切到不可辨认。

## Impact

- 影响 `src/GSYNC.App/Pages/HomePage.xaml` 与 `src/GSYNC.App/Pages/HomePage.xaml.cs` 中 Library 主表格操作列的布局与列宽配置。
- 可能影响 `src/GSYNC.App/Primitives/ResizableTableView.*` 的单元格承载方式，但优先应避免扩大到整张表格的重构。
- 影响主 specs 中 `library-and-details-ui` 对 Library 主表格操作列可读性和可操作性的要求，需要补充明确约束。
- 后续验证将以 Library refined screen `a1b9236eff004d55b51d032e23304614` 和实际运行效果为主要基准。