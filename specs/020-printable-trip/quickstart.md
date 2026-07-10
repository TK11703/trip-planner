# Quickstart: Printable Trip View

Validates the printable trip feature end to end. See [plan.md](plan.md), [data-model.md](data-model.md), and [contracts/](contracts/) for details.

## Prerequisites

- .NET 10 SDK, and the ability to run the Aspire AppHost (VS Code task **watch (Aspire hot reload)** or `dotnet run --project src/TripPlanner.AppHost`).
- A signed-in user who **owns** at least one trip that has several legs and events, including events with times, locations, notes, and estimated costs across more than one timezone.

## Run the app

```pwsh
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the web front end from the Aspire dashboard and sign in.

## Scenario 1 — Open the printable version (User Story 1, FR-001/002/010)

1. Navigate to a trip you own: `/trips/{tripId}`.
2. In the header actions, click **Print**.
3. **Expected**: You land on `/trips/{tripId}/print` showing a clean document — trip name, dates, description, and estimated total — with **no** navigation bar, footer, or app menus. Only a small on-screen **Print** and **Back** control are visible.

## Scenario 2 — Every detail is present, in order (User Story 2, FR-003/004/005)

1. On the print page, confirm the **metadata block** appears first (name, `MM/dd/yyyy` – `MM/dd/yyyy` range, description, estimated total).
2. Confirm legs appear as **row dividers in chronological order**, each showing title, `Origin → Destination`, and the leg's start/end.
3. Under each leg, confirm its **events** appear as table rows with columns: Type, Title, Location, Start, End, Confirmation, Est. Cost. (Notes are intentionally omitted to keep the table layout intact.)
4. **Expected**: An event missing an optional field (e.g., no location or cost) still appears, with that cell simply empty — no error text.

## Scenario 3 — Combined datetime + timezone column (FR-008)

1. Inspect any Start / End cell.
2. **Expected**: Each shows a single combined value like `07/14/2026 09:30 America/New_York`. For a multi-timezone trip, each cell carries its own zone token, so the schedule is unambiguous. Times match the wall-clock values shown in the normal trip view.

## Scenario 4 — Print / Save as PDF (FR-002/007)

1. Click the on-screen **Print** button (or press Ctrl/Cmd+P).
2. In the browser dialog, choose a printer or **Save as PDF**.
3. **Expected**: The preview/output contains only the trip document — the Print/Back controls and all app chrome are absent. A large trip flows across multiple pages with repeated column headers and no truncated content.

## Scenario 5 — Empty trip (FR-009)

1. Open the print page for a trip with no legs or events.
2. **Expected**: A coherent page with the metadata block and a clear "no legs or events" message — not a broken or blank table.

## Scenario 6 — Ownership (FR-013)

1. While signed in as a user who does **not** own a trip, navigate directly to `/trips/{thatTripId}/print`.
2. **Expected**: The existing not-found/denied state renders (chrome-free); no trip content is shown. The **Print** button is not offered on trips you do not own.

## Scenario 7 — Return to the app (FR-011)

1. From the print page, click **Back**.
2. **Expected**: You return to `/trips/{tripId}` without losing your place.

## Automated checks

- **bUnit** (`tests/TripPlanner.Web.Tests/Trips/TripPrintPageTests.cs`): metadata block renders; legs render as dividers in chronological order; events render as rows with the expected columns; a Start/End cell matches `MM/dd/yyyy HH:mm TZ`; empty-trip state; denied state on null detail.
- **Unit** (`tests/TripPlanner.Web.Tests/Trips/TripPrintFormattingTests.cs`): `FormatDateTimeWithZone` examples/edge cases; leg ordering; event grouping/ordering; missing-optional handling.
- **E2E** (`tests/TripPlanner.E2E.Tests`): owner clicks **Print** → chrome-free print page containing the trip's legs and events.

Run the fast suites:

```pwsh
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```
