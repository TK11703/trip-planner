# Quickstart: Trip Leg Events and Timeline

## Prerequisites

- .NET 10 SDK available.
- PostgreSQL/Aspire prerequisites used by this repository are installed.
- Run commands from the repository root.

## Setup

Restore and build the solution:

```powershell
dotnet restore .\TripPlanner.slnx
dotnet build .\TripPlanner.slnx
```

Start the Aspire app host for manual validation:

```powershell
dotnet watch --non-interactive --project .\src\TripPlanner.AppHost\TripPlanner.AppHost.csproj
```

## Scenario 1: Create an event related to a trip leg

1. Sign in and open a trip that has at least one leg.
2. Choose **Add event**.
3. Confirm the event form requires a trip leg selection.
4. Select a leg, choose an event color, fill required item details, and save.
5. Reopen the trip.

Expected outcome:

- The event persists with the selected leg.
- The event displays in that leg's row on the trip timeline using the selected color.
- The event does not appear under other legs.

## Scenario 2: View the custom trip timeline

1. Open a trip with multiple trip legs and events across different days.
2. Review the trip detail page timeline.
3. Scroll horizontally across the time grid and vertically through leg rows if needed.

Expected outcome:

- The left column lists trip legs as timeline resources.
- The first timeline header row shows day and date.
- The second timeline header row shows hours of the day.
- Each hour is divided into two 30-minute slots.
- Events appear in the row for their related leg.
- Empty legs remain visible.

## Scenario 3: Select timeline items

1. Select an event block in the timeline.
2. Select a trip leg row or leg block in the timeline.

Expected outcome:

- Selecting an event opens the existing tracked item modal populated with that event.
- Selecting a leg opens the existing trip leg modal populated with that leg.
- The trip detail page does not show a selected-item side details pane for timeline selections.

## Scenario 4: Reassign and protect related events

1. Edit an existing event and assign it to a different leg on the same trip.
2. Save and reopen the trip timeline.
3. Attempt to delete a leg that still has related events.

Expected outcome:

- The reassigned event moves to the new leg row.
- The event no longer appears under the previous leg.
- Deleting a leg with related events is blocked with guidance to reassign or remove those events.

## Focused Test Commands

Run the relevant test projects after implementation:

```powershell
dotnet test .\tests\TripPlanner.Database.Tests\TripPlanner.Database.Tests.csproj
dotnet test .\tests\TripPlanner.Api.Tests\TripPlanner.Api.Tests.csproj
dotnet test .\tests\TripPlanner.Web.Tests\TripPlanner.Web.Tests.csproj
```

For end-to-end validation, run the existing E2E project once the app host is available:

```powershell
dotnet test .\tests\TripPlanner.E2E.Tests\TripPlanner.E2E.Tests.csproj
```