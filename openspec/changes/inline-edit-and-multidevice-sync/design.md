## Context

Target, variable, content-item and game metadata editors are currently modal and settings duplicates target management. Comparison hashes the two current sides but has no per-device common baseline; a differing hash alone cannot establish which side changed.

## Goals / Non-Goals

**Goals:** Inline draft editing with validation and cancellation; a single target-management location; opt-in, remote-only automatic pull when an earlier verified common baseline proves local content unchanged; clear review/error state and regression coverage.

**Non-Goals:** Automatic upload, deletion propagation, forced overwrites, or assuming a matching game name always identifies the same remote save. The service runs while the app is open; remote providers expose no push subscription in the current abstraction.

## Decisions

- Edit drafts in the selected target, variable, or game detail panel. Save explicitly and keep invalid drafts visible. Keep confirmation dialogs for destructive actions.
- Place the default-target action on the target detail view and remove the duplicate Settings links and selector.
- Poll remotely at a bounded interval only after opt-in. Explicit user confirmation establishes per-game baseline hashes only when a complete, non-empty comparison proves the local and remote sides equal. Bind the baseline to the current profile, destination, content selection and relevant configuration; missing, corrupt or changed configuration requires review.
- Auto-pull only if every local path still matches the baseline and remote files are added or changed; missing remote paths, local additions/deletions or divergent local changes require review. Build explicit remote decisions with expected hashes, preserve the existing snapshot and transactional write path, and verify the entire compared state at execution time.
- Refresh the baseline only from the exact compared versions after a completed atomic pull, then compare again. Never treat queued or failed work as a successful synchronization. Avoid duplicate jobs and stop promptly with the application lifetime.
- Give users a truthful status for disabled, checking, review, failure, and queued/completed states. Explain that the same remote destination and game identity are needed on both computers.
- Existing games edit their selected target and remote namespace inline. Preserve the binding and selections, serialize changes with game operations, reject changes while jobs are queued or active, and bind queued/manual conflict work to the destination it was prepared against. A destination change invalidates the local automatic-pull baseline without touching either remote location.

## Risks / Trade-offs

- [A game can keep state in memory while running] → Tell users to close the game before an automatic pull and keep a snapshot; disk hash checks cannot detect unsaved in-memory changes or reliably determine whether a game is running.
- [Network or application stops mid-pull] → Use existing immutable remote generations, snapshots, atomic staged writes and cancellation-aware task hosting.
- [Configuration or baseline is missing or stale] → Refuse automatic pull and require an explicit comparison; no first-run overwrite.
- [Large collections or slow remote endpoints] → Use bounded polling, one pass at a time, no upload monitoring, and make the interval explicit.
