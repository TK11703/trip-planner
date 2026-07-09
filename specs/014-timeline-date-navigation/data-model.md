# Phase 1 Data Model: Timeline Date Navigation

**Feature**: 014-timeline-date-navigation
**Date**: 2026-07-09

This feature is presentation-only and introduces **no persisted entities** and **no changes to `TripPlanner.Contracts`, the API, or the database**. The "entities" below are transient client-side view-state concepts within the `TripTimeline` Blazor component.

## View-State Concepts

### TripDateRange (derived)

Represents the inclusive span of dates a trip covers; the valid bounds for navigation.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| Start | `DateOnly` | `TripTimelineResponse.StartDate` (existing) | First navigable day; already held as `_start`. |
| End | `DateOnly` | `TripTimelineResponse.EndDate` (existing) | Last navigable day; already held as `_end`. |
| Days | `int` | Derived: `End.DayNumber - Start.DayNumber + 1` | Already held as `_days`; count of entries in the date list. |

**Derived list — `TripDates`**: ordered `IReadOnlyList<DateOnly>` of every date from `Start` to `End` inclusive. Length equals `Days`. Exposed publicly for the "Jump to date" control.

**Validation rules**:
- The list contains exactly `Days` entries with no gaps and no duplicates.
- Only dates within `[Start, End]` are selectable (FR-004).
- When `Days == 1`, the list has a single entry and day-stepping stops immediately at both boundaries (edge case: single-day trip).

### TimelinePosition (transient)

The date the timeline is currently focused on / scrolled to.

| Field | Type | Notes |
|-------|------|-------|
| CurrentDate | `DateOnly?` | The last date the user jumped/stepped to; `null` before any navigation (initial view sits at trip start). Drives the current-position highlight (FR-003). |

**State transitions**:
- Jump to date `d` (in range): `CurrentDate := d`, then scroll so `d` aligns to the left edge.
- Next day: if `CurrentDate < End` then `CurrentDate := CurrentDate + 1 day` and scroll; else no-op with boundary feedback (FR-006).
- Previous day: if `CurrentDate > Start` then `CurrentDate := CurrentDate - 1 day` and scroll; else no-op with boundary feedback.
- Jump to trip start: `CurrentDate := Start`.
- Jump to trip end: `CurrentDate := End`.
- Jump to today: allowed only when `Start <= Today <= End`; then `CurrentDate := Today`; otherwise the shortcut is unavailable/inapplicable (FR-008).

### DayScrollOffset (derived, no state)

The horizontal pixel offset used to place a day at the left edge of the scroll track.

- **Formula**: `offsetX(dayIndex) = dayIndex * SlotsPerDay * SlotWidthPx`, where `dayIndex = date.DayNumber - Start.DayNumber`.
- **Existing constants** (in `TripTimeline.razor`): `SlotWidthPx = 26`, `SlotMinutes = 30`, `SlotsPerDay = 48`.
- Passed to the JS interop `tripTimeline.scrollToDate(scrollEl, offsetX)`; not stored.

## Relationships

```text
TripTimelineResponse (existing DTO)
        │  StartDate / EndDate
        ▼
TripDateRange ──derives──▶ TripDates (list shown in Jump-to-date control)
        │
        ▼
TimelinePosition.CurrentDate ──maps via DayScrollOffset──▶ scrollLeft on .ttl-scroll
```

## Non-Goals

- No new DTOs, request/response contracts, endpoints, SQL, or migrations.
- No change to how times/time zones are displayed (navigation only).
- No persistence of the user's last-viewed date across sessions.
