## 1. 确认 Library 操作列问题来源

- [x] 1.1 核对 `HomePage.xaml` 中 `LibraryActionCellTemplate` 的双按钮布局与当前按钮样式、间距是否导致文本被压缩
- [x] 1.2 核对 `HomePage.xaml.cs` 中 action 列初始宽度/最小宽度配置，确认它与按钮实际所需宽度的差异

## 2. 修复操作列按钮文字不可见问题

- [x] 2.1 调整 `HomePage.xaml` 中的 `LibraryActionCellTemplate`，让“打开 / 更多”按钮在当前表格密度下保持可读
- [x] 2.2 调整 `HomePage.xaml.cs` 中 action 列的初始宽度或最小宽度，使按钮文本不再被裁切
- [x] 2.3 如页面级修复仍不足，再最小范围评估 `ResizableTableView` 的单元格承载方式，但避免扩大到整张表格重构

## 3. 验证显示与构建

- [x] 3.1 逐项核对 Library 主表格操作列中按钮文字是否已经清晰可见
- [x] 3.2 校验操作列修复后不会明显破坏表格整体密度与相邻列比例
- [x] 3.3 运行 WinUI 构建验证，确认相关改动可编译通过且没有破坏 Library 页面行为
