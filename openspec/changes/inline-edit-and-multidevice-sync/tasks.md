## 1. Contextual editing

- [x] 1.1 Move sync-target creation and editing into the target detail panel, retaining browsing, validation and credentials.
- [x] 1.2 Move default-target management to Sync Targets and remove the duplicate Settings target area.
- [x] 1.3 Move variable creation and editing into the variable detail panel.
- [x] 1.4 Move game metadata, per-game variables and content creation into the game details page; route Settings game entries there.

## 2. Safe remote automatic pull

- [x] 2.1 Implement full-comparison preconditions for automatic conflict plans and test concurrent changes.
- [x] 2.2 Implement a persistent, configuration-bound baseline and a conservative remote-only pull planner with focused tests.
- [x] 2.3 Add opt-in policy, status, bounded polling, duplicate prevention and application lifecycle integration.
- [x] 2.4 Explain the policy and cross-device identity in the UI; surface review and failure states.

## 3. Validation

- [x] 3.1 Build and run regression tests for editing, identity, first connection, local changes, deletions and concurrent remote writes.
- [x] 3.2 Inspect the editing flows and automatic-pull states with isolated test data and both languages.

## 4. Existing-game destination editing

- [x] 4.1 Add an inline editor for an existing game's target and remote namespace, including the path to create a new target and return.
- [x] 4.2 Preserve binding identity and content selections, prevent pending or stale sync jobs from applying to a changed destination, and invalidate the automatic-pull baseline.
- [x] 4.3 Verify destination changes, first comparison, queued jobs, credentials, namespace validation and the UI with isolated data.
