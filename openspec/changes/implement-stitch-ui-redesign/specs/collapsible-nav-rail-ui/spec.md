## MODIFIED Requirements

### Requirement: 可展开导航 rail 落地
应用程序 SHALL 在常规窗口中默认显示带文字的导航，并保留用户可触发的紧凑图标导航状态。

#### Scenario: 默认显示带文字导航
- **WHEN** 用户以常规窗口打开应用
- **THEN** 导航 SHALL 显示图标和文字标签

#### Scenario: 用户切换紧凑导航
- **WHEN** 用户触发导航收起控制
- **THEN** 导航 SHALL 收窄并保持各入口可辨认且可操作
