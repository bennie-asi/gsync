## Purpose

定义 GSYNC 解决方案的项目分层、职责边界与依赖方向。

## Requirements

### Requirement: Solution 项目分层
Solution SHALL 包含以下项目，各项目职责不得混淆：
- `GSYNC.App`：WinUI 3 UI 层，Pages、ViewModels、App 入口、共享 UI primitives
- `GSYNC.Core`：领域模型、接口定义、SyncEngine、PathResolver、ManifestService
- `GSYNC.Data`：SQLite 持久化实现
- `GSYNC.Providers`：ISourceProvider 内置实现（Steam、Epic、Custom）
- `GSYNC.Storage`：IStorageProvider 内置实现（WebDAV、LocalFolder）
- `GSYNC.Manifest`：Ludusavi 解析、变量映射、定义聚合

#### Scenario: UI 层隔离
- **WHEN** 构建 GSYNC.Core、GSYNC.Data、GSYNC.Providers、GSYNC.Storage、GSYNC.Manifest
- **THEN** 这些项目 SHALL 不包含对 WinUI SDK 或 Microsoft.UI.Xaml 的直接引用

#### Scenario: App 层 UI 组织
- **WHEN** 在 GSYNC.App 中实现 Library、Game Details、Sync Targets、Variables、History、Settings 等界面
- **THEN** UI 代码 SHALL 以 Views、ViewModels 和共享 UI primitives 的结构组织，而不是把页面结构、状态逻辑与复用组件全部堆叠在单个窗口文件中

### Requirement: 依赖方向约束
项目间依赖 SHALL 遵循单向规则：所有项目可依赖 GSYNC.Core，GSYNC.Core 不得依赖任何其他 GSYNC.* 项目。

#### Scenario: 循环依赖检测
- **WHEN** 任意项目尝试引用 GSYNC.Core 而 GSYNC.Core 也直接或间接引用该项目
- **THEN** 构建 SHALL 失败（循环依赖）

### Requirement: 测试项目覆盖
Solution SHALL 包含以下测试项目：GSYNC.Core.Tests、GSYNC.PathResolver.Tests、GSYNC.Manifest.Tests、GSYNC.Storage.Tests，并 SHOULD 让共享 UI 状态逻辑尽量下沉到可在这些非 UI 测试项目中验证的层级。

#### Scenario: 无 UI 依赖的测试执行
- **WHEN** 运行所有测试项目
- **THEN** 测试 SHALL 在无显示器环境（CI）中无需用户交互即可执行完成

#### Scenario: UI 状态逻辑可测试
- **WHEN** App 层引入页面状态、筛选状态或导航状态逻辑
- **THEN** 相关逻辑 SHOULD 优先设计为无需启动 WinUI Application 即可在测试项目中验证的形式
