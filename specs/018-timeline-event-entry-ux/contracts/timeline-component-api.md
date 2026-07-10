# Contract: `TripTimeline` Component API (active-date tracking)

**Feature**: 018-timeline-event-entry-ux
**Component**: `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`

This describes the public members and interop callback used to track and expose the active (centered) date, and to render the type icon. Members not listed here are unchanged from their current behavior.

## Existing members reused (unchanged signatures)

| Member | Behavior |
|--------|----------|
| `DateOnly? CurrentDate` | The date the timeline is positioned on. **Change**: now also updated by scroll (see below), not only by jump/step/shortcut. |
| `IReadOnlyList<DateOnly> TripDates` | All trip dates, start to end inclusive. Unchanged. |
| `Task ScrollToDateAsync(DateOnly date)` | Aligns the day to the left edge and sets `CurrentDate`. Unchanged externally. |
| `Task GoToNextDayAsync()` / `GoToPreviousDayAsync()` / `GoToTripStartAsync()` / `GoToTripEndAsync()` / `GoToTodayAsync()` | Unchanged. |
| `bool AtTripStart` / `AtTripEnd` / `TodayInRange` | Unchanged. |
| `EventCallback OnLoaded` | Unchanged; still raised after load. |

## New / changed members

### `[JSInvokable] void SetCenteredDayIndex(int dayIndex)`

- **Called by**: `tripTimeline.js` debounced scroll listener (see `timeline-js-interop.md`).
- **Behavior**: Maps `dayIndex` to `DateOnly` as `_start.AddDays(dayIndex)`, clamps to `[_start, _end]`, and if changed, sets `_currentDate`, calls `StateHasChanged()`, and raises `OnActiveDateChanged`.
- **Guards**: No-op when `_timeline is null`, `!HasGrid`, or `_disposed`.

### `EventCallback<DateOnly> OnActiveDateChanged` (new `[Parameter]`)

- **Purpose**: Notifies the host (`TripDetails`) when the active date changes so header controls (the "Jump to date: {date}" label and active highlight) re-render to reflect scroll-driven changes.
- **Raised by**: `SetCenteredDayIndex` and the existing `ScrollToDateAsync` path.

### DotNet reference lifecycle

- **On** `OnAfterRenderAsync(firstRender: true)` (when `HasGrid`): create a `DotNetObjectReference<TripTimeline>` and pass it to `tripTimeline.init(scrollEl, dotNetRef)`.
- **On** `DisposeAsync`: dispose the `DotNetObjectReference` after `tripTimeline.dispose(scrollEl)`.

### Type icon rendering

- Each timeline item button additionally renders `<TrackedItemIcon Type="item.ItemType" />` before the title span (see `type-icons.md`). Purely additive to markup; no API change.

## Invariants

- The active date is always within the trip range.
- Scroll-driven updates are idempotent: reporting the same day index twice does not raise duplicate change notifications.
- Existing left-edge jump geometry and sticky-header behavior are preserved.
