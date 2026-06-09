## Purpose

定义 GSYNC 运行时数据与用户自定义定义的持久化边界。

## Requirements

### Requirement: SQLite 存储运行时数据
GSYNC.Data SHALL 使用 SQLite 存储以下数据：GameInstance、StorageBinding、SyncRecord、Snapshot 元数据、变量覆盖（UserVariableOverride）。数据库文件 SHALL 存储在 `%APPDATA%\GSYNC\data.db`。

#### Scenario: 首次启动数据库初始化
- **WHEN** 应用程序首次启动，`%APPDATA%\GSYNC\data.db` 不存在
- **THEN** 应用程序 SHALL 自动创建数据库并执行初始 schema migration，无需用户干预

#### Scenario: schema 升级
- **WHEN** 新版本应用启动，检测到数据库 schema 版本低于当前版本
- **THEN** 应用程序 SHALL 自动执行迁移脚本将 schema 升级到最新版本

### Requirement: YAML 存储用户游戏内容定义
用户自定义的 GameContentDefinition SHALL 以 YAML 文件形式存储，路径为 `%APPDATA%\GSYNC\definitions\<game-id>.yaml`。

#### Scenario: 用户新建自定义定义
- **WHEN** 用户通过"添加游戏"向导创建一个 Custom 游戏定义
- **THEN** 系统 SHALL 在 definitions 目录下生成对应的 YAML 文件

#### Scenario: 外部编辑后重新加载
- **WHEN** 用户在外部编辑器修改了 YAML 文件，ManifestService 检测到文件变更
- **THEN** ManifestService SHALL 重新加载该文件，更新内存中的定义缓存

### Requirement: 持久化职责边界
SQLite 和 YAML 的职责不得混用：运行时状态（实例、历史、绑定）只存 SQLite；游戏内容定义（路径模板、内容项）只存 YAML（用户定义）或 SQLite 缓存（社区导入定义）。

#### Scenario: 社区定义缓存
- **WHEN** ManifestService 从 Ludusavi manifest 导入游戏定义
- **THEN** 导入结果 SHALL 缓存到 SQLite，不写入用户 YAML 目录
