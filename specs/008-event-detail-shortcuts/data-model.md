# Data Model: Event Detail Fields and Quick-Fill Shortcuts

## TrackedItem

Represents a trip event, activity, reservation, or reminder related to exactly one trip leg.

**Fields relevant to this feature**:

- `TrackedItemId`: unique tracked item identifier.
- `TripId`: parent trip identifier.
- `TripLegId`: selected leg relationship.
- `OwnerUserId`: authenticated owner copied for owner-scoped queries.
- `ItemType`: one of `event`, `reservation`, `activity`, or `reminder`.
- `Title`: required event label.
- `Location`: optional location text.
- `StartLocal`: required local event start date/time shown in the modal.
- `StartTimeZoneId`: required timezone ID for `StartLocal`.
- `StartsAt`: required derived instant used for ordering and timeline projection.
- `EndLocal`: optional local event end date/time shown in the modal.
- `EndTimeZoneId`: required when `EndLocal` is supplied; empty when the event has no end.
- `EndsAt`: optional derived instant used for ordering and timeline projection.
- `DisplayColor`: selected event color used for timeline display.
- `ConfirmationCode`: optional Confirmation/Reservation Code text, maximum 255 characters.
- `Notes`: optional longer free-text notes, maximum 2,000 characters.
- `SortOrder`: stable fallback ordering when event times tie.

**Relationships**:

- Belongs to one `Trip`.
- Belongs to exactly one `TripLeg` for all new and updated items.
- Appears as a timeline item under its related leg.

**Validation rules**:

- `Title`, `TripLegId`, `StartLocal`, and `StartTimeZoneId` are required.
- `EndTimeZoneId` is required only when `EndLocal` is supplied.
- `StartTimeZoneId` and `EndTimeZoneId`, when supplied, must be valid supported timezone IDs.
- `StartsAt` is derived from `StartLocal` plus `StartTimeZoneId`; callers do not choose it independently.
- `EndsAt` is derived from `EndLocal` plus `EndTimeZoneId`; callers do not choose it independently.
- `EndsAt`, when supplied, must be on or after `StartsAt`.
- `ConfirmationCode` must be 255 characters or fewer.
- `Notes` must be 2,000 characters or fewer.
- The selected `TripLegId` must belong to the same trip and owner as the tracked item.

## TripLeg

Represents the dated segment that an event belongs to and the source for copy-from-leg values.

**Fields used by copy-from-leg**:

- `TripLegId`: source leg identifier.
- `StartLocal`: copied into the event's `StartLocal` when requested.
- `StartTimeZoneId`: copied into the event's `StartTimeZoneId` when start is copied.
- `EndLocal`: copied into the event's `EndLocal` when requested.
- `EndTimeZoneId`: copied into the event's `EndTimeZoneId` when end is copied.

**Validation rules**:

- Copy is available only after an event has a selected trip leg.
- Copy does not change the persisted event until the traveler saves.
- Copy must not silently overwrite manually entered start or end values.

## EventDetailsModal

Represents the traveler-facing create/edit form state for a tracked item.

**Field order relevant to this feature**:

1. Existing event identity and categorization fields.
2. Start date/time with start timezone dropdown and a copy-from-leg action.
3. End date/time with end timezone dropdown and a copy-from-leg action.
4. Color selection.
5. `Confirmation/Reservation Code` input capped at 255 characters.
6. `Notes` free-text field capped at 2,000 characters.

**State transitions**:

- **Manual start/end -> Copy requested**: System preserves the manual value or asks before overwriting.
- **Leg start/end -> Copied form values**: System sets the form values from the selected trip leg; save is still required.
- **Copied form values -> Manual edits**: Traveler edits copied values and those manual edits are what save.
- **Pending edits -> Cancel**: System discards all unsaved form changes.