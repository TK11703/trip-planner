# Quickstart: Validate Trip Leg and Event Timeline Adjustments

## Prerequisites

- .NET 10 SDK available.
- PostgreSQL/local dependencies available through the existing Aspire setup.
- A user account that can sign in to the Trip Planner app.
- A trip with at least two trip legs and several events assigned across those legs.

## Start the App

From the repository root:

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the Trip Planner web app from the Aspire dashboard or the configured web endpoint.

## Scenario 1: Per-Leg Event Counts

1. Sign in and open a trip with multiple trip legs.
2. View the trip timeline.
3. Inspect the left trip-leg label column.

Expected outcome:

- Every trip leg shows an event count below the leg label content.
- A leg with no events shows `0 events` or an equivalent clear no-events count.
- Counts match the events rendered in each row.

## Scenario 2: Add Event from a Trip Leg Row

1. On the timeline, choose the add-event action for a specific trip leg row.
2. Confirm the event form opens with that leg selected.
3. Fill in the minimum required event fields.
4. Save the event.

Expected outcome:

- The new event appears under the selected leg on the timeline.
- The selected leg's event count increments after save.
- Other leg counts do not change.
- Canceling the same flow before save creates no event and changes no counts.

## Scenario 3: Click a Time Slot Even When Events Span the Row

1. Use a trip leg that has an event spanning a large part of its time range.
2. Click a visible open area in that leg's timeline lane at a specific time.
3. Confirm the add-event flow starts for that leg and time.

Expected outcome:

- Event bars do not consume the full row height.
- A visible portion of the leg row remains clickable for time-slot selection.
- Clicking the reduced-height event bar still selects/opens that event.
- Clicking the open lane area starts adding an event at the clicked time.

## Scenario 4: Dark Mode Leg Range Visibility

1. Switch the application to dark mode.
2. Open a trip timeline with multiple legs, including adjacent or overlapping legs if available.
3. Compare the leg time-range bands against the background, grid, and event bars.

Expected outcome:

- Each leg's time range is easy to spot without hover or zoom.
- Adjacent or overlapping leg ranges remain distinguishable.
- Event bars and count text remain readable.

## Scenario 5: Reassignment and Deletion Count Refresh

1. Reassign an event from one leg to another, or delete an event from a leg.
2. Return to or refresh the timeline.

Expected outcome:

- The source leg count decreases when an event is moved away or deleted.
- The destination leg count increases when an event is reassigned to it.
- Counts remain consistent with the rendered event bars.

## Suggested Automated Checks

```powershell
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

Focused automated coverage should assert the UI contract in [contracts/ui.md](./contracts/ui.md), especially event-count labels, pre-selected leg creation, reduced event bar height, lane clickability, and dark-mode leg-band visibility.
