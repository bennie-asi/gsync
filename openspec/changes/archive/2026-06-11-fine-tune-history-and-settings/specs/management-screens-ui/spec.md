## MODIFIED Requirements

### Requirement: 管理页面按 Stitch 落地
应用程序 SHALL 按 Stitch refined / normalized 设计稿实现 Sync Targets、Variables、History 与 Settings 页面，并保持高信息密度的桌面工具风格。对于 Sync Targets 页面，应用程序 SHALL 提供与 refined Sync Targets 设计稿一致的 split pane 管理结构，包括顶部工具区、左侧目标列表/表格区、右侧属性编辑区，以及与页面状态匹配的操作层级。对于 Variables 页面，应用程序 SHALL 提供与 refined Variables 设计稿一致的 split pane 管理结构，包括顶部工具区、左侧变量列表/表格区、右侧详情与模板测试区，以及与页面状态匹配的操作层级。对于 History 页面，应用程序 SHALL 提供与 refined History 设计稿一致的顶部筛选/操作区、左侧历史记录表格区与右侧详情/快照区；对于 Settings 页面，应用程序 SHALL 提供与 refined Settings 设计稿一致的设置分组导航、中心设置面板与右侧辅助检查/预览区，而不是退化为松散卡片页或单列表单。后续对 History 与 Settings 的细调 SHALL 优先收敛页面内的硬编码文案、局部模板和状态表达，而不是继续保留页面专属分叉。

#### Scenario: 管理页面结构一致
- **WHEN** 用户打开 Sync Targets、Variables、History 或 Settings 页面
- **THEN** 页面 SHALL 保持与 Stitch 设计稿一致的 split pane、property sheet、toolbar 与状态表达

#### Scenario: Sync Targets 页面按 refined 稿显示
- **WHEN** 用户打开 Sync Targets 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的目标列表区、属性编辑区、顶部工具区和主要操作按钮，而不是退化为松散卡片页或不对称布局

#### Scenario: Variables 页面按 refined 稿显示
- **WHEN** 用户打开 Variables 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的变量列表区、详情/测试区、顶部工具区和主要操作按钮，而不是退化为松散卡片页或不对称布局

#### Scenario: History 页面按 refined 稿显示
- **WHEN** 用户打开 History 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的筛选工具区、历史记录表格、详情摘要和快照列表，并保持桌面工具式高密度布局

#### Scenario: Settings 页面按 refined 稿显示
- **WHEN** 用户打开 Settings 页面
- **THEN** 页面 SHALL 显示符合 Stitch refined 设计稿的设置分组、中心属性编辑区和右侧辅助检查/预览区，而不是改成居中的宽松卡片页

#### Scenario: History 与 Settings 细调优先收敛页面级分叉
- **WHEN** History 或 Settings 已大体符合 refined 骨架但仍存在局部硬编码、模板重复或状态表达不稳
- **THEN** 后续细调 SHALL 优先收敛这些页面级分叉，而不是继续扩散新的专属结构

### Requirement: 状态变体按 Stitch 落地
应用程序 SHALL 按 Stitch 已提供的状态变体实现 Sync Targets 连接失败、Variables 模板解析错误与 History 空状态，并保持与正常页面相同的壳与布局。对于 History empty state，应用程序 SHALL 在保留同一页面骨架的前提下显示符合 refined empty state 的焦点内容与恢复动作。History 的后续细调 SHALL 确保 empty state 与正常态在筛选区、右侧详情/快照区和页面密度上继续保持一致。

#### Scenario: 连接失败状态显示
- **WHEN** Sync Targets 页面出现连接测试失败
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的失败状态，并保留相同的壳与管理页面结构

#### Scenario: History 空状态显示
- **WHEN** History 页面当前没有任何记录或快照
- **THEN** 应用程序 SHALL 在保留 History 页面相同骨架和主区域结构的前提下，显示符合 Stitch refined empty state 的空状态表达与恢复动作

#### Scenario: History 空状态保持细调后结构一致
- **WHEN** 用户在 History 正常态与空状态之间切换
- **THEN** 筛选区、右侧详情/快照区和整体密度 SHALL 继续保持一致，而不是让空状态退化为脱离主结构的独立页面

### Requirement: 设置页面遵循 Stitch 信息层级
应用程序 SHALL 按 Stitch Settings 设计稿实现外观与通用设置界面，并保留设计稿中的分组、密度与桌面工具式布局。设置页 SHALL 将全局设置分组、主编辑区和辅助预览/检查区域清晰分层，而不是将不同层级的设置项混成单一区域。后续对 Settings 的细调 SHALL 优先收敛分组导航语义、按钮主次和右侧辅助区表达。

#### Scenario: 设置页面显示
- **WHEN** 用户进入 Settings 页面
- **THEN** 应用程序 SHALL 显示符合 Stitch 设计稿的设置分组、控件层级与页面结构

#### Scenario: 设置分组与预览分区清晰
- **WHEN** 用户查看 Settings 页面
- **THEN** 用户 SHALL 能清楚区分设置分组导航、中心编辑区和右侧辅助区，而不是在一个长表单中混杂所有设置项

#### Scenario: Settings 细调收紧分组与辅助区语义
- **WHEN** 设置页面继续进行 refined 细调
- **THEN** 分组导航、中心编辑区和右侧辅助区的标题、按钮层级与状态表达 SHALL 继续向清晰分层收敛，而不是保留数据页式或占位式表达
