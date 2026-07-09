# Contract: TripTimeline Component Public API

**Feature**: 014-timeline-date-navigation
**Component**: `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`

This contract defines the new public surface the `TripTimeline` component exposes so its host page (`TripDetails.razor`) can drive date navigation via the existing `@ref="_timeline"`. Existing parameters and callbacks are unchanged.

## New public members

### `IReadOnlyList<DateOnly> TripDates { get; }`

- Returns every date from the trip's start to end, inclusive, in ascending order.
- Empty until the timeline data has loaded; callers must handle an empty list (e.g., control disabled while loading).
- Length equals the number of trip days.

### `DateOnly? CurrentDate { get; }`

- The date the timeline is currently positioned on (last jumped/stepped to), or `null` before any navigation has occurred.
- Used by the host to reflect the active selection in the "Jump to date" control.

### `bool TodayInRange { get; }`

- `true` when the current local date falls within `[StartDate, EndDate]`; otherwise `false`.
- The host uses this to enable/disable or hide the "Today" shortcut (FR-008).

### `Task ScrollToDateAsync(DateOnly date)`

- Preconditions: `date` is within `[StartDate, EndDate]`. Out-of-range values are clamped to the nearest boundary (no exception, no scroll into empty space).
- Effect: sets `CurrentDate = date` and scrolls the timeline so the day's first column aligns to the left edge of the scroll track.
- No-op (safe) if the timeline grid is not yet rendered/available.

### `Task GoToNextDayAsync()` / `Task GoToPreviousDayAsync()`

- Move `CurrentDate` by +1 / −1 day and scroll, clamped to `[StartDate, EndDate]`.
- At a boundary, the method makes no movement and leaves position unchanged (host communicates the boundary state).
- If `CurrentDate` is `null`, "next" starts from `StartDate` and "previous" starts from `EndDate` (deterministic entry).

### `Task GoToTripStartAsync()` / `Task GoToTripEndAsync()` / `Task GoToTodayAsync()`

- Convenience wrappers over `ScrollToDateAsync` targeting `StartDate`, `EndDate`, and today respectively.
- `GoToTodayAsync` is a no-op when `TodayInRange` is `false`.

## New callback parameter (for layout relocation)

### `EventCallback OnAddLegRequested { get; set; }`

- Invoked when the user activates the "Add leg" footer button rendered at the bottom of the trip-legs column.
- Only surfaced when `CanEdit` is `true`.
- `TripDetails` wires this to its existing `OpenCreateLegModal` handler.

## Behavioral guarantees

- All navigation methods are safe to call repeatedly and when data is still loading (they no-op rather than throw).
- Navigation never changes timeline structure, zoom, hour granularity, or the displayed times.
- Existing selection/edit callbacks (`OnLegSelected`, `OnItemSelected`, `OnLegSlotSelected`) continue to function unchanged during and after navigation.
