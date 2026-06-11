## Purpose

定义 GSYNC 第二阶段 UI 中 Library 与 Game Details 两个主页面的落地要求。

## Requirements

### Requirement: Library 界面落地
应用程序 SHALL 按 Stitch 的 refined / normalized Library 设计稿实现多游戏总览界面。Library 页面 SHALL 在主内容区顶部提供占据主要横向权重的搜索输入框，并在同一行右侧提供排序与刷新按钮；主内容中段 SHALL 显示游戏表格区域；右侧上方 SHALL 提供“添加游戏”和“立即同步”主操作区，右侧下方 SHALL 提供总览视图。页面各分区的信息层级、相对位置与桌面工具式密度 SHALL 与 Stitch refined Library 设计稿保持一致。Library 主表格中的操作列 SHALL 保持按钮文本可读、可点击，不得因列宽或模板布局不足而把“打开 / 更多”等文本按钮裁切到不可辨认。

#### Scenario: Library 主界面显示
- **WHEN** 用户打开 Library 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的游戏列表、状态标记、筛选/搜索工具栏与详情预留区域

#### Scenario: Library 顶部工具区对齐设计稿
- **WHEN** 用户查看 Library 主功能区顶部
- **THEN** 应用程序 SHALL 在左侧显示较宽的搜索输入框，在右侧显示排序与刷新按钮，而不是将这些控件重新排布成与设计稿不一致的结构

#### Scenario: Library 右侧操作与总览区对齐设计稿
- **WHEN** 用户查看 Library 页面右侧区域
- **THEN** 应用程序 SHALL 在右上方显示“添加游戏”和“立即同步”操作，在其下方显示总览视图，而不是将这些内容并入表格工具栏或改为其他信息架构

#### Scenario: Library 操作列按钮文本可辨认
- **WHEN** 用户查看 Library 主表格中的操作列
- **THEN** “打开 / 更多”等按钮文本 SHALL 清晰可见，而不是因为列宽或按钮布局过窄而被裁切到无法辨认

### Requirement: Game Details 界面落地
应用程序 SHALL 按 Stitch 的 refined Game Details 设计稿实现单游戏详情页面，支持内容项检查、路径查看、同步历史预览与上下文操作。Game Details 页面 SHALL 保持桌面工具式主次分区：顶部上下文与主要操作区、中部内容项表格区、底部历史/快照区，以及右侧属性/检查面板，并与 Library 主壳层保持一致的密度与信息层级。Game Details 页面中的底部历史/快照区与右侧检查区 SHALL 优先复用共享原语（如 ActivityFeed、SnapshotFeed、KeyValueList 或等价共享模式），而不是继续保留页面专属的重复模板结构。

#### Scenario: Game Details 页面显示
- **WHEN** 用户从 Library 进入某个游戏的详情界面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的内容项列表、属性区与相关操作区

#### Scenario: Game Details 分区符合 refined 稿
- **WHEN** 用户查看 Game Details 页面
- **THEN** 页面 SHALL 保持顶部上下文卡、内容项表格、历史/快照区与右侧详情面板的稳定分区，而不是将这些内容重新拼接为松散卡片或单列布局

#### Scenario: Game Details 优先复用共享原语
- **WHEN** 页面需要展示历史活动、快照信息或属性检查内容
- **THEN** 实现 SHALL 优先复用共享原语，而不是在页面中继续复制新的 Border、StackPanel 和局部模板组合

### Requirement: Library 状态变体落地
应用程序 SHALL 按 Stitch 提供的状态变体实现 Library 的 first-run / empty 状态以及 sync in-progress 状态，并保持与正常页面相同的应用壳与布局结构。

#### Scenario: Library 空状态显示
- **WHEN** 当前没有任何游戏实例
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的 first-run / empty 状态，而不改变应用壳结构

### Requirement: Library 底部运行状态展示
应用程序 SHALL 在 Library 页面底部固定展示运行状态信息。状态栏左侧 SHALL 显示 WebDAV 在线状态与 Local 就绪状态，右侧 SHALL 显示当前 WebDAV 名称与同步状态，并与主界面其余区域同时可见。

#### Scenario: Library 底部展示连接与同步状态
- **WHEN** 用户停留在 Library 页面
- **THEN** 页面底部 SHALL 同时显示 WebDAV: Online / Offline、Local: Ready 等运行状态，以及当前 WebDAV 名称与同步摘要

### Requirement: Add Game 主流程与空结果状态保持一致体验
应用程序 SHALL 在 Add Game 主流程中保持与 Stitch refined Wizard 屏幕一致的步骤感与操作节奏；当出现 no-results 状态时，界面 SHALL 继续保留相同的向导上下文和恢复路径，而不是跳转到脱离流程的独立空页面。Add Game 主流程中的重复卡片结构和步骤轨表达 SHALL 尽量收敛到共享模板或共享原语，避免同一页面内部重复定义多份等价布局。

#### Scenario: Add Game 无结果仍保留流程上下文
- **WHEN** 用户在 Add Game 主流程中遇到没有结果的状态
- **THEN** 应用程序 SHALL 保留相同的步骤上下文、返回路径和下一步恢复动作，而不是丢失当前向导骨架

#### Scenario: Add Game 重复卡片模板被收敛
- **WHEN** 多个步骤展示相同语义的选项卡片或步骤轨内容
- **THEN** 实现 SHALL 优先通过共享模板或共享原语复用这些结构，而不是在同一页面内重复定义多份相同布局
