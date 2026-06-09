## Purpose

定义 GSYNC 第二阶段 UI 中 Library 与 Game Details 两个主页面的落地要求。

## Requirements

### Requirement: Library 界面落地
应用程序 SHALL 按 Stitch 的 refined / normalized Library 设计稿实现多游戏总览界面，提供紧凑列表/表格、状态可视化、搜索过滤与快捷同步操作区域。

#### Scenario: Library 主界面显示
- **WHEN** 用户打开 Library 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的游戏列表、状态标记、筛选/搜索工具栏与详情预留区域

### Requirement: Game Details 界面落地
应用程序 SHALL 按 Stitch 的 refined Game Details 设计稿实现单游戏详情页面，支持内容项检查、路径查看、同步历史预览与上下文操作。

#### Scenario: Game Details 页面显示
- **WHEN** 用户从 Library 进入某个游戏的详情界面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的内容项列表、属性区与相关操作区

### Requirement: Library 状态变体落地
应用程序 SHALL 按 Stitch 提供的状态变体实现 Library 的 first-run / empty 状态以及 sync in-progress 状态，并保持与正常页面相同的应用壳与布局结构。

#### Scenario: Library 空状态显示
- **WHEN** 当前没有任何游戏实例
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的 first-run / empty 状态，而不改变应用壳结构
