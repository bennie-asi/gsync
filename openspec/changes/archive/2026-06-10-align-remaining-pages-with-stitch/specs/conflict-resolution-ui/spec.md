## ADDED Requirements

### Requirement: Conflict Resolution 按安全优先对比界面落地
应用程序 SHALL 按 Stitch refined Conflict Resolution 设计稿实现双栏版本对比界面，展示本地版本、远端版本、同步基线摘要与受影响文件列表，并保持桌面工具式的高信息密度布局。

#### Scenario: Conflict Resolution 对比界面显示
- **WHEN** 用户打开冲突解决界面
- **THEN** 应用程序 SHALL 同时显示本地版本、远端版本、基线摘要和冲突文件列表，而不是退化为单栏信息页或缺少对比上下文的按钮页

### Requirement: Conflict Resolution 提供显式解决动作
应用程序 SHALL 在 Conflict Resolution 界面中提供清晰分层的解决动作，例如使用本地版本、使用远端版本、保留双方、备份后恢复或取消，并将风险提示保持在靠近对比区域或动作区的位置。

#### Scenario: Conflict Resolution 动作可判读
- **WHEN** 用户准备处理冲突
- **THEN** 用户 SHALL 能从界面上直接辨认主要解决动作、辅助动作和风险提示，而不是只能依赖隐藏菜单或模糊按钮文案
