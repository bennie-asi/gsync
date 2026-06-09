## Context

GSYNC 是一个 Windows 桌面工具，用于在多设备间同步游戏存档。项目目前处于规划阶段，只有 UI 原型和设计文档，没有任何应用代码。本文档记录在首次编写代码之前确认的全部架构决策，作为后续所有实现工作的技术约束基线。

现状约束：
- 目标平台：Windows-first（Mac/Linux 为远期可能目标，不是当前约束）
- 交互需要读写用户文件系统上任意路径的游戏存档，不能有沙箱限制
- 设计稿已基于 Fluent 深色 utility 风格完成，技术选型需与之匹配
- 无现有代码库，无历史技术债

## Goals / Non-Goals

**Goals:**

- 确定并冻结语言、运行时、UI 框架、MVVM 方案
- 定义 Solution 的项目分层结构和各层职责边界
- 定义提供者插件接口契约（ISourceProvider / IStorageProvider）
- 确定持久化双轨方案（SQLite + YAML）
- 确定社区游戏数据聚合策略（Ludusavi manifest 导入层）
- 确定同步引擎串行队列的行为边界

**Non-Goals:**

- 不包含具体功能实现（UI shell、同步算法等在各自变更中实现）
- 不定义数据库 schema 细节（属于 persistence-strategy 的实现范畴）
- 不定义具体的 Ludusavi 字段映射表（属于 manifest-data-model 实现范畴）
- 不讨论 Mac/Linux 跨平台移植路径

## Decisions

### D1：WinUI 3 (Unpackaged) 取代 Avalonia

**选择**：WinUI 3 via WinAppSDK，Unpackaged 部署模式。

**理由**：
- GSYNC 需要读写用户任意路径的游戏存档，Packaged (MSIX) 的沙箱会阻止这一能力
- WinUI 3 的 NavigationView 与设计稿左侧 rail 完美对应，开发摩擦最小
- Fluent Design（Mica/Acrylic 材质）直接适配设计方向，无需自行实现深色主题基础设施
- Windows Credential Manager 原生集成，密钥存储无需第三方库
- 放弃 Avalonia 的跨平台能力是已知 trade-off，Windows-first 策略下可接受

**备选考虑过的方案**：
- Avalonia：跨平台，但深色 Fluent 体验需大量手工实现，且 Windows 集成较弱
- WinForms / WPF：不支持 Fluent/Mica，视觉差距大
- MAUI：偏移动感，WebView 依赖，不适合 desktop utility

---

### D2：插件式提供者架构（接口 + 内置实现）

**选择**：在 GSYNC.Core 中定义 `ISourceProvider` 和 `IStorageProvider` 接口，MVP 内置核心提供者，不做运行时动态加载（非 MEF/Plugin DLL）。

```
ISourceProvider
  ProviderId: string
  DisplayName: string
  ScanAsync(ct) → IReadOnlyList<DiscoveredGame>
  ResolveVariables(instance) → IReadOnlyDictionary<string, string>

IStorageProvider
  ProviderId: string
  DisplayName: string
  TestConnectionAsync(config, ct) → ConnectionResult
  UploadAsync(remotePath, stream, ct) → Task
  DownloadAsync(remotePath, ct) → Stream
  ListAsync(namespace, ct) → IReadOnlyList<RemoteEntry>
  DeleteAsync(remotePath, ct) → Task
```

**内置实现清单（MVP）**：
- Source：Steam、Epic、Custom（手动）
- Storage：WebDAV、Local Folder

**理由**：
- 接口隔离确保 SyncEngine 不依赖具体提供者，可独立测试
- 内置实现而非动态加载，避免 MVP 阶段的发现机制复杂度
- 接口一旦确立，未来添加新提供者只需实现接口，无需修改核心

---

### D3：持久化双轨方案

**选择**：SQLite 存运行时数据，YAML 文件存用户游戏内容定义。

| 存储 | 内容 |
|------|------|
| SQLite | GameInstance、StorageBinding、SyncRecord、Snapshot 元数据、变量覆盖 |
| YAML 文件 | 用户自定义 GameContentDefinition（游戏定义、内容项、路径模板） |

**理由**：
- SQLite 适合查询、历史记录、关系型运行时状态；YAML 适合人类可读的配置文件
- 用户可直接用文本编辑器查看/修改自定义游戏定义，便于调试和备份
- 导入的社区定义（来自 Ludusavi）在合并后以 SQLite 缓存形式存储，不污染用户 YAML

---

### D4：社区游戏数据来自 Ludusavi manifest，GSYNC 自有格式为主

**选择**：定义 GSYNC 自己的 `GameContentDefinition` 格式；提供 Ludusavi manifest 导入层，作为社区数据的一等来源；不直接采用 Ludusavi 格式作为 GSYNC 的内部格式。

**Ludusavi 变量映射层**：

