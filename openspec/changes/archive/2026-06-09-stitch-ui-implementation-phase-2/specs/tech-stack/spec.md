## MODIFIED Requirements

### Requirement: UI 框架约束
桌面 UI SHALL 使用 WinUI 3（WinAppSDK）实现，采用 Unpackaged 部署模式，并在涉及界面实现时严格遵循 Stitch refined / normalized 设计稿，不得自行重设计页面信息架构或视觉布局。

#### Scenario: 无沙箱限制
- **WHEN** 应用程序需要读写用户文件系统上的任意游戏存档路径
- **THEN** 应用程序 SHALL 能够访问 `%APPDATA%`、`%DOCUMENTS%`、`%LOCALAPPDATA%` 及任意自定义路径，不受沙箱约束

#### Scenario: Fluent 材质
- **WHEN** 主窗口渲染
- **THEN** 窗口背景 SHALL 应用 Mica 或 Acrylic 材质，颜色主题 SHALL 为深色

#### Scenario: 设计稿约束
- **WHEN** 开发实现已经存在 Stitch 设计稿的界面
- **THEN** 实现 SHALL 以对应 Stitch 设计稿为主要依据，不得擅自改变页面结构、导航关系或状态表现

### Requirement: MVVM 方案约束
应用程序 SHALL 使用 CommunityToolkit.Mvvm 实现 MVVM 架构。ViewModel 中不得直接引用 WinUI 控件类型，并 SHALL 让与界面状态相关的 UI 逻辑优先落在可测试的 ViewModel / UI primitive 层，而不是散落在页面代码后置中。

#### Scenario: ViewModel 独立性
- **WHEN** 单元测试实例化任意 ViewModel
- **THEN** 测试 SHALL 无需启动 WinUI Application 即可运行

#### Scenario: UI 逻辑落点
- **WHEN** 页面需要表达筛选状态、选中状态、工具栏状态或状态变体
- **THEN** 应用程序 SHALL 优先通过 ViewModel 或共享 UI primitive 承载这些逻辑，而不是在页面代码后置中堆叠特例
