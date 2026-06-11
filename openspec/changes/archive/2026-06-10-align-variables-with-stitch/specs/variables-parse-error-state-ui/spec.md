## ADDED Requirements

### Requirement: Variables 解析错误状态变体落地
应用程序 SHALL 为 Variables 页面提供专用的模板解析错误状态变体，用于在路径模板无效、变量缺失或解析失败时展示诊断信息与修复入口。

#### Scenario: 模板解析失败时显示错误态
- **WHEN** 用户查看一个解析失败的变量或模板测试结果
- **THEN** 页面 SHALL 显示符合 Stitch parse error state 的错误内容，包括失败摘要、受影响变量/模板和恢复动作

### Requirement: 解析错误状态不破坏页面结构一致性
应用程序 SHALL 在 Variables parse error state 下保持与正常管理页面相同的导航壳、顶部工具区、主 split pane 和信息密度，只替换受影响的主内容区域。

#### Scenario: 解析错误态保留壳层与 pane 结构
- **WHEN** Variables 页面从正常态切换到解析错误态
- **THEN** 导航、标题栏、底部状态栏和主 split pane SHALL 继续保留，避免把错误态重新设计成独立落地页
