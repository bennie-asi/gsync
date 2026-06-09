# GSYNC UI Prototype Inventory

## Overview
This document tracks the Stitch prototype set for GSYNC, a cross-platform desktop game save synchronization tool. The prototype emphasizes a native desktop utility feel: split panes, property sheets, compact tables, restrained cards, and dense information hierarchy.

Design system in Stitch:
- **Name:** GSYNC Desktop Dark
- **Asset ID:** `assets/3772949807639653402`
- **Documentation:** `docs/ui/DESIGN_SYSTEM.md`

Stitch project:
- **Project:** GSYNC
- **Project ID:** `13407775155513183369`

---

## Information Architecture

### Primary navigation
- Library
- Sync Targets
- Variables
- History
- Settings

### Core flows
1. Browse and assess game sync status
2. Open a game and inspect synchronized content items
3. Resolve conflicts safely
4. Add a new game through a guided wizard
5. Manage sync targets and path variables
6. Review history and restore snapshots
7. Adjust appearance and application settings

---

## Screen Inventory

### Core screens
| Screen | Purpose | Screen ID |
|---|---|---|
| GSYNC Library | Main multi-game dashboard | `38975d6ce721492494aed43ff2cb1ed3` |
| GSYNC - Elden Ring Details | Per-game details and content items | `68c6a0f917de4effb086c0a235535604` |
| GSYNC - Conflict Resolution | Local/remote conflict resolution dialog | `413d76ade5e4478ab0e5fa2ba548fe9b` |

### Add Game wizard
| Step | Screen | Purpose | Screen ID |
|---|---|---|---|
| 1 | Add Game - Select Source | Choose Steam / Epic / Custom / Emulator / Manual | `385e47e6ff314b1a97e0179ca7b8c013` |
| 2 | Add Game - Select Game | Choose detected game from source | `5ceb9bb9f1074439a964316625bfcec5` |
| 3 | Add Game - Choose Content Items | Select save/config/extra items to sync | `22792668496743398fa8417b80dd601f` |
| 4 | Add Game - Review Paths | Validate path templates and resolved paths | `dc93029be03e4b0fa1afa552887dded4` |
| 5 | Add Game - Bind Sync Target | Bind WebDAV/local/cloud target | `b151d11b1f9142edb29f5d918dcca1e5` |
| 6 | Add Game - Finish | Final review and profile creation | `740cf785e2864979bbfed767affd4699` |

### Management screens
| Screen | Purpose | Screen ID |
|---|---|---|
| GSYNC - Sync Targets Management | Manage WebDAV/local/preview targets | `40024de095bb47d5978d50cb4c2c2f12` |
| GSYNC - Variables Management | Manage path variables and template testing | `01b3e241a7cf4b43b4efeb5a692e2b9b` |
| GSYNC - History & Snapshots | Review sync log and restore snapshots | `127dea50886e43d9b2bd539d4bff8bd1` |
| GSYNC Settings - Appearance & General | Appearance and global behavior | `5eb0814d5a834d9da738870bc2e9a465` |

### Interaction & Confirmation Dialogs
| Screen | Purpose | Screen ID |
|---|---|---|
| Delete Sync Target Confirmation | Danger modal for target removal | `2c104aea8c2644faa515fb7671db2bbd` |
| Restore Snapshot Confirmation | Danger modal for overwriting local files | `Pending` |

---

## Refined desktop-utility variants
These are the unified variants produced after visual/system consolidation. They should be treated as the preferred direction when implementing the real desktop UI.

### Refined core screens
| Screen | Refined title | Screen ID |
|---|---|---|
| Library | GSYNC Library - Desktop Refined | `a1b9236eff004d55b51d032e23304614` |
| Game Details | GSYNC - Elden Ring Details - Desktop Refined | `0a1af726e04f4b56900569eff34aa148` |
| Sync Targets | GSYNC - Sync Targets - Desktop Refined | `ee786c958cf24700b1ddb593a37697d2` |
| Settings | GSYNC Settings - Desktop Refined | `87f707956f504208bdf6a7ab4dc54620` |

### Refined remaining screens
| Screen | Refined title | Screen ID |
|---|---|---|
| History | GSYNC - History - Refined | `339ca09813484c6db566decddc0cd266` |
| Variables | GSYNC - Variables - Refined | `3023eb712c434b62a287bad3b44b6d89` |
| Conflict Resolution | GSYNC - Conflict Resolution - Refined | `2c5485592b864d9caa78302c82f60264` |
| Wizard Step 1 | GSYNC Wizard - Source - Refined | `db842d2107e44a1484348389ca3512d3` |
| Wizard Step 2 | GSYNC Wizard - Select Game - Refined | `04dc632fde7543a98f9fa1c663f9a262` |
| Wizard Step 3 | GSYNC Wizard - Content Items - Refined | `db204db7e9e84b40a880d9bf7c7c5bdc` |
| Wizard Step 4 | GSYNC Wizard - Review Paths - Refined | `285f61ae184a42b09908b5993237f34a` |
| Wizard Step 5 | GSYNC Wizard - Bind Target - Refined | `b09bb4b87fb8452c9871b0721011137f` |
| Wizard Step 6 | GSYNC Wizard - Finish - Refined | `1cf61b86a57e4467b274f9b0396f5acb` |

### Refined state variants
| State | Refined title | Screen ID |
|---|---|---|
| Library empty / first-run | GSYNC Library - Refined Empty State | `1aae122d7e484f85bdb8893aa6b63984` |
| Library sync in-progress | GSYNC Library - Sync In-Progress State | `2c82c39e32dc48df9595daa4e4861d2d` |
| Sync Targets failure | GSYNC - Sync Target Failure State | `99e89eaae486405b962d54f3e258a17f` |
| Variables parse error | GSYNC - Variables Error State | `b4ce8536544543dbabf87e867774b6fb` |
| Add Game no-results | Add Game - No Results State | `7f439bbb643e4f5a8e0600a7133e8304` |
| History empty | GSYNC History - Refined Empty State | `867633a947be457c9d8e6a80fcc4198e` |

---

## Visual system decisions
... (Visual decisions)

---

## Recommended implementation priority
... (Priority)

---

## State variant coverage
... (Coverage)

---

## Notes for engineering handoff
- Prefer the refined screens over the first-pass screens for implementation.
- The wizard is intentionally split into six explicit steps to reduce cognitive load and preserve traceability of setup decisions.
- Variable and path review screens are critical because they reflect the platform portability model of the product.
- Conflict resolution should remain safety-first: clear comparison, explicit actions, backup-first option, and conservative default emphasis.
- Sync Targets and Variables are effectively "power-user" screens, so high information density is acceptable there.
