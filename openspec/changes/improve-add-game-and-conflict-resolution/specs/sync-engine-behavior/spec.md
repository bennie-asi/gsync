## MODIFIED Requirements

### Requirement: 快照前置保护
执行 Download（覆盖本地文件）操作前，SyncEngine SHALL 自动对本地文件创建快照备份。该要求同样 SHALL 适用于文件级冲突解决中任何会覆盖本地文件的选择性应用路径，而不是仅适用于整组 Download 任务。

#### Scenario: 下载前自动快照
- **WHEN** SyncEngine 执行 Download 方向的 SyncJob
- **THEN** SHALL 在覆盖任何本地文件之前，先将受影响文件打包为 ZIP 快照并记录到 SyncRecord

#### Scenario: 文件级冲突解决前自动快照
- **WHEN** 冲突解决方案包含一个或多个将以远端版本覆盖本地文件的决策
- **THEN** SyncEngine SHALL 在应用这些覆盖前先创建本地快照，并将该快照与后续同步记录关联

## ADDED Requirements

### Requirement: SyncEngine SHALL 支持按文件应用冲突决策
SyncEngine SHALL 能接收一组文件级冲突决策，并只对被明确决策的文件执行对应的上传、下载或跳过操作。未决文件 SHALL 保持原状，不得因同组任务的其他文件被隐式覆盖。

#### Scenario: 仅应用已决文件
- **WHEN** 冲突决策集中同时包含已决文件和未决文件
- **THEN** SyncEngine SHALL 只对已决文件执行计划中的操作，并跳过未决文件

#### Scenario: 混合上传与下载决策
- **WHEN** 同一组冲突决策中同时存在保留本地和保留远端的文件
- **THEN** SyncEngine SHALL 在同一次受控执行中分别应用上传和下载操作，而不是要求用户把它们拆成两次完全独立的整组同步
