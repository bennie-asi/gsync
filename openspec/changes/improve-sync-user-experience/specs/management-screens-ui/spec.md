## ADDED Requirements

### Requirement: Management views remain usable and consistent
Target, history, and settings views SHALL use consistent status and operation hierarchy, offer actionable empty and error states, and keep primary content usable at narrow widths.

#### Scenario: Target test result changes
- **WHEN** a target test succeeds or fails
- **THEN** the management view and shell SHALL reflect the same result and check time.

#### Scenario: Narrow management window
- **WHEN** a management page cannot fit its normal panes
- **THEN** its essential controls and selected details SHALL remain accessible.
