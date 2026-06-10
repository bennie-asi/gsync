## MODIFIED Requirements

### Requirement: 基于 Stitch 的统一应用壳
应用程序 SHALL 提供与 Stitch refined / normalized 屏幕一致的统一应用壳，包括紧凑左侧导航 rail、顶部标题/工具栏区域、主内容区与可选底部状态栏，并在各页面之间保持一致的桌面工具式布局。在 Library 页面中，左侧导航区顶部 SHALL 显示用户头像、应用名与版本信息；页面底部 SHALL 提供固定状态栏，且该状态栏不因主内容滚动而离开视口。

#### Scenario: 主界面壳一致
- **WHEN** 用户在 Library、Sync Targets、Variables、History、Settings 之间切换
- **THEN** 应用程序 SHALL 保持相同的导航 rail、标题栏高度、内容边距与主布局结构

#### Scenario: Library 页面显示菜单头部
- **WHEN** 用户打开 Library 页面
- **THEN** 左侧导航区顶部 SHALL 显示头像、应用名与版本信息，而不是只显示普通导航项列表

#### Scenario: Library 页面显示固定底部状态栏
- **WHEN** 用户在 Library 页面浏览较长内容并发生主区域滚动
- **THEN** 页面底部状态栏 SHALL 继续固定显示连接状态与同步摘要，而不是随内容一起滚动离开可见区域
