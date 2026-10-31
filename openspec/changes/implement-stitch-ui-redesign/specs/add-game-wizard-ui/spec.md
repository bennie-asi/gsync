## MODIFIED Requirements

### Requirement: Add Game Wizard 六步流程按 Stitch 落地
Add Game Wizard SHALL 将已有的来源、游戏、内容、路径、目标与完成阶段呈现为游戏与存档、同步目标、完成确认三个清晰的用户阶段，并保留有效的前进、后退和取消路径。

#### Scenario: 手动添加游戏
- **WHEN** 用户选择手动配置
- **THEN** 用户 SHALL 能输入游戏名称及文件或目录并在取消后保留向导上下文

#### Scenario: 目标配置往返
- **WHEN** 用户从向导配置或测试目标并返回
- **THEN** 已填写的游戏与存档信息 SHALL 保留
