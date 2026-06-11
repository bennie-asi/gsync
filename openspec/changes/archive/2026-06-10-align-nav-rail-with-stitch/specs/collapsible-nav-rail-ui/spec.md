## ADDED Requirements

### Requirement: 可展开导航 rail 落地
应用程序 SHALL 为左侧导航 rail 提供收起态与展开态两个明确状态。收起态以图标为主，展开态显示图标与文字，并通过显式控制按钮进行切换。

#### Scenario: 默认显示图标态
- **WHEN** 用户打开应用
- **THEN** rail SHALL 以窄宽度图标态显示，而不是默认展开完整文字列表

#### Scenario: 用户切换到展开态
- **WHEN** 用户点击导航 rail 的展开控制
- **THEN** rail SHALL 增宽并显示文字标签，且当前选中项与设置按钮仍保持清晰可辨

### Requirement: rail 状态切换保持主布局稳定
应用程序 SHALL 在导航 rail 收起/展开切换时，保持顶部标题栏、底部状态栏和主内容区域布局稳定，不使用破坏桌面工具式结构的独立落地浮层替代。

#### Scenario: rail 切换时主布局重排
- **WHEN** rail 从收起态切换到展开态或反向切换
- **THEN** 主内容区域 SHALL 根据 rail 宽度变化进行可预测的布局调整，而不是被覆盖或打断
