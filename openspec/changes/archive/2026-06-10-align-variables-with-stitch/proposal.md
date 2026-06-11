## Why

当前方案 A 已按 Stitch refined 设计稿推进了 Library、Game Details、History 与 Sync Targets 页面，Variables 页成为管理类页面里下一个最关键的收口点。现在推进这一页，可以把路径变量管理、解析结果、模板测试和 parse error state 一并收敛到统一的桌面工具式管理页结构中，避免 Variables 页面继续成为信息层级和错误态表达的例外。

## What Changes

- 将 Variables 页面按 Stitch refined Variables 设计稿重新对齐，保持与现有壳层一致的高密度桌面工具式 split pane 结构。
- 调整 Variables 页面的顶部工具区、左侧变量列表/表格区、右侧变量详情与测试区，使其符合 Stitch 页面层级与交互分区。
- 明确 Variables 页面中系统变量、来源变量、实例变量与用户自定义变量的展示密度和筛选方式。
- 将 Variables 的模板解析错误状态按 Stitch 提供的 parse error variant 落地，确保错误态不破坏壳层和管理页主体结构。
- 收敛 Variables 页面与已完成 Library / History / Sync Targets 页面在边距、分栏、属性编辑与错误提示表达上的一致性。

## Capabilities

### New Capabilities
- `variables-parse-error-state-ui`: 定义 Variables 页面模板解析错误场景下的专用状态变体与恢复动作展示要求。

### Modified Capabilities
- `management-screens-ui`: 调整 Variables 页面在管理类页面中的结构、工具栏、split pane、详情/测试区和错误状态要求。

## Impact

- 影响 `src/GSYNC.App/Pages/VariablesPage.*` 与对应 ViewModel 的静态布局和展示模型。
- 可能影响共享原语在管理页中的使用方式，尤其是表格、属性编辑区、路径模板测试区与错误提示组件。
- 影响主 specs 中管理页面与状态变体的定义，需要补充 Variables refined 页面与 parse error state 的明确要求。
- 后续验证将以 Stitch 项目 `13407775155513183369` 中 refined Variables 屏幕 `3023eb712c434b62a287bad3b44b6d89` 和 parse error state `b4ce8536544543dbabf87e867774b6fb` 为主要基准。