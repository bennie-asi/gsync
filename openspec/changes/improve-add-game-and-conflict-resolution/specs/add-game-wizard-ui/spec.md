## MODIFIED Requirements

### Requirement: Add Game Wizard 六步流程按 Stitch 落地
应用程序 SHALL 按 Stitch refined Add Game Wizard 屏幕实现固定的六步桌面向导流程，包括 Source、Select Game、Content Items、Review Paths、Bind Target 与 Finish 六个步骤。该向导 SHALL 保持左侧步骤轨、右侧当前步骤主内容区与底部主次操作条的稳定结构，而不是在不同步骤间切换为不一致的信息架构。步骤轨表达、步骤标题层级与重复选项卡片 SHALL 在页面内尽量收敛为共享模板或共享原语，避免每一步继续维护各自分叉的结构。向导在 Source、Select Game、Review Paths 和 Finish 步骤中 SHALL 能承载匹配依据、预检结果、阻断错误和手动回退信息，而不破坏既有 Stitch 骨架。

#### Scenario: Add Game Wizard 正常步骤显示
- **WHEN** 用户进入 Add Game Wizard 并在六个步骤之间前进或后退
- **THEN** 应用程序 SHALL 保持相同的向导骨架，并按当前步骤显示符合 Stitch refined 屏幕的主内容和操作层级

#### Scenario: 向导步骤轨与重复卡片结构一致
- **WHEN** 用户浏览多个步骤
- **THEN** 步骤轨与重复出现的选项卡片 SHALL 保持一致的视觉节奏和结构，而不是在同一向导内出现多套近似但不相同的布局

#### Scenario: 向导显示匹配依据与预检反馈
- **WHEN** 用户在选择游戏、检查路径和完成步骤查看当前配置
- **THEN** 应用程序 SHALL 在现有向导骨架中显示匹配依据、路径预检状态和必要的错误/警告反馈，而不是把这些关键信息放到脱离主流程的临时页面中

### Requirement: Add Game Wizard no-results 状态保持向导骨架
应用程序 SHALL 按 Stitch 提供的 Add Game no-results 状态变体显示未搜索到结果的场景，并保留与正常向导步骤相同的步骤轨、主内容区和底部操作条。no-results 状态 SHALL 将诊断信息和恢复动作放在当前步骤上下文内，而不是把用户带离现有向导骨架。系统在未命中可靠内容定义时 SHALL 继续复用该骨架承载“改用手动配置”“返回切换来源”和“继续低置信结果检查”的恢复路径。

#### Scenario: Add Game Wizard 无结果状态显示
- **WHEN** 用户在选择游戏步骤中没有获得任何匹配结果
- **THEN** 应用程序 SHALL 显示符合 Stitch no-results 变体的诊断信息与恢复动作，同时保持相同的向导骨架和主次操作布局

#### Scenario: 无结果状态保留恢复路径
- **WHEN** 向导进入 no-results 状态
- **THEN** 用户 SHALL 能直接在当前步骤中返回上一步、切换到手动配置或继续恢复，而不是失去当前流程上下文
