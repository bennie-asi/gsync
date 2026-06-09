# GSYNC Implementation Handoff

> This document summarizes the key product, architecture, UI, and implementation decisions established during early planning and prototype work. It is intended to let future implementation work start directly from an agreed baseline.

## 1. Project context

- **Project path:** `C:\dev\projects\other\gsync`
- **Product name:** GSYNC
- **Product type:** Cross-platform desktop application for game save synchronization
- **Target platforms:** Windows / macOS / Linux
- **Primary UX direction:** Native-feeling desktop utility, not a web admin dashboard and not a WebView-style app shell

## 2. Product goal

GSYNC is intended to help users synchronize game-related data across devices.

The product is broader than a simple WebDAV uploader. The long-term product direction is:

> A modular game data synchronization platform

The platform should support:
- Multiple games
- Multiple sync targets
- Multiple game sources
- Multiple kinds of syncable content
- Portable path templates across devices and operating systems
- Safe conflict handling and backup/restore workflows

## 3. Agreed product scope

### Core user-facing capabilities
- Manage multiple games
- Detect or define multiple content items per game
- Synchronize game data between devices
- Bind a game to a sync target
- Resolve conflicts safely
- Review sync history and snapshots
- Configure path variables and sync targets
- Use a desktop GUI with compact, utility-first interaction patterns

### Content that may be synchronized
A game should not be modeled as a single folder only. A game may include multiple **content items**, for example:
- Main save
- Character slots / multiple save slots
- User config
- Device-specific config
- Extra companion files
- Emulator-related files
- Screenshots or optional media

### Content that should usually be excluded or not default-enabled
- Temporary files
- Cache
- Shader cache
- Crash logs / dumps
- Device-only graphics settings by default

## 4. Architecture direction

The agreed architecture principle is **strong module separation**.

Do not mix these concerns:
1. **Game source** — where a game comes from
2. **Game content definition** — what should be synchronized
3. **Storage provider** — where the synchronized data is stored
4. **Theme / appearance** — how the app looks

### Recommended modular areas
- **Source providers**
  - Steam
  - Epic
  - Custom
  - Emulator
- **Storage providers**
  - WebDAV
  - Local folder
  - Future: OneDrive / Google Drive / SFTP / NAS
- **Content / recipe definitions**
  - Save data
  - Config
  - Extra files
  - Optional advanced items
- **Theme layer**
  - Color, density, icon style, appearance tokens

### Important architectural rule
Synchronization behavior should be handled by the **core sync engine**, not by a storage provider.

That means:
- WebDAV does not decide conflict behavior
- Google Drive does not decide snapshot behavior
- Storage providers are primarily responsible for remote read/write capabilities

## 5. Recommended implementation stack

The implementation recommendation reached during discussion was:

- **Language:** C#
- **Runtime:** .NET 8
- **Desktop UI:** Avalonia UI
- **Architecture:** MVVM
- **Persistence:** SQLite
- **Logging:** Serilog
- **Archive / snapshot:** ZIP
- **Secret storage:** System credential store when possible

### Why this direction
- Fast iteration speed
- Cross-platform desktop support
- Good AI-assisted implementation ergonomics
- Not WebView based
- Suitable for a utility-style desktop application

## 6. Domain model direction

The following conceptual model was established.

### GameDefinition
Represents what the game is.

Suggested fields:
- `gameId`
- `displayName`
- `aliases`
- `sourceHints`
- `recipeRefs`

### GameInstance
Represents the user-managed instance on a specific machine or context.

Suggested fields:
- `instanceId`
- `gameId`
- `sourceId`
- `installPath`
- `enabledContentItems`
- `storageBinding`
- `variableOverrides`

### ContentItem
Represents a syncable item belonging to a game.

Suggested fields:
- `contentId`
- `category`
- `name`
- `entryType`
- `pathTemplates`
- `include`
- `exclude`
- `portability`
- `conflictPolicy`
- `slotMode`
- `defaultEnabled`

### StorageBinding
Represents a binding between a game instance and a sync target.

Suggested fields:
- `providerId`
- `providerConfigId`
- `remoteNamespace`
- `syncPolicy`

### Snapshot / SyncRecord / Conflict
These should capture:
- sync direction
- result
- timestamps
- source device
- remote target
- affected files count
- snapshot identifiers
- conflict metadata

## 7. Path and variable strategy

This area is critical to the project.

### Key rules
1. **Store logical paths, not raw OS-specific paths**
2. Use forward-slash style in configuration templates where possible
3. Resolve to real platform paths at runtime
4. Support variable layers and override precedence

### Expected built-in variables
- `%HOME%`
- `%DOCUMENTS%`
- `%APPDATA%`
- `%LOCALAPPDATA%`
- `%USERDATA%`
- `%GAME_INSTALL_DIR%`
- `%STEAM_LIBRARY%`

