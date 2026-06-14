## Why

当前 GSYNC 已经具备可运行的基础同步流程，但 Add Game 向导仍主要依赖简单名称匹配和静态路径复查，Conflict Resolution 也仍以整组上传/下载为主，缺少文件级别决策能力。这会让用户在首次建档时更容易生成质量不高的配置，也会在发生分叉时被迫做过于粗粒度的覆盖决定，因此现在需要把这两个高风险入口补强到更接近产品预期的安全级别。

## What Changes

- 改进 Add Game 向导中的清单匹配逻辑，支持更可靠的匹配依据、匹配结果判读和未匹配回退路径，而不是仅靠简单显示名命中。
- 为 Add Game 向导增加更强的路径预检，明确展示每个内容项的解析结果、存在性和风险提示，并在必要时阻止用户创建明显无效的配置。
- 为冲突解决引入文件级决策模型，允许用户在同一组冲突中按文件选择保留本地或远端版本，而不是只能对整组内容执行统一上传或统一下载。
- 扩展同步引擎的冲突应用路径，使其能够安全执行选择性覆盖，并继续在任何本地破坏性写入前保留快照保护。
- 保持现有 Stitch refined UI 外壳和页面结构，不把这些增强退化为脱离既有设计语言的临时对话框或杂乱表单。

## Capabilities

### New Capabilities
- `add-game-onboarding-behavior`: 定义 Add Game 向导中的匹配、路径预检、手动回退与建档约束行为。
- `conflict-resolution-behavior`: 定义文件级冲突决策、批量选择和选择性应用行为。

### Modified Capabilities
- `add-game-wizard-ui`: 更新向导 UI 要求，使其能够承载匹配判读、预检反馈和手动配置增强，而不偏离 Stitch 骨架。
- `conflict-resolution-ui`: 更新冲突页面 UI 要求，使其能表达文件级选择、批量动作和更明确的决策层级。
- `sync-engine-behavior`: 更新同步引擎要求，使快照保护和执行路径覆盖文件级冲突解决场景。

## Impact

- 影响 `src/GSYNC.App` 中的 Add Game Wizard 与 Conflict Resolution 页面、ViewModel 和交互流。
- 影响清单匹配/路径预检相关的应用层逻辑，可能新增专用匹配与预检服务。
- 影响 `src/GSYNC.Core` 中的冲突处理模型与 SyncEngine 选择性应用逻辑。
- 影响用户定义内容定义的落盘时机和生成规则，但不要求引入新的外部依赖。
