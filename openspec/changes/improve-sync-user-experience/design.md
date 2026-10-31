## Context

The WinUI application has a serial queue, per-file conflict resolution, snapshots, and storage providers. Several UI paths label uploads as generic sync, use different upload protection, or present status separately. The existing wizard and fixed-width panes make initial setup and small windows harder to use.

## Goals / Non-Goals

**Goals:** Implement P0 safety and status first, P1 onboarding and navigation second, then P2 visual hierarchy, responsive layout, and accessibility. Preserve user profiles, existing sync data, and localization.

**Non-Goals:** New cloud providers, automatic destructive bidirectional merging, or a new desktop framework.

## Decisions

- Treat differing content as a conflict; timestamps are contextual information, not proof of a safe direction. Normal uploads request remote-change protection. Explicit per-file conflict decisions remain the mechanism for intentional replacement.
- Distinguish queue submission from a terminal result. Link users to the queue while jobs run and to history after completion.
- Reuse the same target checks and publish the result to shell and management views. Unknown or stale status is not described as a confirmed success.
- Preserve validation while grouping setup into user-facing phases; retain state across target creation and provide local error recovery.
- Evolve existing WinUI primitives and themes rather than introducing another UI toolkit. Show essential actions at narrow widths and expose keyboard-accessible selection semantics.

## Risks / Trade-offs

- [More conservative uploads can require conflict resolution] → Explain the reason and give a direct comparison entry point.
- [Changing layout can break localization and small-window behavior] → Verify Chinese and English, scaling, empty/error states, and representative widths.
- [Cross-device state changes between comparison and execution] → Preserve conditional commits and compared-version checks.

## Migration Plan

Changes are backward compatible with saved profiles, targets, and snapshots; no migration is planned. Existing queue and file safety behavior remains in place.
