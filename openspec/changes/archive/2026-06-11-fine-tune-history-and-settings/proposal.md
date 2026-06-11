## Why

在完成高优先级三页细调后，History 与 Settings 成为下一批最自然的 Stitch 精修目标。它们已经具备基本结构，但仍然保留明显的页面级模板、硬编码标题/按钮分区，以及与 refined 稿相比还不够稳定的空态、预览区和分组层级表达；现在继续收紧这两页，可以把剩余管理页也推进到与主壳层同样稳定的 refined 质量。

## What Changes

- 继续细调 History 页面，使筛选区、主表格、右侧详情/快照区和 empty state 更贴近 Stitch refined History 稿。
- 继续细调 Settings 页面，使左侧分组导航、中心设置区和右侧辅助检查/预览区的层级更贴近 Stitch refined Settings 稿。
- 收敛这两页中仍然残留的页面内联模板和硬编码文案，优先复用 shared primitives 与已有 view model 文案属性。
- 强化这两页在管理页体系中的一致性，避免它们继续成为“骨架对了，但细节还是页面专属写法”的例外。

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `management-screens-ui`: 细化 History 页面在筛选区、详情/快照区和 empty state 上的 refined 要求，并细化 Settings 页面在分组导航、中心设置区和右侧辅助区上的 refined 要求。
- `stitch-app-shell`: 强化 History 与 Settings 在后续细调中对统一壳层语言和共享原语复用的约束，减少页面级模板分叉。

## Impact

- 影响 `src/GSYNC.App/Pages/HistoryPage.*`、`SettingsPage.*` 及对应 ViewModel 的布局与展示模型。
- 可能影响 `src/GSYNC.App/Primitives/SnapshotFeed.*`、`InspectorPanel.*`、`PropertySheet.*`、`InfoCallout.*` 的复用方式或轻量扩展。
- 影响主 specs 中 `management-screens-ui` 与 `stitch-app-shell` 的 refined 细调要求，需要补充这两页的“下一批细调”约束。
- 后续验证将以 Stitch refined History `339ca09813484c6db566decddc0cd266`、History empty state `867633a947be457c9d8e6a80fcc4198e`、Settings `87f707956f504208bdf6a7ab4dc54620` 为主要基准。