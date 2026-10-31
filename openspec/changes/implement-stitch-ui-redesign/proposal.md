## Why

The approved Stitch redesign gives GSYNC a clearer visual identity and makes its core workflows easier to scan. The current WinUI screens retain the previous compact rail and table-heavy layout, so they need to be brought into line with the approved designs while preserving the safety work already completed.

## What Changes

- Implement the approved dark slate and teal desktop shell and expanded, labelled navigation, with responsive compact behavior and the existing light theme maintained.
- Rework the library and first-run empty state into the approved game rows, primary actions, search, status filter, and truthful conflict feedback.
- Align add-game setup, target management, game details, and file-level conflict resolution with the approved Stitch layouts while retaining existing flows, validations, and actions.
- Apply compatible shared styling to remaining screens without inventing functionality; the queue design was unavailable from Stitch and must preserve its current working controls.
- Verify the visual result in an isolated data root, representative window sizes, and both languages/themes, and run the existing solution tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `stitch-app-shell`: Apply the newly approved labelled navigation and visual system.
- `collapsible-nav-rail-ui`: Default to readable labelled navigation while retaining a compact option.
- `library-and-details-ui`: Render the redesigned library, empty state, and game detail hierarchy.
- `add-game-wizard-ui`: Present the existing setup workflow in the approved three-phase layout.
- `conflict-resolution-ui`: Emphasize file decisions, overwrite direction, and safe partial application.
- `management-screens-ui`: Align target management and shared management styling with the new shell.

## Impact

- WinUI XAML, shared UI primitives, palette resources, localized text, and presentation-only view-model properties.
- Existing sync engine, persisted data, conflict safety, target checks, and queue behavior remain in place.
