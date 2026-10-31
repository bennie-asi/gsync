## MODIFIED Requirements

### Requirement: 管理页面按 Stitch 落地
Sync Targets SHALL 按新版 Stitch 设计提供目标列表、真实连接状态、最近检查时间、配置详情与测试、编辑、移除操作。Variables、History、Queue 和 Settings SHALL 继续使用一致的共享壳与视觉风格并保留原有功能。

#### Scenario: 目标状态与操作
- **WHEN** 用户打开同步目标并选择一个目标
- **THEN** 页面 SHALL 显示真实状态、检查时间、配置详情以及可用的测试和管理操作

#### Scenario: 未测试目标
- **WHEN** 目标尚未进行连接测试
- **THEN** 页面 SHALL 显示未测试而不是已连接
