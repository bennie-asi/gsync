## ADDED Requirements

### Requirement: Safe automatic remote pull is opt-in
The application SHALL default automatic remote polling and pull to off and SHALL allow the user to enable it with a visible explanation of its scope and the need to close the game.

#### Scenario: Default policy
- **WHEN** an existing installation starts without an automatic-pull setting
- **THEN** it SHALL not perform automatic downloads

### Requirement: A verified baseline is required
The application SHALL establish a device-local baseline only after the user explicitly confirms a complete, non-empty comparison in which corresponding local and remote hashes match, and SHALL invalidate it when the relevant game, content or destination configuration changes.

#### Scenario: First connection has existing remote files
- **WHEN** no trusted baseline exists, regardless of whether the two sides match
- **THEN** the application SHALL request manual review and SHALL not download automatically

### Requirement: Automatic pull preserves local changes
The application SHALL automatically pull only remote additions or changes while all local files still match the trusted baseline, and SHALL retain a snapshot and verify the compared state before writing.

#### Scenario: Local and remote both changed
- **WHEN** any local file differs from its baseline while remote files have also changed
- **THEN** no automatic download SHALL be queued and the user SHALL be able to review the difference

#### Scenario: Remote changes after polling
- **WHEN** a remote or local file changes after comparison and before the automatic job starts
- **THEN** the job SHALL stop without applying its decisions

#### Scenario: Remote file removed
- **WHEN** a baseline file is absent from the remote target
- **THEN** the application SHALL not propagate a deletion automatically

### Requirement: Polling follows application lifetime
The automatic-pull service SHALL run at a bounded interval while the application is running, avoid duplicate queued work and stop with the application.

#### Scenario: Policy is disabled
- **WHEN** automatic pull is disabled
- **THEN** the service SHALL not enqueue automatic work
