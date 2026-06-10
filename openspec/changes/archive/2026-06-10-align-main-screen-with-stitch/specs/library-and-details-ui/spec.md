## MODIFIED Requirements

### Requirement: Library 界面落地
应用程序 SHALL 按 Stitch 的 refined / normalized Library 设计稿实现多游戏总览界面。Library 页面 SHALL 在主内容区顶部提供占据主要横向权重的搜索输入框，并在同一行右侧提供排序与刷新按钮；主内容中段 SHALL 显示游戏表格区域；右侧上方 SHALL 提供“添加游戏”和“立即同步”主操作区，右侧下方 SHALL 提供总览视图。页面各分区的信息层级、相对位置与桌面工具式密度 SHALL 与 Stitch refined Library 设计稿保持一致。

#### Scenario: Library 主界面显示
- **WHEN** 用户打开 Library 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的游戏列表、状态标记、筛选/搜索工具栏与详情预留区域

#### Scenario: Library 顶部工具区对齐设计稿
- **WHEN** 用户查看 Library 主功能区顶部
- **THEN** 应用程序 SHALL 在左侧显示较宽的搜索输入框，在右侧显示排序与刷新按钮，而不是将这些控件重新排布成与设计稿不一致的结构

#### Scenario: Library 右侧操作与总览区对齐设计稿
- **WHEN** 用户查看 Library 页面右侧区域
- **THEN** 应用程序 SHALL 在右上方显示“添加游戏”和“立即同步”操作，在其下方显示总览视图，而不是将这些内容并入表格工具栏或改为其他信息架构

## ADDED Requirements

### Requirement: Library 底部运行状态展示
应用程序 SHALL 在 Library 页面底部固定展示运行状态信息。状态栏左侧 SHALL 显示 WebDAV 在线状态与 Local 就绪状态，右侧 SHALL 显示当前 WebDAV 名称与同步状态，并与主界面其余区域同时可见。

#### Scenario: Library 底部展示连接与同步状态
- **WHEN** 用户停留在 Library 页面
- **THEN** 页面底部 SHALL 同时显示 WebDAV: Online / Offline、Local: Ready 等运行状态，以及当前 WebDAV 名称与同步摘要
