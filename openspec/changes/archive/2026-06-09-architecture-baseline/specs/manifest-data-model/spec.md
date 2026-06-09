## ADDED Requirements

### Requirement: GSYNC 自有 GameContentDefinition 格式
GSYNC.Manifest SHALL 定义 `GameContentDefinition` 模型作为内部格式，不直接使用 Ludusavi YAML 结构。模型 SHALL 包含：gameId、displayName、contentItems（列表）、sourceHints。每个 ContentItem SHALL 包含：contentId、category（save/config/extra）、pathTemplates、include/exclude 规则、defaultEnabled 标志。

#### Scenario: 定义加载
- **WHEN** ManifestService 加载一个 GameContentDefinition
- **THEN** SHALL 返回包含完整 ContentItem 列表的强类型模型，所有路径模板使用 GSYNC 变量格式（`%VAR_NAME%`）

### Requirement: Ludusavi manifest 导入层
GSYNC.Manifest SHALL 提供 Ludusavi YAML 解析器，将 Ludusavi 格式转换为 GameContentDefinition。解析器 SHALL 处理变量映射（见 design.md D4 映射表）。

#### Scenario: Ludusavi 变量转换
- **WHEN** 解析含 `<winAppData>` 路径的 Ludusavi 条目
- **THEN** 输出的 ContentItem.pathTemplates SHALL 使用 `%APPDATA%` 替换 `<winAppData>`

#### Scenario: 未知变量处理
- **WHEN** Ludusavi 条目包含映射表中不存在的变量（如 Linux 专用 `<xdgData>`）
- **THEN** 解析器 SHALL 跳过该路径条目并记录警告日志，不抛出异常

### Requirement: 定义来源聚合与优先级
ManifestService SHALL 聚合三个来源的定义，优先级从高到低：用户自定义 YAML → 用户本地覆盖记录（SQLite）→ 社区导入缓存（Ludusavi，SQLite）。同一 gameId 的高优先级来源 SHALL 完全覆盖低优先级来源。

#### Scenario: 用户覆盖社区定义
- **WHEN** 用户为一个已有社区定义的游戏创建了自定义 YAML 定义
- **THEN** ManifestService SHALL 返回用户 YAML 中的定义，社区定义不可见

### Requirement: Manifest 更新策略
应用程序 SHALL 内置一份 Ludusavi manifest 快照以支持离线使用；SHALL 在启动后后台静默检查并拉取最新 manifest；SHALL 提供用户手动触发更新的入口。

#### Scenario: 离线启动
- **WHEN** 应用程序在无网络环境下启动
- **THEN** 应用程序 SHALL 正常加载内置 manifest 快照，不显示错误，仅在后台更新失败时记录日志

#### Scenario: 后台更新成功
- **WHEN** 启动后后台拉取到比本地版本更新的 manifest
- **THEN** ManifestService SHALL 静默替换本地缓存，无需重启即可使用新数据
