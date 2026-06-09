## ADDED Requirements

### Requirement: Solution 项目分层
Solution SHALL 包含以下项目，各项目职责不得混淆：
- `GSYNC.App`：WinUI 3 UI 层，Pages、ViewModels、App 入口
- `GSYNC.Core`：领域模型、接口定义、SyncEngine、PathResolver、ManifestService
- `GSYNC.Data`：SQLite 持久化实现
- `GSYNC.Providers`：ISourceProvider 内置实现（Steam、Epic、Custom）
- `GSYNC.Storage`：IStorageProvider 内置实现（WebDAV、LocalFolder）
- `GSYNC.Manifest`：Ludusavi 解析、变量映射、定义聚合

#### Scenario: UI 层隔离
- **WHEN** 构建 GSYNC.Core、GSYNC.Data、GSYNC.Providers、GSYNC.Storage、GSYNC.Manifest
- **THEN** 这些项目 SHALL 不包含对 WinUI SDK 或 Microsoft.UI.Xaml 的直接引用

### Requirement: 依赖方向约束
项目间依赖 SHALL 遵循单向规则：所有项目可依赖 GSYNC.Core，GSYNC.Core 不得依赖任何其他 GSYNC.* 项目。

#### Scenario: 循环依赖检测
- **WHEN** 任意项目尝试引用 GSYNC.Core 而 GSYNC.Core 也直接或间接引用该项目
- **THEN** 构建 SHALL 失败（循环依赖）

### Requirement: 测试项目覆盖
Solution SHALL 包含以下测试项目：GSYNC.Core.Tests、GSYNC.PathResolver.Tests、GSYNC.Manifest.Tests、GSYNC.Storage.Tests。

#### Scenario: 无 UI 依赖的测试执行
- **WHEN** 运行所有测试项目
- **THEN** 测试 SHALL 在无显示器环境（CI）中无需用户交互即可执行完成
