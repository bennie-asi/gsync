## Why

GSYNC 目前只有规划文档和 UI 原型，尚无任何应用代码。在开始编码之前，需要将探讨阶段确认的技术架构决策正式固化，形成工程基线，以确保后续所有实现任务在统一的技术约束下推进。

## What Changes

- 确立开发语言和运行时：C# / .NET 8
- 确立桌面 UI 框架：WinUI 3（WinAppSDK，Unpackaged 模式）并向 Fluent Design 靠拢
- 确立 MVVM 方案：CommunityToolkit.Mvvm
- 确立插件式提供者架构：`ISourceProvider` / `IStorageProvider` 接口，核心内置 Steam、Epic、Custom（Source）及 WebDAV、Local（Storage）实现
- 确立持久化策略：SQLite 存运行时数据与历史记录；YAML 文件存用户自定义游戏内容定义
- 确立社区游戏数据方案：以 GSYNC 自有 ContentDefinition 格式为主，提供 Ludusavi manifest 导入层；支持内置快照 + 启动后台刷新 + 用户手动触发三种更新模式
- 确立同步引擎行为：串行操作队列，支持 CancellationToken 和 IProgress 进度报告
- 确立辅助技术选型：Serilog（日志）、ZIP（快照存档）、Windows Credential Manager（密钥存储）
- 确立 Solution 分层结构：GSYNC.App / GSYNC.Core / GSYNC.Data / GSYNC.Providers / GSYNC.Storage / GSYNC.Manifest

## Capabilities

### New Capabilities

- `tech-stack`: 开发语言、运行时、UI 框架、MVVM 方案的选型约束
- `solution-structure`: Solution 项目分层与职责边界定义
- `provider-plugin-model`: ISourceProvider / IStorageProvider 接口契约与内置实现清单
- `sync-engine-behavior`: 串行同步队列、取消、进度报告的行为规格
- `persistence-strategy`: SQLite + YAML 双轨持久化方案，各自的存储职责边界
- `manifest-data-model`: 社区游戏数据（Ludusavi 导入）与用户自定义定义的聚合策略

### Modified Capabilities

## Impact

- 所有后续实现任务（UI shell、同步引擎、提供者实现）均以本基线为约束
- 依赖：WinAppSDK、CommunityToolkit.Mvvm、Microsoft.Data.Sqlite（或 EF Core）、YamlDotNet、Serilog、SharpZipLib 或 System.IO.Compression
- 排除 Avalonia、WebView shell、MAUI 等备选方案，不再作为讨论选项
- 确认 Windows-first 策略：Unpackaged 部署意味着不上架 Microsoft Store，可读写任意文件系统路径
