# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository reality check

- This repo now contains a real .NET solution: `GSYNC.sln`, production code under `src/`, and xUnit test projects under `tests/`.
- The checked-in desktop app is `src/GSYNC.App`, a **WinUI 3** app targeting `net8.0-windows10.0.19041.0` on **win-x64**.
- Planning docs still describe the original intended stack (for example Avalonia / cross-platform direction). Use those docs for product intent and UX rationale, but use the current `.csproj` files as the source of truth for build/runtime decisions.
- OpenSpec is still active for planning and change management under `openspec/` and `.claude/commands/opsx/`.

## Common commands

### Restore and build
- Restore solution packages:
  - `dotnet restore GSYNC.sln`
- Build everything:
  - `dotnet build GSYNC.sln`
- Build the desktop app explicitly:
  - `dotnet build src/GSYNC.App/GSYNC.App.csproj -p:Platform=x64`
- Run the desktop app:
  - `dotnet run --project src/GSYNC.App/GSYNC.App.csproj -p:Platform=x64`

### Tests
- Run the full test suite without rebuilding:
  - `dotnet test GSYNC.sln --no-build`
- Run a single test project:
  - `dotnet test tests/GSYNC.Core.Tests/GSYNC.Core.Tests.csproj`
  - `dotnet test tests/GSYNC.Manifest.Tests/GSYNC.Manifest.Tests.csproj`
  - `dotnet test tests/GSYNC.PathResolver.Tests/GSYNC.PathResolver.Tests.csproj`
  - `dotnet test tests/GSYNC.Storage.Tests/GSYNC.Storage.Tests.csproj`
- Run one test class:
  - `dotnet test tests/GSYNC.Core.Tests/GSYNC.Core.Tests.csproj --filter "FullyQualifiedName~SyncEngineExecutionTests"`
- Run one test method:
  - `dotnet test tests/GSYNC.Core.Tests/GSYNC.Core.Tests.csproj --filter "FullyQualifiedName~UploadJob_UploadsFiles_AndWritesSyncRecord"`

### Lint / formatting
- There is currently **no separate repo-defined lint or formatting command** checked in (no `.editorconfig`, no dedicated formatter script, no CI workflow in `.github/workflows`).
- Treat `dotnet build` plus the relevant `dotnet test` commands as the verification baseline unless a change adds stricter tooling.

### OpenSpec workflow
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

## Build configuration

- `Directory.Build.props` enables nullable reference types, implicit usings, and C# 12 across the repo.
- `Directory.Packages.props` uses central package management. Shared versions for MVVM, SQLite, Microsoft.Extensions.*, Serilog, YamlDotNet, Windows App SDK, xUnit, and coverlet are declared there.
- The app is Windows-specific today because `GSYNC.App.csproj` targets WinUI / Windows App SDK; the backend projects and tests target plain `net8.0`.

## High-level architecture

### Solution layout
- `src/GSYNC.App` — WinUI 3 desktop shell, XAML pages, view models, UI primitives, app-side infrastructure, and DI composition root.
- `src/GSYNC.Core` — domain models, service abstractions, path/variable resolution, sync queueing, and the sync engine itself.
- `src/GSYNC.Data` — persistence/repository implementations for application state and sync/history data.
- `src/GSYNC.Manifest` — manifest loading/parsing for known games and content definitions.
- `src/GSYNC.Storage` — concrete storage providers such as local-folder and WebDAV implementations.
- `src/GSYNC.Providers` — source-provider integrations and provider-side discovery logic.

### Dependency direction
- `GSYNC.App` is the composition root and references all backend projects directly.
- Cross-project contracts live in `GSYNC.Core.Abstractions` and related core model namespaces.
- Concrete implementations live in `Data`, `Manifest`, `Storage`, and `Providers`, then get wired into the app through DI registration in the app layer.
- Tests are split by backend boundary rather than by end-to-end scenarios: `GSYNC.Core.Tests`, `GSYNC.Manifest.Tests`, `GSYNC.PathResolver.Tests`, and `GSYNC.Storage.Tests`.

### Sync architecture
- Keep synchronization policy inside the core sync pipeline, not inside a storage provider.
- The important architectural split from both the docs and the code is:
  1. source/provider discovery,
  2. game/content definition,
  3. storage read/write implementation,
  4. sync orchestration and history/snapshot behavior.
- In practice, UI actions eventually flow into core abstractions and `GSYNC.Core.Services.Sync.SyncEngine`; storage providers should stay focused on IO capabilities, while conflict handling, records, and snapshot behavior stay in core/data layers.

### UI architecture
- The app follows a page + view model structure by feature area. Current screens/pages line up with the planned product areas: Library, Game Details, Add Game wizard, Conflict Resolution, Sync Targets, Variables, History, and Settings.
- Reusable desktop controls live under `src/GSYNC.App/Primitives/`; view models and page code-behind stay feature-oriented under `ViewModels/` and `Pages/`.
- The current UI work is heavily driven by Stitch mockups. For UI implementation, keep using the refined/normalized Stitch screens and the desktop-utility interaction model from `docs/ui/` rather than redesigning flows ad hoc.

### Cross-layer changes
- Many non-trivial features span more than one project. Typical examples:
  - sync/history work touches `GSYNC.App` view models, `GSYNC.Core` abstractions/services, and `GSYNC.Data` repositories;
  - game discovery or Add Game work often touches app wizard/view model code plus manifest/provider layers;
  - storage-target work usually crosses app settings pages, core abstractions, and concrete storage providers.
- When behavior changes feel “UI-only”, verify whether the corresponding repository, provider, or core service contract also needs updating.

## Key reference docs

- `docs/implementation-handoff.md` — product goals, domain vocabulary, and long-range architectural intent.
- `docs/ui/prototype-inventory.md` — Stitch screen inventory and state coverage.
- `docs/ui/implementation-notes.md` — shared UI patterns, screen mapping, and UX cautions.
- `docs/ui/README.md` and `docs/README.md` — entry points into the planning/design documentation.

## OpenSpec conventions already in the repo

The checked-in experimental OPSX command docs under `.claude/commands/opsx/` establish these expectations:
- `/opsx:explore` is for investigation/thinking only, not application-code implementation.
- `/opsx:apply` implements from artifact context and updates task checkboxes as work completes.
- `/opsx:sync` merges delta specs from a change into `openspec/specs/...`.
- `/opsx:archive` archives completed changes and should prompt instead of guessing when the target change is ambiguous.

## UI reference metadata

If UI work needs Stitch references, the planning docs currently point to:
- Stitch project ID: `13407775155513183369`
- Design system: `assets/3772949807639653402` (`GSYNC Desktop Dark`)
