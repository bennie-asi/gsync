## Purpose

定义 GSYNC 第二阶段 UI 中管理页面与状态变体的落地要求。

## Requirements

### Requirement: 管理页面按 Stitch 落地
应用程序 SHALL 按 Stitch refined / normalized 设计稿实现 Sync Targets、Variables、History 与 Settings 页面，并保持高信息密度的桌面工具风格。

#### Scenario: 管理页面结构一致
- **WHEN** 用户打开 Sync Targets、Variables、History 或 Settings 页面
- **THEN** 页面 SHALL 保持与 Stitch 设计稿一致的 split pane、property sheet、toolbar 与状态表达

### Requirement: 状态变体按 Stitch 落地
应用程序 SHALL 按 Stitch 已提供的状态变体实现 Sync Targets 连接失败、Variables 模板解析错误与 History 空状态，并保持与正常页面相同的壳与布局。

#### Scenario: 连接失败状态显示
- **WHEN** Sync Targets 页面出现连接测试失败
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的失败状态，并保留相同的壳与管理页面结构

### Requirement: 设置页面遵循 Stitch 信息层级
应用程序 SHALL 按 Stitch Settings 设计稿实现外观与通用设置界面，并保留设计稿中的分组、密度与桌面工具式布局。

#### Scenario: 设置页面显示
- **WHEN** 用户进入 Settings 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的设置分组、控件层级与页面结构
