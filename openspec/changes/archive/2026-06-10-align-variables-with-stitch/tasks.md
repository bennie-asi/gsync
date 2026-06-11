## 1. 对齐 Variables 正常态结构

- [x] 1.1 对照 Stitch refined Variables 设计稿确认顶部工具区、左侧变量列表区与右侧详情/测试面板的最终区域划分
- [x] 1.2 重排 Variables 页面顶部工具区，使搜索、作用域/来源筛选、刷新、添加变量等列表级操作符合 refined 稿层级
- [x] 1.3 重构 Variables 主体为左侧变量列表/表格 + 右侧详情与模板测试区的 split pane 结构

## 2. 收敛变量列表与详情/测试区

- [x] 2.1 调整左侧变量列表/表格的列、来源/作用域/状态展示和信息密度，使其符合管理类页面的桌面工具风格
- [x] 2.2 调整右侧详情面板，统一展示变量属性、来源、优先级、路径模板影响与测试结果摘要
- [x] 2.3 复用现有 PropertySheet、KeyValueList、Badge、InfoCallout 与表格原语，减少 Variables 页面上的自定义布局分叉

## 3. 落地 Variables parse error state

- [x] 3.1 根据 Stitch parse error variant 设计模板解析失败状态的受影响区域切换方式，保持壳层和 split pane 主体不变
- [x] 3.2 在错误状态中展示失败变量/模板、错误原因、最近一次解析结果和修复动作（如刷新解析、编辑变量、复制模板、重试测试）
- [x] 3.3 校验错误状态与正常状态之间的切换不会破坏导航、顶部栏、底部状态栏和页面密度一致性

## 4. 完成 Stitch 对稿与验证

- [x] 4.1 逐项核对 Variables 正常态与 refined 屏幕 `3023eb712c434b62a287bad3b44b6d89` 的结构和层级
- [x] 4.2 逐项核对解析错误状态与 parse error state `b4ce8536544543dbabf87e867774b6fb` 的结构和恢复动作
- [x] 4.3 运行构建验证，确认 VariablesPage 相关改动在 WinUI 项目中可编译通过