### Variable categories
- System variables
- Source-provided variables
- Game-instance variables
- User-defined variables

### Variable precedence
Agreed resolution order:
- system < source < game instance < user override

### Required UX support
- Variable management page
- Path template tester
- Variable resolution explanation
- Error handling for invalid templates / parse failures

## 8. Sync product behavior

### MVP sync model
The first implementation should prefer **manual and explicit synchronization** over aggressive automation.

Recommended first-pass actions:
- Upload local to remote
- Download remote to local
- Compare / inspect state
- Resolve conflicts manually
- Backup before destructive replacement

### Conflict handling principles
Conflict resolution must be safety-first.

Suggested actions:
- Use local and upload
- Use remote and download
- Backup local then restore remote
- Keep both copies
- Cancel

### Snapshot and history expectations
- Record sync operations
- Keep snapshots/restoration entry points
- Let users inspect history and restore older states

## 9. UX direction and design system decisions

### Chosen design direction
The project has converged on a **desktop utility** style with:
- split panes
- compact tables
- property sheets
- restrained cards
- explicit status bars and toolbars
- high information density where appropriate

### Explicitly avoided
- web admin dashboard look
- oversized SaaS cards
- landing-page empty states
- flashy or theatrical failure states

### Design language keywords
- native-feeling
- compact
- utility-first
- diagnostic
- safe
- professional

## 10. Stitch project and design system references

### Stitch project
- **Project name:** GSYNC
- **Project ID:** `13407775155513183369`

### Design system
- **Name:** GSYNC Desktop Dark
- **Asset ID:** `assets/3772949807639653402`

### Important note
The preferred implementation references are now the **refined / normalized** screens, not the earliest first-pass screens.

For detailed screen inventory and IDs, see:
- `docs/ui/prototype-inventory.md`
- `docs/ui/implementation-notes.md`
- `docs/ui/README.md`

## 11. Prototype status summary

### Prototype coverage completed
The following areas have been prototyped in Stitch:
- Library / dashboard
- Game details
- Conflict resolution
- Add Game wizard (all 6 steps)
- Sync Targets management
- Variables management
- History & snapshots
- Settings & appearance

### Refined variants completed
A unified desktop-utility pass was completed across the major screens.

### Normalized variants completed
A stricter normalization pass was completed for key app-shell screens to unify:
- canvas size
- left navigation rail width
- icon and label treatment
- title bar height
- content margins
- split-pane spacing

### State variants completed
The following key states were also designed:
- Library empty / first-run
- Sync Targets failure state
- Variables parse error state
- Add Game no-results state
- History empty state

## 12. Current preferred design references

When implementing, prefer this hierarchy:

### 1. Normalized screens
Use these as the primary reference for:
- global shell
- navigation rail
- title bar
- content spacing
- main pane layout

### 2. Refined screens
Use these as the reference for:
- interaction density
- property sheet style
- wizard structure
- conflict layout

### 3. State variants
Use these as the reference for:
- empty states
- no-results states
- parse/error/connection failures

## 13. Implementation order recommendation

### Phase 1 — shell and shared UI primitives
Build the reusable shell first.

Suggested shared components:
- `AppNavRail`
- `AppTitleBar`
- `StatusBar`
- `DenseDataGrid`
- `PropertySheet`
- `InspectorPanel`
- `ToolbarFilterRow`
- `WizardShell`
- `WizardStepRail`
- `WizardFooterBar`
- `ConflictComparisonPanel`
- `PathTemplateTester`
- `Badge`
- `InfoCallout`

### Phase 2 — main screens
1. Library
2. Game Details
3. Add Game wizard
4. Conflict Resolution
5. Sync Targets
6. Variables
7. History
8. Settings

### Phase 3 — state variants
Implement the already-modeled empty/error states using the same shell components.

### Phase 4 — behavior and engine wiring
- provider integration
- path resolution
- history persistence
- snapshot workflows
- sync engine integration

## 14. Immediate engineering guidance

### Important implementation rules
- Keep the shell consistent between pages
- Do not redesign empty pages as standalone marketing layouts
- Reuse the same pane structure for state variants whenever possible
- Keep destructive actions visually separated
- Preserve compact utility density for management screens
- Keep wizard steps explicit rather than collapsing too much into one page

### Good first coding milestone
A strong first milestone would be:
- app shell
- normalized navigation rail
- normalized title bar
- Library page
- Game Details page
- placeholder state routing between normal / empty / error states

## 15. Remaining useful follow-up work

Not required before implementation starts, but useful later:
- snapshot restore confirmation flow
- delete target confirmation dialog
- sync-in-progress row/state
- draggable split panes
- path warning vs hard failure distinction
- additional logs views

## 16. Files created during planning

This planning work already produced local documentation under:
- `docs/ui/README.md`
- `docs/ui/prototype-inventory.md`
- `docs/ui/implementation-notes.md`

This file adds the broader project-level handoff and decision summary.
