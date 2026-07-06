# Data Model: Trip Leg Events and Timeline

## Trip

Represents the overall journey.

**Fields relevant to this feature**:

- `TripId`: unique trip identifier.
- `OwnerUserId`: authenticated owner.
- `Name`: trip name.
- `StartDate`: initially entered trip start date.
- `EndDate`: initially entered trip end date.

**Relationships**:

- Has many `TripLeg` records.
- Has many `TrackedItem` records through the same trip ownership boundary.
- Has one computed `TripTimeline` projection.

**Validation rules**:

- Timeline access is allowed only for the trip owner.
- The timeline date range spans the union of the trip date range and all leg/item dates.

## TripLeg

Represents a dated segment of a trip and acts as the timeline resource row.

**Fields relevant to this feature**:

- `TripLegId`: unique leg identifier.
- `TripId`: parent trip identifier.
- `OwnerUserId`: authenticated owner copied for owner-scoped queries.
- `Title`: leg/resource label shown in the left timeline column.
- `Origin`: optional leg origin.
- `Destination`: optional leg destination.
- `StartLocal`: local leg start date/time.
- `StartTimeZoneId`: timezone for `StartLocal`.
- `EndLocal`: local leg end date/time.
- `EndTimeZoneId`: timezone for `EndLocal`.
- `SortOrder`: stable fallback ordering when leg times tie.

**Relationships**:

- Belongs to one `Trip`.
- Has many `TrackedItem` records.
- Appears as one row in `TripTimeline` even when it has no items.

**Validation rules**:

- A leg cannot be deleted while tracked items still reference it; the traveler must reassign or remove those items first.
- Reordering or re-dating a leg does not change the `TripLegId` relationship on its tracked items.

## TrackedItem

Represents a trip event, activity, reservation, or reminder that appears on the timeline.

**Fields relevant to this feature**:

- `TrackedItemId`: unique tracked item identifier.
- `TripId`: parent trip identifier.
- `TripLegId`: selected leg relationship; required for new and updated items, nullable only for legacy unassigned items.
- `OwnerUserId`: authenticated owner copied for owner-scoped queries.
- `ItemType`: one of `event`, `reservation`, `activity`, or `reminder`.
- `Title`: event label displayed on the timeline block.
- `Location`: optional location text.
- `StartsAt`: event start instant.
- `EndsAt`: optional event end instant.
- `DisplayColor`: selected event color used for the timeline block.
- `SortOrder`: stable fallback ordering when event times tie.

**Relationships**:

- Belongs to one `Trip`.
- Belongs to exactly one `TripLeg` for all new and updated items.
- May be temporarily unassigned only when it predates the feature or is imported without a leg assignment.

**Validation rules**:

- `TripLegId` is required for create and update requests.
- The selected `TripLegId` must belong to the same trip and owner as the tracked item.
- `DisplayColor` must be one of the supported palette values or a valid normalized color value accepted by the UI contract.
- `EndsAt`, when supplied, must be on or after `StartsAt`.
- An event outside its leg's timeframe is allowed but must be flagged in the timeline projection.

## TripTimeline

Computed per-trip view model for the custom resource timeline.

**Fields**:

- `TripId`: parent trip identifier.
- `StartDate`: first date visible in the timeline, covering the trip and its legs/items.
- `EndDate`: last date visible in the timeline, covering the trip and its legs/items.
- `SlotMinutes`: fixed at 30 for this feature.
- `Legs`: ordered timeline resource rows.
- `UnassignedItems`: legacy items that do not yet have a leg.

## TimelineLeg

One resource row in the custom timeline.

**Fields**:

- `TripLegId`: leg identifier.
- `Title`: row label.
- `Origin`: optional origin.
- `Destination`: optional destination.
- `StartLocal`: leg start local date/time.
- `EndLocal`: leg end local date/time.
- `SortOrder`: stable row ordering.
- `Items`: timeline events related to this leg.

**Ordering**:

1. Start instant ascending.
2. `SortOrder` ascending.
3. Title ascending.
4. `TripLegId` ascending as a final deterministic tie-breaker.

## TimelineItem

One visible event block in a timeline leg row.

**Fields**:

- `TrackedItemId`: item identifier.
- `TripLegId`: related leg identifier.
- `ItemType`: event type.
- `Title`: visible event label.
- `Location`: optional location.
- `StartsAt`: event start instant.
- `EndsAt`: optional event end instant.
- `DisplayColor`: timeline block color.
- `StartsOutsideLeg`: true when the item starts before or after the leg range.
- `EndsOutsideLeg`: true when the item end is outside the leg range.
- `SortOrder`: stable ordering value.

**Ordering within a leg**:

1. `StartsAt` ascending.
2. `SortOrder` ascending.
3. Title ascending.
4. `TrackedItemId` ascending as a final deterministic tie-breaker.

## State Transitions

- **Unassigned legacy item -> Assigned item**: Traveler selects a valid leg and saves the item.
- **Assigned item -> Reassigned item**: Traveler selects a different leg from the same trip and saves.
- **Assigned item -> Deleted item**: Traveler removes the event.
- **Leg with items -> Delete blocked**: System prevents deletion until items are reassigned or removed.
- **Leg without items -> Deleted leg**: System deletes the leg normally.