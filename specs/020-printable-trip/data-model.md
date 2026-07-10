# Phase 1 Data Model: Printable Trip View

This feature introduces **no persisted data** and **no new contracts**. It is a read-only rendering of the existing `TripDetail` returned by `GET /api/trips/{tripId}`. This document describes the **view models** the print page derives in memory from that existing DTO.

## Source (existing, unchanged)

`TripDetail` (from `TripPlanner.Contracts.Trips`) already provides everything the printout needs:

| Source field | Used for |
|--------------|----------|
| `Name`, `StartDate`, `EndDate`, `Description`, `EstimatedCostTotal`, `CreatedAtUtc` | Metadata block |
| `Legs` : `TripLegDto[]` | Leg row-dividers (chronological) |
| `TrackedItems` : `TrackedItemDto[]` | Event rows grouped under legs |
| `IsOwner` / null result | Ownership / denied handling |

`TripLegDto` fields consumed: `TripLegId`, `Title`, `Origin`, `Destination`, `StartLocal`, `StartTimeZoneId`, `EndLocal`, `EndTimeZoneId`, `SortOrder`.

`TrackedItemDto` fields consumed: `TrackedItemId`, `TripLegId`, `ItemType`, `Title`, `Location`, `StartLocal`, `StartTimeZoneId`, `EndLocal`, `EndTimeZoneId`, `ConfirmationCode`, `EstimatedCost`, `SortOrder`. (`Notes` is intentionally not printed — see below.)

## Derived view models (transient, in `TripPlanner.Web`)

These are computed by `TripPrintFormatting` and consumed by the print page/table. They are never persisted or sent over the wire.

### PrintableTrip

The top-level view model assembled for one trip.

| Field | Type | Derivation |
|-------|------|-----------|
| `Name` | string | `TripDetail.Name` |
| `DateRangeText` | string | `StartDate.ToString("MM/dd/yyyy")` – `EndDate.ToString("MM/dd/yyyy")` (InvariantCulture) |
| `Description` | string? | `TripDetail.Description` (omitted when null/whitespace) |
| `EstimatedCostText` | string | `EstimatedCostTotal.ToString("C")` |
| `Legs` | list of `PrintableLeg` | `OrderLegsChronologically(TripDetail.Legs)` with events grouped in |
| `HasContent` | bool | `Legs` non-empty OR any tracked items exist |

### PrintableLeg (row-divider)

One per leg, rendered as a full-width divider row followed by its event rows.

| Field | Type | Derivation |
|-------|------|-----------|
| `TripLegId` | Guid | `TripLegDto.TripLegId` |
| `Title` | string | `TripLegDto.Title` |
| `RouteText` | string? | `Origin` → `Destination` when either present (e.g., "Seattle → Tokyo") |
| `StartText` | string | `FormatDateTimeWithZone(StartLocal, StartTimeZoneId)` |
| `EndText` | string | `FormatDateTimeWithZone(EndLocal, EndTimeZoneId)` |
| `Events` | list of `PrintableEvent` | `OrderEventsWithinLeg(items for this leg)` |

An implicit trailing group holds events whose `TripLegId` is null (labeled "Unassigned"), only rendered if such events exist.

### PrintableEvent (table row)

One per tracked item; one property per column.

| Column / Field | Type | Derivation | Missing-value behavior |
|----------------|------|-----------|------------------------|
| `TypeText` | string | Title-cased `ItemType` (event/reservation/activity/reminder) | always present |
| `Title` | string | `TrackedItemDto.Title` | always present |
| `Location` | string? | `TrackedItemDto.Location` | empty cell when null/whitespace |
| `StartText` | string | `FormatDateTimeWithZone(StartLocal, StartTimeZoneId)` | always present |
| `EndText` | string? | `EndLocal` present → `FormatDateTimeWithZone(EndLocal, EndTimeZoneId ?? StartTimeZoneId)` | empty cell when no end |
| `ConfirmationCode` | string? | `TrackedItemDto.ConfirmationCode` | empty cell when null |
| `EstimatedCostText` | string? | `EstimatedCost?.ToString("C")` | empty cell when null |

> `Notes` is intentionally excluded from the printout: free-text notes can be long and would break the tabular layout. They remain available in the normal trip view.

## Ordering & grouping rules (in `TripPrintFormatting`)

- **Legs**: ascending `StartLocal`, tie-break ascending `SortOrder`, then `Title` ordinal. → `OrderLegsChronologically`.
- **Events within a leg**: ascending `StartLocal`, tie-break ascending `SortOrder`. → `OrderEventsWithinLeg`.
- **Grouping**: events partitioned by `TripLegId`; unmatched (null or unknown leg id) collected into the trailing "Unassigned" group. → `GroupEventsByLeg`.

## Datetime + timezone formatting rule

`FormatDateTimeWithZone(DateTime local, string timeZoneId)` → `\"{local:MM/dd/yyyy HH:mm} {timeZoneId}\"` using `CultureInfo.InvariantCulture`. Example: `07/14/2026 09:30 America/New_York`. See [contracts/datetime-tz-format.md](contracts/datetime-tz-format.md).

## Relationships

```text
TripDetail (existing DTO from GET /api/trips/{tripId})
 ├── metadata ──────────────► PrintableTrip (name, date range, description, total)
 ├── Legs (TripLegDto 0..*) ─► PrintableLeg (row divider, chronological)
 └── TrackedItems (0..*) ────► PrintableEvent (grouped under owning PrintableLeg;
                                                null-leg items → "Unassigned" group)
```

## Entity-to-requirement mapping

| Spec requirement | View-model element |
|------------------|--------------------|
| FR-003 (trip summary block) | `PrintableTrip.Name/DateRangeText/Description/EstimatedCostText` |
| FR-004 (legs chronological, events grouped) | `OrderLegsChronologically` + `GroupEventsByLeg` + `OrderEventsWithinLeg` |
| FR-005 (event details as columns) | `PrintableEvent` columns |
| FR-006 (missing optional omitted, no error) | Missing-value behavior per column |
| FR-008 (times consistent, multi-TZ) | `FormatDateTimeWithZone` combined datetime+TZ column |
| FR-009 (coherent empty page) | `PrintableTrip.HasContent` → empty-state message |
| FR-013 (ownership) | Null `TripDetail` (denied) → denied state; owner-only entry button |
