## ADDED Requirements

### Requirement: Conflict Resolution SHALL 支持文件级冲突决策
系统 SHALL 允许用户在同一组比较结果中对每个差异文件分别选择保留本地版本、保留远端版本或暂不处理，而不是只能对整组内容执行统一上传或统一下载。

#### Scenario: 用户对单个文件做不同决策
- **WHEN** 一个比较结果中同时包含多个冲突文件
- **THEN** 用户 SHALL 能为不同文件设置不同的保留决策

#### Scenario: 用户暂不处理部分文件
- **WHEN** 用户只想处理其中一部分差异文件
- **THEN** 系统 SHALL 允许未被选定的文件保持未决状态，而不是强制一次性处理整组差异

### Requirement: Conflict Resolution SHALL 支持批量决策快捷动作
系统 SHALL 在文件级决策之上提供批量快捷动作，例如全部保留本地、全部保留远端或按当前筛选批量设置，以减少大量冲突文件时的重复操作。

#### Scenario: 用户批量选择全部保留本地
- **WHEN** 用户点击批量保留本地动作
- **THEN** 系统 SHALL 将当前适用范围内的差异文件决策设置为保留本地

#### Scenario: 批量动作后仍可逐文件调整
- **WHEN** 用户先执行批量动作后又修改某个单独文件的决策
- **THEN** 系统 SHALL 保留该文件的显式覆盖决定，而不是强制回退到批量默认值

### Requirement: Conflict Resolution SHALL 仅对已决文件应用覆盖
系统 SHALL 只对用户已明确决策的文件执行覆盖操作，未决文件 SHALL 不被上传、下载或删除。

#### Scenario: 存在未决文件
- **WHEN** 用户提交冲突解决方案时仍有未决文件
- **THEN** 系统 SHALL 仅应用已决文件的操作，并保留未决文件供后续继续处理
