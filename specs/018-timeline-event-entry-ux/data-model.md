# Phase 1 Data Model: Timeline Event Entry Experience Enhancements

**Feature**: 018-timeline-event-entry-ux
**Date**: 2026-07-10

This feature is presentation-only. It introduces **no** persisted schema changes, no new database tables or columns, and no contract changes. The existing `Location` (optional text) and `ItemType` values are reused as-is. The "entities" below are transient client-side view-state concepts and derived values that the Blazor components manage during interaction.

## Existing persisted fields reused (no change)

| Field | Source | Use in this feature |
|-------|--------|---------------------|
| `TrackedItem.ItemType` | `TripItemContracts` / `TimelineItem.ItemType` | Selects the type icon shown on the timeline and in the form. Values: `event`, `reservation`, `activity`, `reminder`. |
| `TrackedItem.Location` | `Create/UpdateTrackedItemRequest`, `TrackedItemDto` | Presented as an address; validated as map-capable when entered; used as the map query. Remains optional. |
| `TrackedItem.StartLocal` / `EndLocal` | `TrackedItemDto` | Defaulted from the active date on create; end reacts to start + 1h until manually set. |

## Transient view-state concepts

### Active Timeline Date

- **Represents**: The trip day currently centered in the timeline viewport.
- **Owned by**: `TripTimeline.razor` (`_currentDate`, exposed as `CurrentDate`).
- **Set by**: Debounced scroll reporting (new), and existing jump/step/shortcut actions.
- **Rules**:
  - Derived as the day whose column contains the horizontal center of the visible track.
  - Always clamped to the trip's `[StartDate, EndDate]` range.
  - Falls back to the trip start when no position has been established.
- **Consumed by**: `TripDetails` when opening the Add-event flow; the header "Jump to date: {date}" label and active-date highlight.

### New-Event Date Defaults

- **Represents**: The initial start/end applied to a newly created event.
- **Derived from**: The Active Timeline Date (start) and start + 1 hour (end).
- **Rules**:
  - Applied only on create, never on edit (an existing event keeps its saved dates — FR-005).
  - A lane click carrying an explicit slot start overrides the active-date default with the clicked time.
  - When no active date exists, defaults to a sensible day within the trip range (FR-006).

### Reactive End Adjustment

- **Represents**: The rule that the end follows the start by one hour.
- **Owned by**: `TrackedItemForm` (`_model.StartLocal` change handler; an `_endManuallySet` flag).
- **Rules**:
  - On any change to `StartLocal`, set `EndLocal = StartLocal + 1h` **only if** the end has not been manually edited.
  - Once the traveler edits `EndLocal` directly, `_endManuallySet` becomes true and auto-adjust stops.

### Location Map Target

- **Represents**: The external map destination for an event's location.
- **Derived from**: The `Location` text exactly as entered, URL-encoded into a maps search query.
- **Rules**:
  - Available only when `Location` is present and passes map-capable validation (FR-011/FR-012).
  - Opened in a new browser context with reverse-tabnabbing-safe semantics (FR-014).

### Location Validation State

- **Represents**: Whether an entered location is plausibly map-capable.
- **Rules**:
  - Empty location is valid (optional).
  - Non-empty must be a trimmed value of reasonable length containing at least one letter or digit (not solely whitespace/punctuation).
  - Advisory: surfaced via the form's existing inline validation; does not perform real geocoding.

### Type Icon Mapping

- **Represents**: The association between an `ItemType` and its displayed glyph.
- **Owned by**: `TrackedItemIcon.razor` (new).
- **Rules**:
  - Each known type (`event`, `reservation`, `activity`, `reminder`) maps to a distinct inline-SVG glyph.
  - Unknown or missing type maps to a clear default glyph (FR-010) — no entry is ever iconless.

## Relationships

- **Active Timeline Date → New-Event Date Defaults**: the active date seeds the created event's start (and thereby its +1h end).
- **ItemType → Type Icon Mapping → Timeline entry & Form**: one type resolves to one icon rendered in both places.
- **Location → Location Validation State → Location Map Target**: a valid location enables the globe/map action.

## Non-changes (explicit)

- No new columns, tables, migrations, or SQL.
- No `TripPlanner.Contracts`, `TripPlanner.Api`, or `TripPlanner.Database` changes.
- No new stored geocoordinates; the map uses the existing free-text location.
