# GSYNC UI/UX Design System

## 1. Visual Tone & Identity
*   **Concept**: Native Desktop Utility (Professional, Dense, Traceable).
*   **Theme**: Dark Mode First.
*   **Background**: `#0B0E17`
*   **Surface/Panels**: `#131929`
*   **Dividers/Outlines**: `#6B7592` (1px subtle separators).

## 2. Color Palette (Semantic)
*   **Primary/Action**: `#5B8CFF` (Accent Blue)
*   **Success/Synced**: Subdued Green (e.g., `#A5D6A7` / `#4CAF50` variants)
*   **Warning/Pending**: `#FFB74D` (Amber)
*   **Conflict/Error/Danger**: `#EE7D77` (Red)
*   **Neutral/Structural**: Muted grays and dark navy.

## 3. Typography
*   **Font Family**: Inter (System sans-serif fallback).
*   **Headline**: Bold, restrained scale.
*   **Body/Label**: Compact, high readability for small sizes.

## 4. Layout & Components
*   **Navigation**: Slim left rail with icon-only or icon+label states.
*   **Structure**: Split-pane (Master-Detail) for complex data (Library, History, Settings).
*   **Corner Roundness**: 8px (`ROUND_EIGHT`).
*   **Spacing**: 2x (Compact/Dense layout).
*   **Buttons**: Right-aligned in toolbars/footers. Primary action always most prominent.

## 5. Screen & State Definitions
*   **Refined Set**: The definitive reference for implementation (IDs tracked in `prototype-inventory.md`).
*   **Mandatory States**:
    *   **Empty State**: Centered illustration + CTA.
    *   **Error State**: Inline or full-page banner with diagnostic info.
    *   **Sync In-Progress**: Subtle animation or progress indicators in list rows.
    *   **Confirmation Dialogs**: Overlay modals for destructive actions (Delete, Overwrite, Restore).

## 6. Interaction Guidelines
*   **Confirmation**: Required for any action that could result in data loss.
*   **Traceability**: Every sync action should be logged and reviewable in History.
*   **Safety**: Explicit local/remote comparison view for conflict resolution.
