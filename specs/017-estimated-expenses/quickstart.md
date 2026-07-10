# Quickstart: Estimated Expenses

**Feature**: 017-estimated-expenses | **Date**: 2026-07-10 | **Phase**: 1

This guide validates that estimated costs can be recorded on events and that estimated totals roll up correctly in the timeline leg column and on the trip details page. It references [contracts/api.md](./contracts/api.md) and [data-model.md](./data-model.md) instead of restating field details.

## Prerequisites

- Local solution runs via Aspire (`TripPlanner.AppHost`), with PostgreSQL provisioned by the app host.
- The schema script `009_estimated_expenses.sql` has been applied (adds the `estimated_cost` column and non-negative check).
- An authenticated user who owns at least one trip with one or more trip legs.

## Run the app

```powershell
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the Web front end from the Aspire dashboard and sign in.

## Scenario 1 — Record an estimated cost (US1, P1)

1. Open a trip, open an event's detail modal, and locate the **Estimated cost** field (a secondary field near Notes).
2. Enter `45.00` and save.
3. Reopen the event: the **Estimated cost** shows `45.00`.
4. Edit it to `60`, save, reopen: value is `60.00`.
5. Clear the field, save, reopen: the estimate is empty (no estimate recorded).

**Expected**: The value persists across sessions; clearing removes it. Entering a negative value (e.g., `-5`) is rejected with a friendly "Estimated cost cannot be negative." message and the item is not saved.

## Scenario 2 — Trip estimated total on trip details (US2, P2)

1. Add estimated costs to several events across the trip (e.g., `45`, `120`, `0`, and leave one with no estimate).
2. Open the **trip details page**.
3. Confirm the **estimated total** equals the sum of the entered estimates (the no-estimate item is excluded; the `0` item is counted as zero).
4. Confirm the estimated total is shown in a secondary, non-dominant position.
5. With no estimates anywhere, confirm the trip details page shows `0` / a clear "no estimates yet" indication rather than blank or an error.

**Expected**: Trip estimated total matches the sum of item estimated costs and updates after add/edit/remove when the page is next viewed.

## Scenario 3 — Per-leg estimated totals in the timeline (US3, P3)

1. Ensure events with estimates are spread across different legs.
2. Open the timeline view.
3. Confirm each **travel leg column** shows an **estimated total** equal to the sum of that leg's item estimated costs.
4. Add the per-leg estimated totals together and confirm the sum equals the trip estimated total from Scenario 2.
5. A leg with no estimates shows `0` / a clear "no estimates" indication.

**Expected**: Per-leg estimated totals are correct and their sum equals the overall trip estimated total.

## Automated test coverage

- **Database tests** (`TripPlanner.Database.Tests`): upsert round-trips `estimated_cost` (null, zero, positive); non-negative constraint rejects negatives; leg/trip aggregation ignores NULLs and returns `0` when empty.
- **API tests** (`TripPlanner.Api.Tests/TripItems`): create/update accept valid `estimatedCost`, reject negative and >2-decimal values; DTO round-trips the value; trip detail and timeline responses expose correct `estimatedCostTotal` values and the sum invariant.
- **Web component tests** (`TripPlanner.Web.Tests`): the event modal binds/clears the estimated cost field; the timeline leg column renders the estimated total; the trip details page renders the overall estimated total and the empty-state indication.
- **E2E** (`TripPlanner.E2E.Tests`): enter estimates on events and verify the leg-column and trip-detail estimated totals reflect the sums.

## Success signals

- Estimated cost recorded on an item is reflected in the relevant totals in the same session (SC-001).
- Trip estimated total equals the sum of item estimated costs (SC-002).
- Sum of per-leg estimated totals equals the trip estimated total (SC-003).
- Recording an estimated cost takes under 30 seconds without help (SC-004).
- Expenses read as a supporting detail, not a primary feature (SC-005).
