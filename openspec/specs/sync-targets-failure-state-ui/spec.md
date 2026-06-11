## Purpose

定义 GSYNC 第二阶段 UI 中 Sync Targets 连接失败状态变体的落地要求。

## Requirements

### Requirement: Sync Targets 失败状态变体落地
应用程序 SHALL 为 Sync Targets 页面提供专用的连接失败状态变体，用于在目标不可达、认证失败或连接测试失败时展示诊断信息与恢复入口。

#### Scenario: 目标连接失败时显示失败态
- **WHEN** 用户查看一个连接测试失败的同步目标
- **THEN** 页面 SHALL 显示符合 Stitch failure state 的失败状态内容，包括失败摘要、受影响目标和恢复动作

### Requirement: 失败状态不破坏页面结构一致性
应用程序 SHALL 在 Sync Targets failure state 下保持与正常管理页面相同的导航壳、顶部工具区、主 split pane 和信息密度，只替换受影响的主内容区域。

#### Scenario: 失败态保留壳层与 pane 结构
- **WHEN** Sync Targets 页面从正常态切换到失败态
- **THEN** 导航、标题栏、底部状态栏和主 split pane SHALL 继续保留，避免把失败态重新设计成独立落地页
