## MODIFIED Requirements

### Requirement: 基于 Stitch 的统一应用壳
应用程序 SHALL 采用用户确认的新版 Stitch 桌面视觉系统，在常规窗口中提供带文字的左侧导航、顶部标题栏与主内容区，并在窄窗口中保持导航和操作可访问。各页面 SHALL 共享一致的颜色、字体、边距与状态表达，且应用 SHALL 保留浅色主题。

#### Scenario: 常规窗口导航
- **WHEN** 用户打开应用并切换主要页面
- **THEN** 导航 SHALL 显示图标与文字，且当前页面和设置入口清晰可辨

#### Scenario: 窄窗口布局
- **WHEN** 窗口宽度降低
- **THEN** 导航和内容 SHALL 可预测地重排或收起，而不遮挡主要操作

### Requirement: Stitch 设计稿强约束
应用程序 SHALL 以用户确认的 GSYNC Stitch 页面为主要视觉参考，同时保留已实现的同步安全和真实状态语义。

#### Scenario: 设计与真实状态对齐
- **WHEN** 用户查看设计稿对应的页面
- **THEN** 页面 SHALL 体现设计稿的结构和视觉层级，并使用真实数据而非示例状态
