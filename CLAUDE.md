# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

This repository currently contains planning and design artifacts, not application source code. There are no `.sln`, `.csproj`, `package.json`, `pyproject.toml`, `go.mod`, or similar build manifests in the repo at the moment.

Do not assume the recommended implementation stack from the docs already exists in code.

## Common commands

### OpenSpec workflow
The repo is set up around OpenSpec with `openspec/config.yaml` using the `spec-driven` schema.

- List active changes:
  - `openspec list --json`
- Inspect a change:
  - `openspec status --change "<name>" --json`
- Create a new change:
  - `openspec new change "<name>"`
- Get artifact instructions for a change:
  - `openspec instructions <artifact-id> --change "<name>" --json`
- Get implementation/apply instructions:
  - `openspec instructions apply --change "<name>" --json`

### Build / lint / test
There are no repo-defined build, lint, or test commands yet because the implementation codebase has not been added.

If application code is introduced later, update this file with the actual commands rather than assuming `.NET`, Node, or other tooling from the planning docs.

## Key files

- `docs/implementation-handoff.md`: primary project-level handoff; best single source for product goals, architecture direction, recommended stack, and UX decisions.
- `docs/ui/prototype-inventory.md`: Stitch screen inventory, refined screen IDs, state variants, and implementation priority.
- `docs/ui/implementation-notes.md`: UI shell patterns, shared component candidates, screen-to-feature mapping, and UX cautions.
- `docs/ui/README.md`: entry point for UI prototype docs.
- `openspec/config.yaml`: confirms the OpenSpec `spec-driven` workflow.

## High-level architecture direction

The intended product is a cross-platform desktop application for game save synchronization, with the longer-term goal of being a modular game data synchronization platform.

### Core architectural rule
Keep these concerns separate:
1. Game source provider
2. Game content definition
3. Storage provider
4. Theme / appearance

The sync engine owns synchronization behavior such as conflict handling and snapshots. Storage providers are only responsible for remote/local read-write capabilities; they should not own sync policy.

### Planned modular areas
- Source providers: Steam, Epic, Custom, Emulator
- Storage providers: WebDAV, local folder, future cloud/storage backends
- Content definitions: save data, config, extra files, optional advanced items
- Theme layer: color, density, icon style, appearance tokens

### Domain model direction
The handoff documents organize the future app around these concepts:
- `GameDefinition`: what the game is
- `GameInstance`: the user-managed machine-specific instance
- `ContentItem`: an individual syncable item for a game
- `StorageBinding`: how an instance is bound to a sync target
- Snapshot / SyncRecord / Conflict: operational history, safety, and restore/conflict metadata

### Path and portability model
Path handling is a major part of the design:
- Store logical path templates, not raw OS-specific paths
- Prefer forward-slash-style templates in configuration
- Resolve templates to platform paths at runtime
- Support layered variables with precedence:
  - `system < source < game instance < user override`

Important built-in variables discussed in the docs include `%HOME%`, `%DOCUMENTS%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%USERDATA%`, `%GAME_INSTALL_DIR%`, and `%STEAM_LIBRARY%`.

### MVP sync behavior
The planned MVP is safety-first and explicit:
- prefer manual, explicit sync actions over aggressive automation
- support upload, download, compare/inspect, and manual conflict resolution
- back up before destructive replacement
- keep sync history and snapshot/restore entry points

## UI architecture and implementation guidance

The design direction is a native-feeling desktop utility, not a web dashboard or WebView-style admin shell.

### Preferred UI patterns
- compact left navigation rail
- restrained top title/toolbar area
- split panes and master-detail layouts
- dense tables and property sheets
- explicit wizard shell for Add Game
- focused conflict-resolution dialog
- optional bottom status bar on operational screens

### Important UI constraints
- Prefer the refined / normalized Stitch screens over first-pass screens.
- Reuse the same shell across normal, empty, and error states.
- Do not redesign empty states as landing pages or marketing-style layouts.
- Keep management screens compact and information-dense.
- Keep destructive actions visually separated.

### Shared component direction
The docs consistently point toward a reusable shell and primitives first, especially:
- app navigation rail / title bar / status bar
- dense data grid + property sheet + inspector panel
- wizard shell and step rail
- conflict comparison panel
- path template tester
- normalized status badge semantics

## Recommended implementation stack (planned, not yet present)

The project-level handoff recommends:
- C# on .NET 8
- Avalonia UI
- MVVM
- SQLite
- Serilog
- ZIP for archive/snapshot handling
- system credential storage where possible

Treat this as the intended direction, not as proof that these dependencies already exist in the repo.

## Project workflow conventions already present

The repository includes project-specific OpenSpec command/skill docs under `.claude/commands/opsx/` and `.claude/skills/openspec-*`.

Important expectations from those docs:
- planning is centered on OpenSpec changes and artifacts
- `/opsx:explore` is for investigation only, not implementation
- `/opsx:apply` implements from artifact context and updates task checkboxes as work is completed
- `/opsx:sync` merges delta specs into `openspec/specs/...`
- `/opsx:archive` archives completed changes

## UI reference metadata

If UI work comes up, the docs reference these Stitch assets:
- Stitch project ID: `13407775155513183369`
- Design system: `assets/3772949807639653402` (`GSYNC Desktop Dark`)
