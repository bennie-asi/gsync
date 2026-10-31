## MODIFIED Requirements

### Requirement: Add Game Wizard 六步流程按 Stitch 落地
The wizard SHALL guide the user through choosing a game and save paths, choosing a target, and reviewing the configuration. It SHALL preserve the existing validation and allow users to return without losing their entries.

#### Scenario: Add Game Wizard 正常步骤显示
- **WHEN** 用户在添加游戏流程中前进或后退
- **THEN** 页面 SHALL 显示当前阶段和下一步，并保留已填写的数据。

#### Scenario: 向导步骤轨与重复卡片结构一致
- **WHEN** 用户浏览多个步骤
- **THEN** 选项和步骤状态 SHALL 具有一致且可通过键盘使用的交互。

## ADDED Requirements

### Requirement: Manual setup and target recovery
Manual entry SHALL remain editable after validation errors or cancellation, and target selection SHALL offer an in-flow path to configure a target without discarding the wizard state.

#### Scenario: Manual entry cancelled
- **WHEN** 用户取消手动录入
- **THEN** 向导 SHALL 留在可重新录入的阶段，不得前进到空白游戏列表。

#### Scenario: Target needed during setup
- **WHEN** 用户需要新建或测试同步目标
- **THEN** 用户 SHALL 能配置目标并返回当前向导，保留已填写内容。
