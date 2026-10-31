## Context

The user approved Stitch project `14294389959264589495`: library `1419615ab34341c781b4672866fd1839`, empty `c2a314c01b5444d3997a8e7702e8f41c`, onboarding `a3276b39749b414ca5871134a32f9e43`, targets `1ac5e8cedc444c6f9f8a115ee2797a7d`, details `f5df3146f34b4bcd93ec414136d721a7`, and conflict `1fcfa971878a462b99a6a52b62e15f52`. These are visual references; sample game data and credentials in the mockups are not application data.

The previous change already introduced protected uploads, comparisons, explicit conflict decisions, shared target checks, and recoverable onboarding. These behaviors must remain intact.

## Goals / Non-Goals

**Goals:** Translate the approved screens into native WinUI, retain accessibility and localization, keep actions available at narrower window sizes, and preserve existing behavior and data.

**Non-Goals:** Import the generated HTML, add cloud providers, invent pause/resume, fabricate progress or connection states, or redesign unprovided queue/history/settings flows.

## Decisions

- Reuse WinUI brushes, components and bindings rather than embedding a web page. Recolor the existing dynamic palette for dark and light themes, and keep status semantics and focus targets readable.
- Use a labelled left navigation at normal widths, with the existing compact mode available and automatic reduction when space is constrained. Global state comes from existing view models, never mock data.
- Render the game library as accessible native game rows with open and compare actions, using existing filtered models. Show a conflict callout only for actual conflict results; do not infer direction from timestamps.
- Keep the wizard's real internal stages and recovery paths while visually grouping them into the three approved phases. Preserve manual input and target configuration state.
- Place per-file conflict actions and an explicit partial-decision summary above the fold. Existing overwrite confirmation and snapshot semantics are authoritative where mockups are ambiguous.
- Keep existing queue, history, variables and settings workflows and adapt their shared shell/theme. Do not invent a queue design that Stitch did not provide.

## Risks / Trade-offs

- [Desktop size and localization can alter the reference proportions] → Verify at normal and narrow widths, in Chinese and English, and maintain scroll/focus behavior.
- [Mockups contain illustrative states] → Bind every displayed value to existing application state and test empty, failure and conflict cases.
- [Visual changes may accidentally alter safety flow] → Keep sync/restore services untouched and exercise comparisons, confirmation, and onboarding in isolated data.
