## ADDED Requirements

### Requirement: Add Game Wizard 六步流程按 Stitch 落地
应用程序 SHALL 按 Stitch refined Add Game Wizard 屏幕实现固定的六步桌面向导流程，包括 Source、Select Game、Content Items、Review Paths、Bind Target 与 Finish 六个步骤。该向导 SHALL 保持左侧步骤轨、右侧当前步骤主内容区与底部主次操作条的稳定结构，而不是在不同步骤间切换为不一致的信息架构。

#### Scenario: Add Game Wizard 正常步骤显示
- **WHEN** 用户进入 Add Game Wizard 并在六个步骤之间前进或后退
- **THEN** 应用程序 SHALL 保持相同的向导骨架，并按当前步骤显示符合 Stitch refined 屏幕的主内容和操作层级

### Requirement: Add Game Wizard no-results 状态保持向导骨架
应用程序 SHALL 按 Stitch 提供的 Add Game no-results 状态变体显示未搜索到结果的场景，并保留与正常向导步骤相同的步骤轨、主内容区和底部操作条。

#### Scenario: Add Game Wizard 无结果状态显示
- **WHEN** 用户在选择游戏步骤中没有获得任何匹配结果
- **THEN** 应用程序 SHALL 显示符合 Stitch no-results 变体的诊断信息与恢复动作，同时保持相同的向导骨架和主次操作布局
