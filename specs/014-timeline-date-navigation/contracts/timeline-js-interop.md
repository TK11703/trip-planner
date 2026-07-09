# Contract: tripTimeline JavaScript Interop Module

**Feature**: 014-timeline-date-navigation
**Module**: `src/TripPlanner.Web/wwwroot/js/tripTimeline.js` (global `window.tripTimeline`)

Extends the existing `tripTimeline` module (which currently exposes `init` and `dispose`) with one new function for horizontal scroll positioning. Existing functions are unchanged.

## New function

### `tripTimeline.scrollToDate(scrollEl, offsetX)`

| Parameter | Type | Description |
|-----------|------|-------------|
| `scrollEl` | `HTMLElement` | The `.ttl-scroll` container element (the same `ElementReference` passed to `init`). |
| `offsetX` | `number` | Target horizontal scroll offset in pixels for the chosen day's left edge. |

**Behavior**:
- Sets the container's horizontal scroll position so `scrollLeft === clampedOffset`, where `clampedOffset` is `offsetX` clamped to `[0, scrollEl.scrollWidth - scrollEl.clientWidth]`.
- Uses smooth scrolling (`scrollEl.scrollTo({ left: clampedOffset, behavior: 'smooth' })`) by default.
- When the environment reports `prefers-reduced-motion: reduce`, falls back to an instant jump (`behavior: 'auto'`).
- Guards against `null`/undefined `scrollEl` and returns without error.
- Does not modify vertical scroll position and does not scroll the page.

**Invocation from Blazor**:

```csharp
await JS.InvokeVoidAsync("tripTimeline.scrollToDate", _scrollEl, offsetX);
```

**Offset computation (caller side, in `TripTimeline.razor`)**:

```text
dayIndex = date.DayNumber - _start.DayNumber
offsetX  = dayIndex * SlotsPerDay * SlotWidthPx   // 48 * 26 = 1248 px per day
```

## Error handling

- Consistent with existing interop calls, Blazor wraps invocations in `try/catch` for `JSDisconnectedException` and `TaskCanceledException` during teardown.
- The JS function is idempotent and side-effect-free beyond setting `scrollLeft`.
