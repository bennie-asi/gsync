## ADDED Requirements

### Requirement: 管理项在详情区编辑
应用程序 SHALL 在同步目标及变量的详情区提供新增、编辑、保存和取消，并保留原有字段校验与连接测试。

#### Scenario: 新增同步目标
- **WHEN** 用户选择新增同步目标
- **THEN** 目标详情区 SHALL 显示可编辑表单，而不是新增弹窗

#### Scenario: 默认目标管理
- **WHEN** 用户希望更改默认同步目标
- **THEN** 用户 SHALL 在同步目标页完成操作，设置页 SHALL 不重复展示目标管理入口
