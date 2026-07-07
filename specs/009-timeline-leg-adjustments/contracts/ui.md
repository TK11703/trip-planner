# UI Contract: Trip Timeline Leg Rows

## Scope

This contract describes the expected behavior of the trip timeline UI for feature 009. It covers the Blazor timeline component, the parent trip detail page that owns event creation, and the visual contract for leg rows in light and dark modes.

## Existing Surface

- Component: `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`
- Parent page: `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- Timeline styles: `src/TripPlanner.Web/wwwroot/css/app.css`
- Timeline helper script: `src/TripPlanner.Web/wwwroot/js/tripTimeline.js`
- API client method: `ITripApiClient.GetTimelineAsync(Guid tripId, CancellationToken ct = default)`
- Existing data contract: `TripTimelineResponse` with `TimelineLeg.Items`

## Leg Label Contract

Each rendered trip leg row MUST include the following in the sticky left label column:

1. Leg title.
2. Origin/destination summary when present.
3. Event count text below the leg identity content.
4. Add-event affordance for that leg, either as an explicit button/control in the label row or as a clearly discoverable row action that opens the existing event form with the leg pre-selected.

### Event Count Text

- `0 events` for a leg with no items.
- `1 event` for exactly one item.
- `# events` for all other counts.
- The count MUST be derived from the events currently associated with that leg in the timeline response.
- The count MUST update after a successful event add, delete, or reassignment refresh.

## Add-Event Contract

When the traveler initiates adding an event from a leg row:

1. The selected `TripLegId` MUST be passed to the existing event creation flow.
2. The event form MUST open with that trip leg pre-selected.
3. If a time slot was selected from the lane, the event start time SHOULD be initialized to the clicked slot.
4. Saving the event MUST refresh the timeline so the new event appears under the leg and the count updates.
5. Canceling the form MUST leave the timeline unchanged.

Unauthorized or missing trips continue to use the existing owner-scoped API behavior and must not expose add-event success.

## Timeline Lane Clickability Contract

A leg row MUST preserve access to time-slot selection even when one or more events span much of that row.

- Event bars MUST NOT occupy the full height of the timeline row.
- Event bars SHOULD be roughly half the row height.
- At least one contiguous vertical area of the row MUST remain available for clicking the underlying lane/time range.
- Event bars MUST remain directly clickable/selectable.
- The leg time-range band MUST remain non-interactive so it does not block lane clicks.

## Overlapping Events Contract

When two or more events in the same trip leg overlap in time, they MUST NOT be drawn on top of each other.

- Overlapping events MUST be placed on separate vertical lanes within the leg row (stacked), each occupying its own horizontal band.
- The leg row height MUST grow to fit the number of stacked lanes it needs, so no stacked event is hidden.
- Legs with more events (more overlap lanes) MAY be taller than other legs; uneven row heights are acceptable.
- Non-overlapping events MAY share the same lane.
- A leg with zero or one lane MUST keep the default row height.

## Visual Contract

### Light Mode

- Leg time-range bands remain visible without overwhelming event bars or grid lines.
- Event count text remains readable in the sticky label column.
- Event bars remain readable at their reduced height.

### Dark Mode

- Leg time-range bands MUST be clearly visible against the timeline surface.
- Adjacent or overlapping leg time ranges MUST remain distinguishable from each other and from event bars.
- Event count text MUST meet the same readability expectations as other secondary label text.
- No hover, zoom, or tooltip is required to identify the leg time range.

## Non-Goals

- No drag/drop scheduling contract.
- No new event type or bulk event creation contract.
- No new server API contract unless implementation discovers the existing timeline response cannot supply required data.
- No timeline virtualization contract in this feature.
