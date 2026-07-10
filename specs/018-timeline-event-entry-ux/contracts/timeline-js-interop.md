# Contract: `tripTimeline` JS Interop Module (centered-date reporter)

**Feature**: 018-timeline-event-entry-ux
**Module**: `src/TripPlanner.Web/wwwroot/js/tripTimeline.js`

Adds a debounced scroll listener that reports the centered day index back to the Blazor component. Existing functions keep their contracts.

## Existing functions (unchanged behavior)

| Function | Contract |
|----------|----------|
| `tripTimeline.init(scrollEl)` → `tripTimeline.init(scrollEl, dotNetRef)` | **Change**: accepts an optional `dotNetRef`. Still sets up the resize handler for sticky day labels. Now also wires the debounced scroll listener when `dotNetRef` is provided. |
| `tripTimeline.scrollToDate(scrollEl, offsetX)` | Unchanged: smooth-scrolls to `offsetX` (clamped), honoring `prefers-reduced-motion`. |
| `tripTimeline.dispose(scrollEl)` | **Change**: also removes the scroll listener and clears the stored `dotNetRef`. |

## New behavior in `init(scrollEl, dotNetRef)`

- **Geometry constants** must match the component: day width = `SlotsPerDay * SlotWidthPx` = `48 * 26` = `1248` px. (Read from a data attribute or CSS var if available; otherwise a shared constant documented here.)
- **Centered day computation**:
  - `labelW = parseFloat(getComputedStyle(root).getPropertyValue('--ttl-label-w')) || 0`
  - `centerTrackX = scrollEl.scrollLeft + (scrollEl.clientWidth - labelW) / 2`
  - `dayIndex = Math.floor(centerTrackX / dayWidthPx)`
  - Clamp `dayIndex` to `[0, daysCount - 1]` (days derived from `.ttl-day` count).
- **Debounce**: attach a `scroll` listener on `scrollEl` that schedules the computation ~120ms after scrolling settles (trailing debounce), then calls `dotNetRef.invokeMethodAsync('SetCenteredDayIndex', dayIndex)` only when the index changed since the last report.
- **Cleanup**: store `{ onScroll, onResize, dotNetRef, lastIndex }` on `scrollEl.__ttlSticky`; `dispose` removes both listeners and deletes the state.

## Error handling

- Guard against a missing `scrollEl`/`root` (no-op).
- Wrap `invokeMethodAsync` so a disconnected circuit (`JSDisconnectedException` on the .NET side) does not throw uncaught in JS; a rejected promise is swallowed.

## Non-goals

- No change to scroll geometry, zoom, or granularity.
- No per-frame work: computation runs only on the trailing edge of a debounce.
