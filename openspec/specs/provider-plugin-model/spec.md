## Purpose

定义 Source Provider 与 Storage Provider 的接口契约与应用注册行为。

## Requirements

### Requirement: ISourceProvider 接口契约
GSYNC.Core SHALL 定义 `ISourceProvider` 接口，包含以下成员：
- `ProviderId: string`（唯一标识符）
- `DisplayName: string`（UI 显示名）
- `ScanAsync(CancellationToken) → Task<IReadOnlyList<DiscoveredGame>>`
- `ResolveVariables(GameInstance) → IReadOnlyDictionary<string, string>`

#### Scenario: Steam provider 扫描
- **WHEN** SteamSourceProvider.ScanAsync 被调用且 Steam 已安装
- **THEN** SHALL 返回当前用户 Steam 库中所有已安装游戏的 DiscoveredGame 列表

#### Scenario: 变量解析
- **WHEN** ResolveVariables 被调用，传入一个绑定了 Steam 游戏的 GameInstance
- **THEN** SHALL 返回包含 `%STEAM_APP_ID%`、`%STEAM_USER_ID%`、`%STEAM_LIBRARY%`、`%GAME_INSTALL_DIR%` 的字典

### Requirement: IStorageProvider 接口契约
GSYNC.Core SHALL 定义 `IStorageProvider` 接口，包含以下成员：
- `ProviderId: string`
- `DisplayName: string`
- `TestConnectionAsync(config, CancellationToken) → Task<ConnectionResult>`
- `UploadAsync(remotePath, Stream, CancellationToken) → Task`
- `DownloadAsync(remotePath, CancellationToken) → Task<Stream>`
- `ListAsync(namespace, CancellationToken) → Task<IReadOnlyList<RemoteEntry>>`
- `DeleteAsync(remotePath, CancellationToken) → Task`

#### Scenario: WebDAV 连接测试
- **WHEN** WebDavStorageProvider.TestConnectionAsync 被调用，传入有效的服务器地址和凭证
- **THEN** SHALL 返回 `ConnectionResult.Success`

#### Scenario: 连接失败处理
- **WHEN** TestConnectionAsync 被调用，服务器不可达或凭证无效
- **THEN** SHALL 返回包含错误描述的 `ConnectionResult.Failure`，不得抛出未处理异常

### Requirement: 内置提供者注册
GSYNC.App SHALL 在启动时将所有内置提供者注册到 DI 容器，无需额外配置即可使用。

#### Scenario: 提供者枚举
- **WHEN** 用户打开"添加游戏"向导的 Source 选择步骤
- **THEN** UI SHALL 显示所有已注册的 ISourceProvider（Steam、Epic、Custom），每项显示 DisplayName