| Ludusavi 变量 | GSYNC 变量 |
|---------------|-----------|
| `<base>` | `%GAME_INSTALL_DIR%` |
| `<home>` | `%HOME%` |
| `<winAppData>` | `%APPDATA%` |
| `<winDocuments>` | `%DOCUMENTS%` |
| `<storeGameId>` | `%STEAM_APP_ID%` / `%EPIC_APP_ID%` |
| `<storeUserId>` | `%STEAM_USER_ID%` |

**Manifest 更新策略**：app 内置一份 manifest 快照（保证离线可用）；启动时后台静默拉取最新版本；用户可手动触发强制更新。

**理由**：
- GSYNC 自有格式支持 slot 模式、冲突策略、多平台条件等 Ludusavi 不覆盖的语义
- Ludusavi manifest 有数千游戏的社区维护数据，直接复用是最低成本的冷启动方案
- 保持格式独立，未来可接入其他数据源（如 PCGamingWiki 直接 API）

---

### D5：串行同步队列

**选择**：SyncEngine 内部维护单一串行操作队列，一次只处理一个 SyncJob。

```
SyncJob:
  GameInstanceId
  Direction (Upload / Download / Compare)
  CancellationToken
  IProgress<SyncProgress>

SyncQueue (内部):
  Channel<SyncJob>（单消费者）
  ActiveJob: SyncJob?
  QueueDepth: int
```

**理由**：
- 避免并发写入同一游戏文件导致的数据损坏
- 快照和 SyncRecord 写入顺序确定，SQLite 并发压力小
- UI 状态简单：要么空闲，要么有一个活跃任务
- 串行不代表阻塞 UI——SyncEngine 运行在后台线程，通过 IProgress 推送更新

---

### D6：Solution 分层结构

```
GSYNC.sln
├── src/
│   ├── GSYNC.App          # WinUI 3 + ViewModels + Pages（UI 层，唯一依赖 WinUI SDK）
│   ├── GSYNC.Core         # 领域模型 + 接口 + SyncEngine + PathResolver + ManifestService
│   ├── GSYNC.Data         # SQLite 持久化实现（实现 GSYNC.Core 中的存储接口）
│   ├── GSYNC.Providers    # ISourceProvider 内置实现（Steam / Epic / Custom）
│   ├── GSYNC.Storage      # IStorageProvider 内置实现（WebDAV / LocalFolder）
│   └── GSYNC.Manifest     # Ludusavi YAML 解析 + 变量映射 + 用户定义合并
└── tests/
    ├── GSYNC.Core.Tests
    ├── GSYNC.PathResolver.Tests
    ├── GSYNC.Manifest.Tests
    └── GSYNC.Storage.Tests
```

**依赖方向规则**（单向，不可反向）：
```
App → Core ← Data
App → Core ← Providers
App → Core ← Storage
App → Core ← Manifest
Manifest → Core
Data → Core
Providers → Core
Storage → Core
```

GSYNC.Core 不依赖任何具体实现，只定义接口和领域模型。

## Risks / Trade-offs

**[Windows-only]** 放弃了 Avalonia 的跨平台能力。
→ 接受。Windows-first 是明确策略，Core 层设计时避免 Windows-specific API（路径解析、文件操作），为未来移植保留空间。

**[Unpackaged 无自动更新]** Unpackaged 应用不能使用 MSIX 自动更新机制。
→ 近期可接受（工具软件用户可接受手动更新）；中期可集成 Squirrel.Windows 或 WinSparkle 等更新框架。

**[Ludusavi 导入层维护成本]** Ludusavi 格式若发生 breaking change，导入层需同步更新。
→ 在 GSYNC.Manifest 中隔离解析逻辑，变更影响面可控；订阅 ludusavi-manifest 仓库 release 通知。

**[串行队列限制吞吐]** 若用户有大量游戏需要批量同步，串行会成为瓶颈。
→ MVP 阶段可接受。未来可升级为 per-game 并发（每个 GameInstance 独立队列），不需要修改接口契约。

**[YAML 用户定义与 SQLite 缓存的同步]** 用户手动编辑 YAML 后需要触发重新解析和缓存更新。
→ 在 ManifestService 中监听 YAML 文件变更（FileSystemWatcher），或在每次启动时全量重新加载。

## Open Questions

- **EF Core vs Dapper**：SQLite 访问层使用 EF Core（更快上手，schema migration 内置）还是 Dapper（更轻量，SQL 透明）？建议 EF Core + Code First，决策可在 persistence-strategy 变更中确认。
- **App 配置文件位置**：YAML 用户定义和 SQLite 数据库存储在 `%APPDATA%\GSYNC\` 还是应用目录旁？建议 `%APPDATA%\GSYNC\` 以兼容多用户环境。
- **Manifest 拉取来源**：直接从 ludusavi-manifest GitHub raw 拉取，还是 GSYNC 自己托管镜像？建议直接拉取 GitHub，中期考虑 CDN 镜像。
