# GSYNC UI Implementation Notes

## Prototype direction summary
The UI direction chosen in Stitch is a **cross-platform desktop utility shell** rather than a web-style admin dashboard. This should influence implementation decisions in Avalonia or any alternative desktop UI framework.

## Recommended shell structure

### Global app shell
- Left navigation rail (compact width)
- Top title/toolbar region
- Main content region using split panes where appropriate
- Optional bottom status bar on Library and runtime-heavy screens

### Common layout patterns
1. **Dashboard/Table pattern**
   - Used by Library and History
   - Dense rows, sortable columns, status badges, toolbar filters

2. **Master-detail split pane**
   - Used by Game Details, Sync Targets, Variables
   - Left list/table + right property panel

3. **Wizard shell**
   - Used by Add Game flow
   - Left step progress list + main step content + bottom action bar

4. **Modal conflict resolver**
   - Used by conflict flow
   - Focused safety dialog with side-by-side comparisons

## Shared component candidates

### Navigation and shell
- `AppNavRail`
- `AppTitleBar`
- `StatusBar`
- `SectionHeader`

### Data presentation
- `DenseDataGrid`
- `PropertySheet`
- `InspectorPanel`
- `Badge`
- `InfoCallout`
- `ToolbarFilterRow`

### Workflow components
- `WizardShell`
- `WizardStepRail`
- `WizardFooterBar`
- `ConflictComparisonPanel`
- `PathTemplateTester`

## Badge semantics
Normalize these across the app:
- Synced / Success
- Local newer
- Remote newer
- Conflict
- Pending
- Missing
- Disabled
- Ready

## Suggested implementation order
1. App shell and shared components
2. Refined Library screen + first-run empty state
3. Refined Game Details screen
4. Refined Add Game wizard shell + steps, including no-results state
5. Refined Conflict Resolution dialog
6. Refined Sync Targets page + connection failure state
7. Refined Variables page + parse error state
8. Refined History page + empty state
9. Refined Settings page

## Screen-to-feature mapping

### Library
Supports:
- Multi-game overview
- Quick sync actions
- Search/filtering
- Status visibility

### Game Details
Supports:
- Per-game content management
- Path and content item inspection
- Sync history preview
- Contextual actions

### Wizard
Supports:
- Guided onboarding of a game profile
- Progressive disclosure of complexity
- Source → Game → Content → Paths → Target → Finish flow

### Sync Targets
Supports:
- WebDAV and future providers with shared property editor model

### Variables
Supports:
- Cross-device path portability via variable resolution and testing

### History
Supports:
- Auditability
- Snapshot restore entry points
- Sync diagnostics

### Settings
Supports:
- Appearance customization
- Runtime behavior toggles
- Logging and retention options

## UX cautions
- Do not turn dense management pages into oversized cards.
- Avoid hiding critical sync state inside secondary dialogs.
- Keep destructive actions visually separate.
- Do not overload the wizard with too many advanced concepts in one step.
- Preserve the desktop-tool identity through spacing, borders, and compact layout.
- Empty states should still preserve the surrounding desktop shell so the application never feels like a marketing page.
- Failure states should be diagnostic and actionable, not noisy or theatrical.

## State-variant guidance
Use dedicated state variants during implementation rather than improvising ad hoc placeholders.

### Covered variants
- Library first-run / empty state
- Sync Targets connection failure
- Variables template parse error
- Add Game no-results state
- History empty state

### Implementation guidance
- Keep the surrounding shell identical between normal and variant states.
- Reuse the same pane structure and status semantics.
- Only the focal panel or affected section should change for most failure/empty cases.
- Preserve primary recovery actions close to the relevant error message or empty panel.

## Table interaction standard
- Table headers and table body content should default to **left alignment** unless the Stitch design explicitly shows a different alignment.
- Text that exceeds the visible cell width should truncate with an ellipsis rather than wrap unpredictably.
- Truncated table text should expose the full value on hover via a tooltip.
- Table regions and adjacent split panes should support drag-based width adjustment where practical in the desktop implementation.
- Any UI-related deviation from the approved Stitch mockups requires explicit user approval before implementation.

## Suggested future prototype work
- Drag-adjustable split panes
- Snapshot restore confirmation flow
- Multi-select bulk actions in Library
- Additional diagnostic states for path test warnings vs hard failures
- Sync-in-progress row and task progress states
