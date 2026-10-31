## MODIFIED Requirements

### Requirement: Library 界面落地
Library SHALL 按新版 Stitch 设计展示搜索、状态筛选、添加游戏、检查并同步和单向上传全部操作，以及带状态、目标、最近同步时间、打开和比较操作的游戏行。冲突提示 SHALL 基于真实比较结果。

#### Scenario: 游戏列表显示
- **WHEN** 用户打开有游戏的 Library
- **THEN** 每个游戏 SHALL 显示名称、真实状态、目标以及打开和比较操作

#### Scenario: 真实冲突提示
- **WHEN** 比较发现存档冲突
- **THEN** 页面 SHALL 明确提示内容不同并提供比较入口，不以时间推断覆盖方向

### Requirement: Game Details 界面落地
Game Details SHALL 展示游戏上下文、同步操作、存档路径与状态、最近活动和可恢复快照，并保留路径检查及恢复确认。

#### Scenario: 查看和操作游戏
- **WHEN** 用户打开游戏详情
- **THEN** 用户 SHALL 能查看目标与存档路径，并访问检查、上传、下载和恢复操作

### Requirement: Library 状态变体落地
Library SHALL 按新版 Stitch 空状态引导用户添加游戏或配置目标，并保留搜索无结果和同步进行中状态。

#### Scenario: 首次使用
- **WHEN** 没有游戏实例
- **THEN** 页面 SHALL 说明游戏与目标的设置步骤并提供相关入口
