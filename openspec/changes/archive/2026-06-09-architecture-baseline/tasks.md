## 1. Solution 脚手架

- [x] 1.1 创建 `GSYNC.sln` 及所有项目文件（App/Core/Data/Providers/Storage/Manifest + 4 个测试项目）
- [x] 1.2 配置各项目的 `<TargetFramework>`：UI 层 `net8.0-windows10.0.19041.0`，其余 `net8.0`
- [x] 1.3 添加项目间引用，确保依赖方向符合 solution-structure spec（单向，Core 不引用具体实现）
- [x] 1.4 配置 WinAppSDK（Unpackaged）：添加 `WindowsAppSDK` NuGet 包，设置 `<WindowsPackageType>None</WindowsPackageType>`
- [x] 1.5 添加核心 NuGet 依赖：CommunityToolkit.Mvvm、Microsoft.Data.Sqlite（或 EF Core）、YamlDotNet、Serilog、SharpZipLib

## 2. GSYNC.Core：领域模型与接口

- [x] 2.1 定义领域模型：`GameDefinition`、`GameInstance`、`ContentItem`、`StorageBinding`、`SyncRecord`、`Snapshot`、`Conflict`
- [x] 2.2 定义 `ISourceProvider` 接口（ProviderId、DisplayName、ScanAsync、ResolveVariables）
- [x] 2.3 定义 `IStorageProvider` 接口（ProviderId、DisplayName、TestConnectionAsync、UploadAsync、DownloadAsync、ListAsync、DeleteAsync）
- [x] 2.4 定义 `DiscoveredGame`、`ConnectionResult`、`RemoteEntry`、`SyncProgress` 等传输/结果类型
- [x] 2.5 定义 `GameContentDefinition` 和 `ContentItem` 的完整模型（含 category、pathTemplates、include/exclude、defaultEnabled）

## 3. GSYNC.Core：PathResolver

- [x] 3.1 实现路径模板解析器，支持 `%VAR_NAME%` 格式变量替换
- [x] 3.2 实现变量优先级栈（system < source < game instance < user override）
- [x] 3.3 实现内置系统变量解析：`%HOME%`、`%APPDATA%`、`%LOCALAPPDATA%`、`%DOCUMENTS%`
- [x] 3.4 为 PathResolver 编写单元测试（正常替换、未知变量、优先级覆盖、空模板等场景）

## 4. GSYNC.Manifest：Ludusavi 导入与定义聚合

- [x] 4.1 实现 Ludusavi YAML 解析器（解析 files、installDir、steam 字段）
- [x] 4.2 实现 Ludusavi 变量映射层（`<base>` → `%GAME_INSTALL_DIR%` 等，见 design.md D4）
- [x] 4.3 内置一份 Ludusavi manifest 快照作为嵌入资源（Embedded Resource）
- [x] 4.4 实现 `ManifestService`：聚合用户 YAML、SQLite 缓存、社区导入，按优先级合并
- [x] 4.5 实现后台 manifest 更新（启动后异步拉取，静默替换本地缓存）
- [x] 4.6 为 Ludusavi 解析器和 ManifestService 编写单元测试

## 5. GSYNC.Data：SQLite 持久化

- [x] 5.1 定义 EF Core DbContext（或等效方案），包含所有实体表
- [x] 5.2 实现 schema 初始化与 migration 逻辑（首次启动自动建库）
- [x] 5.3 实现 GameInstance、StorageBinding 的 CRUD 仓储
- [x] 5.4 实现 SyncRecord、Snapshot 元数据的写入与查询仓储
- [x] 5.5 确认数据库文件路径为 `%APPDATA%\GSYNC\data.db`

## 6. GSYNC.Providers：内置 Source 提供者

- [x] 6.1 实现 `SteamSourceProvider`：读取 Steam 库路径、枚举已安装游戏、解析 Steam 变量
- [x] 6.2 实现 `EpicSourceProvider`：读取 Epic Games Launcher 已安装游戏列表
- [x] 6.3 实现 `CustomSourceProvider`：支持用户手动指定安装路径

## 7. GSYNC.Storage：内置 Storage 提供者

- [x] 7.1 实现 `WebDavStorageProvider`：TestConnection、Upload、Download、List、Delete
- [x] 7.2 实现 `LocalFolderStorageProvider`：基于本地文件夹的同步目标
- [x] 7.3 为 Storage 提供者编写集成测试（LocalFolder 可直接测试，WebDAV 需 mock 或测试服务器）

## 8. GSYNC.Core：SyncEngine

- [x] 8.1 实现 `SyncQueue`（串行 Channel<SyncJob>，单消费者）
- [x] 8.2 实现 `SyncJob` 模型（GameInstanceId、Direction、CancellationToken、IProgress）
- [x] 8.3 实现 Upload 操作流程：读取 ContentItem 路径 → 解析路径 → 上传文件
- [x] 8.4 实现 Download 操作流程：快照前置保护（ZIP） → 下载文件 → 更新本地
- [x] 8.5 实现 Compare 操作：对比本地与远端文件（hash/timestamp），生成差异报告
- [x] 8.6 实现 SyncRecord 写入（每次操作完成后记录结果到 SQLite）
- [x] 8.7 实现取消逻辑：CancellationToken 检查点 + 临时文件清理

## 9. GSYNC.App：应用入口与 DI

- [x] 9.1 配置 WinUI 3 App 入口，启用 Unpackaged 模式
- [x] 9.2 配置 DI 容器，注册 Core 服务、所有内置提供者、Data 仓储、ManifestService
- [x] 9.3 配置 Serilog，输出到 `%APPDATA%\\GSYNC\\logs\\`，支持文件滚动
- [x] 9.4 配置主窗口 Mica/Acrylic 材质和深色主题
- [x] 9.5 实现 NavigationView shell（左侧 rail：Library、Sync Targets、Variables、History、Settings）
