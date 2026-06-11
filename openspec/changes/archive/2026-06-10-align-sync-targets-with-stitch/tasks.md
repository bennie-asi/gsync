## 1. 对齐 Sync Targets 正常态结构

- [x] 1.1 对照 Stitch refined Sync Targets 设计稿确认顶部工具区、左侧目标列表区与右侧属性面板的最终区域划分
- [x] 1.2 重排 Sync Targets 页面顶部工具区，使搜索/筛选/刷新/新增目标等列表级操作符合 refined 稿层级
- [x] 1.3 重构 Sync Targets 主体为左侧目标列表/表格 + 右侧属性编辑区的 split pane 结构

## 2. 收敛目标列表与属性编辑区

- [x] 2.1 调整左侧目标列表/表格的列、状态展示和信息密度，使其符合管理类页面的桌面工具风格
- [x] 2.2 调整右侧属性面板，统一展示目标名称、类型、地址/根路径、认证参数、状态摘要和操作按钮
- [x] 2.3 复用现有 PropertySheet、Badge、表格与错误提示原语，减少 Sync Targets 页面上的自定义布局分叉

## 3. 落地 Sync Targets failure state

- [x] 3.1 根据 Stitch failure variant 设计连接失败状态的受影响区域切换方式，保持壳层和 split pane 主体不变
- [x] 3.2 在失败状态中展示失败摘要、受影响目标和恢复动作（如刷新、重试测试、编辑配置）
- [x] 3.3 校验失败状态与正常状态之间的切换不会破坏导航、顶部栏、底部状态栏和页面密度一致性

## 4. 完成 Stitch 对稿与验证

- [x] 4.1 逐项核对 Sync Targets 正常态与 refined 屏幕 `ee786c958cf24700b1ddb593a37697d2` 的结构和层级
- [x] 4.2 逐项核对连接失败状态与 failure state `99e89eaae486405b962d54f3e258a17f` 的结构和恢复动作
- [x] 4.3 运行构建验证，确认 SyncTargetsPage 相关改动在 WinUI 项目中可编译通过