## MODIFIED Requirements

### Requirement: 管理页面按 Stitch 落地
应用程序 SHALL 按 Stitch refined / normalized 设计稿实现 Sync Targets、Variables、History 与 Settings 页面，并保持高信息密度的桌面工具风格。各管理页面在导航进入或首次初始化过程中发生异常时，应用程序 SHALL 将失败限制在当前页面内容区，不得直接导致窗口关闭或应用整体崩溃。

#### Scenario: 管理页面结构一致
- **WHEN** 用户打开 Sync Targets、Variables、History 或 Settings 页面
- **THEN** 页面 SHALL 保持与 Stitch 设计稿一致的 split pane、property sheet、toolbar 与状态表达

#### Scenario: 管理页面初始化失败时应用继续运行
- **WHEN** 用户导航到任一管理页面且页面初始化过程中发生异常
- **THEN** 应用程序 SHALL 保留应用壳层和导航能力，并将失败表现为当前页面的错误或降级内容

### Requirement: 状态变体按 Stitch 落地
应用程序 SHALL 按 Stitch 已提供的状态变体实现 Sync Targets 连接失败、Variables 模板解析错误与 History 空状态，并保持与正常页面相同的壳与布局。

#### Scenario: 连接失败状态显示
- **WHEN** Sync Targets 页面出现连接测试失败
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的失败状态，并保留相同的壳与管理页面结构

### Requirement: 设置页面遵循 Stitch 信息层级
应用程序 SHALL 按 Stitch Settings 设计稿实现外观与通用设置界面，并保留设计稿中的分组、密度与桌面工具式布局。Settings 页面作为导航目标时，其控件初始化、资源绑定或首次数据加载失败 SHALL 不得导致应用整体退出。

#### Scenario: 设置页面显示
- **WHEN** 用户进入 Settings 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的设置分组、控件层级与页面结构

#### Scenario: Settings 初始化失败时保留导航能力
- **WHEN** 用户进入 Settings 页面且页面初始化发生异常
- **THEN** 应用程序 SHALL 保留主窗口壳层与其他页面导航能力，并在 Settings 内容区显示受控错误状态
