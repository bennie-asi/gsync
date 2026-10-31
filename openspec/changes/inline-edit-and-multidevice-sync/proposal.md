## Why

Creation and editing are split among inconsistent modal dialogs and duplicated settings navigation. Two computers also need a way to detect remote changes without treating every difference as permission to overwrite a local save.

## What Changes

- Move target, variable, game metadata, and content-item creation or editing into their corresponding detail views while preserving validation and cancel behavior.
- Remove duplicated target navigation from Settings and keep default-target selection on the Sync Targets page.
- Add an opt-in background remote polling and safe automatic pull policy, disabled by default, based on a persisted per-game common baseline; keep uploads manual.
- Require explicit review when a baseline is absent, the local save has changed, files were deleted, or either side changes after comparison. Keep snapshots and verified, atomic writes.
- Explain how the same game and destination are associated across devices and expose useful pending/error states.

## Capabilities

### New Capabilities

- `safe-remote-auto-pull`: Opt-in remote polling and conservative downloads based on a verified shared baseline.

### Modified Capabilities

- `management-screens-ui`: Edit targets and variables in context and consolidate target settings.
- `library-and-details-ui`: Edit game metadata and sync content in the game detail view.
- `sync-engine-behavior`: Reject automatic decisions if the compared local or remote state is no longer current.

## Impact

WinUI views and view models, sync engine comparison and resolution, persisted UI policy and per-game baselines, background task lifecycle, localized status, and regression tests.
