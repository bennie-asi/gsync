## ADDED Requirements

### Requirement: Safe and explicit synchronization
User-facing synchronization actions SHALL identify their direction, protect remote changes before normal uploads, and present divergent files for explicit decisions instead of choosing by timestamp alone.

#### Scenario: Remote content differs
- **WHEN** a normal upload encounters differing or remote-only content
- **THEN** the application SHALL avoid overwriting it and direct the user to compare and resolve differences.

#### Scenario: Comparing conflicting content
- **WHEN** both versions of a file differ
- **THEN** the status SHALL remain a conflict regardless of modification times.

### Requirement: Consistent target and task feedback
The application SHALL distinguish untested, online, and failed target checks consistently across the shell and target page and SHALL distinguish a queued job from a completed job.

#### Scenario: A connection test completes
- **WHEN** the selected target is tested
- **THEN** the target page and shell SHALL show the same most recent result.

#### Scenario: A synchronization job is submitted
- **WHEN** a job is queued successfully
- **THEN** feedback SHALL identify it as queued and provide access to its progress and eventual history.

### Requirement: Understandable restore confirmation
Before restoring a snapshot, the application SHALL identify the game and snapshot and explain that local files will be replaced and backed up.

#### Scenario: A user requests restore
- **WHEN** a snapshot is selected for restoration
- **THEN** a confirmation SHALL identify the snapshot and its consequences before modifying local saves.
