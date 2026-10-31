## ADDED Requirements

### Requirement: Automatic decisions verify the full comparison
An automatic pull plan SHALL verify all expected local and remote paths and hashes against the execution context before making changes, in addition to the existing per-file verification and snapshot.

#### Scenario: A different file changed after comparison
- **WHEN** any file in the compared save set changes before the automatic plan executes
- **THEN** the operation SHALL stop before applying any file
