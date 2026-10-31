## Why

GSYNC exposes its main features, but first-run guidance, synchronization direction, status reporting, and recovery paths are difficult to understand and sometimes inconsistent. The desktop interface also needs clearer hierarchy and better behavior at smaller window sizes.

## What Changes

- P0: make synchronization direction explicit, protect uploads consistently, preserve conflict semantics, reconcile target status, and surface actionable queue and recovery feedback.
- P1: guide first-time setup, repair and simplify the add-game flow, keep in-progress configuration when adding a target, and make safe comparison and next actions discoverable.
- P2: improve library, game details, conflict, target, history, and settings hierarchy; make shared controls responsive, accessible, and visually consistent.
- Preserve existing file-level conflict decisions, snapshots, queue cancellation, localization, and provider boundaries.

## Capabilities

### New Capabilities
- `safe-sync-user-experience`: Clear, safe synchronization actions, consistent status reporting, and actionable task and recovery feedback.

### Modified Capabilities
- `library-and-details-ui`: Prioritize actionable states and responsive, readable controls rather than fixed screen layouts.
- `add-game-wizard-ui`: Provide a concise guided setup with resilient manual entry and in-flow target configuration while preserving validation.
- `management-screens-ui`: Show consistent connection status and improve information hierarchy and narrow-window usability.

## Impact

- WinUI pages, shared primitives, view models, localized text, and application services.
- Synchronization and target-status contracts where needed; synchronization policy remains in the core engine.
- App logic and core tests plus isolated manual UI verification. Existing data and bindings remain compatible.
