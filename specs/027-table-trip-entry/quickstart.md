# Quickstart: Validate Editable Itinerary Table View

## Prerequisites

- .NET 10 SDK
- Repository dependencies restored
- A test account with one editable trip and, for permission checks, one view-only shared trip
- The editable trip should contain a Stay leg, a Car leg, a restricted Travel leg, assigned items, an empty leg, and an unassigned item

## Automated checks

From the repository root:

```powershell
dotnet build TripPlanner.slnx
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```

Expected outcome: the solution builds and all web component/projection tests pass, including existing print and timeline coverage.

## Run locally

Use the existing Aspire task or run:

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the Web endpoint shown by Aspire, sign in, and open the prepared trip.

## Scenario 1: Hierarchy and parity

1. Keep Timeline selected and note the trip's legs and items.
2. Select Table.
3. Verify every leg is a full-width divider in chronological order.
4. Verify assigned items appear immediately beneath their leg in chronological order.
5. Verify the empty leg remains visible with `No items for this leg.`
6. Verify the unassigned item appears in a trailing Unassigned group.
7. Open Print and compare the itinerary table.

Expected outcome: Table and Print use the same hierarchy, columns, labels, and formatted values. Print contains no interactive controls.

## Scenario 2: Existing modal reuse

1. Return to Table view.
2. Invoke Edit on a leg, change a value in the existing leg modal, and save.
3. Invoke Edit on an item, change a value in the existing item modal, and save.
4. Use table-level Add leg and Add item actions.
5. Use Add item on an eligible leg divider.

Expected outcome: existing modal layouts and validation are used throughout; successful saves return to Table and refresh its data without a full page reload. The leg-specific item modal starts with that leg selected.

## Scenario 3: Eligibility and permissions

1. Confirm Stay and Car dividers can offer Add item.
2. Confirm Flight, Train, Bus, and Boat dividers do not offer Add item.
3. Open a view-only shared trip and select Table.

Expected outcome: eligibility matches existing rules. The viewer sees the complete table but no add or edit controls.

## Scenario 4: Keyboard and narrow viewport

1. Navigate the view selector and every table action using Tab and Shift+Tab.
2. Activate controls with Enter or Space.
3. Repeat at a narrow supported viewport.
4. Scroll the table horizontally to the final column.

Expected outcome: native focus remains visible, action names identify their row, columns remain aligned, and all values remain reachable without overlap.

## Contract reference

See [contracts/itinerary-table-ui.md](contracts/itinerary-table-ui.md) for the required row hierarchy, interaction contract, accessibility semantics, and empty-state behavior.

## Validation evidence

Validated on 2026-09-21:

- `dotnet build TripPlanner.slnx`: passed with no errors. The existing `AngleSharp` NU1902 advisory remains.
- `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --no-restore`: 285 passed, 3 pre-existing skipped, 0 failed.
- Focused table/page integration suite: 15 passed, including focus restoration to the invoking table action after modal close.
- Playwright scenario contracts: 6 discovered and skipped by the repository convention because they require a running AppHost.
- Scenario 1: passed against a local trip with three leg groups, empty-leg states, and one tracked item; screen and print rendered the same hierarchy and seven columns, and the shared print table contained no controls.
- Scenario 2: passed for leg-specific item creation and existing item editing; the existing forms opened with the selected leg/item and Table remained active after save.
- Scenario 3: owner eligibility passed; restricted Flight legs suppressed Add item while the Stay leg exposed it. Viewer control suppression passed in bUnit. A second authenticated viewer identity was not available for a separate manual browser session.
- Scenario 4: passed at a 390px viewport. The 317px region exposed all 1,024px of table content through 707px of horizontal scroll, long-content rows had positive non-overlapping bounds, print reset overflow/minimum width, and modal close restored focus to the invoking item action.