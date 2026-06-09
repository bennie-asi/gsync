## ADDED Requirements

### Requirement: 语言和运行时约束
应用程序 SHALL 使用 C# 和 .NET 8 构建，不得引入其他语言运行时。

#### Scenario: 编译目标
- **WHEN** 构建 Solution 中的任意项目
- **THEN** 所有项目的目标框架 SHALL 为 `net8.0-windows` 或 `net8.0`（GSYNC.Core 等纯逻辑层）

### Requirement: UI 框架约束
桌面 UI SHALL 使用 WinUI 3（WinAppSDK）实现，采用 Unpackaged 部署模式。

#### Scenario: 无沙箱限制
- **WHEN** 应用程序需要读写用户文件系统上的任意游戏存档路径
- **THEN** 应用程序 SHALL 能够访问 `%APPDATA%`、`%DOCUMENTS%`、`%LOCALAPPDATA%` 及任意自定义路径，不受沙箱约束

#### Scenario: Fluent 材质
- **WHEN** 主窗口渲染
- **THEN** 窗口背景 SHALL 应用 Mica 或 Acrylic 材质，颜色主题 SHALL 为深色

### Requirement: MVVM 方案约束
应用程序 SHALL 使用 CommunityToolkit.Mvvm 实现 MVVM 架构。ViewModel 中不得直接引用 WinUI 控件类型。

#### Scenario: ViewModel 独立性
- **WHEN** 单元测试实例化任意 ViewModel
- **THEN** 测试 SHALL 无需启动 WinUI Application 即可运行

### Requirement: 辅助库选型约束
日志 SHALL 使用 Serilog；快照存档 SHALL 使用 ZIP 格式（`System.IO.Compression` 或等效库）；密钥存储 SHALL 使用 Windows Credential Manager。

#### Scenario: 日志输出
- **WHEN** 应用程序运行时发生任意操作事件
- **THEN** Serilog SHALL 将结构化日志写入文件，日志文件存储在 `%APPDATA%\GSYNC\logs\` 下
