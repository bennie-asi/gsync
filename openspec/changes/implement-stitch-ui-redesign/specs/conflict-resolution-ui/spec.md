## MODIFIED Requirements

### Requirement: Conflict Resolution 按安全优先对比界面落地
Conflict Resolution SHALL 优先显示可读文件名、本地与目标状态、逐文件决策及安全提示，并按新版 Stitch 布局保留汇总与批量操作。

#### Scenario: 逐文件比较
- **WHEN** 用户打开有差异的比较结果
- **THEN** 页面 SHALL 显示每个文件两端状态和保留本地、保留远端或暂不处理的选择

### Requirement: Conflict Resolution 提供显式解决动作
用户 SHALL 能应用已决定文件而暂不处理其他文件；写入目标和覆盖本地的方向以及本地快照提示 SHALL 清晰可见，执行前 SHALL 请求确认。

#### Scenario: 部分决策
- **WHEN** 一部分文件已选择处理方式而其他文件未选择
- **THEN** 用户 SHALL 能确认并提交已选择的决策，未选择的文件保持不变
