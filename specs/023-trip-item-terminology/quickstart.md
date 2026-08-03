# Quickstart: Validating the Item Terminology Rename

**Feature**: 023-trip-item-terminology

How to prove the rename is complete and behavior-neutral. Run these after implementation, in order.

---

## Prerequisites

- .NET 10 SDK
- Docker running (Aspire provisions PostgreSQL)
- A local database that already contains at least one trip, one leg, one item, and one pending parsed draft — this is what proves the migration preserves data

If you do not have existing data, create it **before** applying the change: run the app on `main`, add a trip with a leg and an item, and forward or simulate one booking email so a draft is pending. Then switch to the feature branch.

---

## 1. Build

```powershell
dotnet clean TripPlanner.slnx
dotnet build TripPlanner.slnx
```

Expect 0 errors. The clean step is not optional — renamed `.sql` files leave stale copies in `bin/`, and a stale copy can hide a broken `_sql.Get(...)` lookup until runtime (research R-004).

## 2. Unit and component tests

Run each project separately; `dotnet test` rejects multiple project paths in one invocation.

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
```

Expect all green. Baseline before the change is 102 passed / 6 skipped in Api.Tests and 128 passed / 3 skipped in Web.Tests — counts should be unchanged, since this feature renames tests rather than adding or removing them.

## 3. Data migration — the critical check

Start the app against the pre-existing database.

```powershell
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Then verify in PostgreSQL:

```sql
-- The legacy table is gone
SELECT to_regclass('public.parsed_event_drafts');   -- expect NULL

-- The new table exists and kept every row
SELECT count(*) FROM parsed_item_drafts;            -- expect the pre-change count

-- Ids and type values carried over unchanged
SELECT parsed_item_draft_id, item_type, review_status FROM parsed_item_drafts LIMIT 5;
```

Then **restart the app** and re-run the three queries. Results must be identical. This proves the reconciling script is a stable no-op on subsequent starts rather than recreating a stray legacy table (research R-003).

Also confirm the core item data is untouched:

```sql
SELECT item_type, count(*) FROM tracked_items GROUP BY item_type;
```

Items saved as `event` must still be present and still typed `event` (FR-016).

## 4. Traveler-facing walkthrough

Covers spec user stories 1 through 4. Each maps to an acceptance scenario in [spec.md](./spec.md).

**Timeline and trip details (US1)**

1. Open a trip with a leg holding two items. The leg summary reads "2 items".
2. Empty a leg. It reads "0 items". Leave exactly one item. It reads "1 item".
3. Hover the add control on a leg row — tooltip and accessible name read "Add an item to {leg}"; the button reads "+ Add item".
4. Create an item with no leg relation. The notice reads "1 item is not related to a trip leg." Add a second. It reads "2 items are not related to a trip leg." No `item(s)` anywhere.
5. Open the printable view. Headings and empty states say "items".

**Add and edit form (US2)**

1. Open the add modal. Title reads "Add item"; the color group's accessible name reads "Item color".
2. The type selector still offers **Event**, Reservation, Activity, Reminder.
3. Save an item typed Event. Its badge still reads "Event".
4. Submit a start outside the leg's travel window. The validation message refers to the item, not an event.
5. On a trip with no legs, attempt to add. The guidance refers to items.

**Notifications and email review (US3)**

1. On a shared trip, add, edit, then delete an item as a collaborator. All three notifications say "item".
2. Open the review screen with a pending draft. Page title and heading read "Review parsed items".
3. Discard all drafts. Empty state reads "No items are waiting for review…".
4. Attempt to confirm a draft with no trip assigned. Guidance reads "Assign this item to a trip before confirming."

**Help content (US4)**

Read the FAQ and About pages. Every reference to a leg's children says "item", and the four types are named accurately.

## 5. Email parsing — the highest-risk path

The language model prompt and its response envelope were renamed together. If they drifted apart, parsing silently returns nothing.

1. Forward or simulate a booking confirmation email into the trip inbox.
2. Confirm a draft appears on the review screen with a populated type, title, and dates.
3. Confirm the draft, then confirm the item appears on the trip.

A non-empty draft is the signal. An empty review queue with no error is the failure mode to watch for (research R-005).

## 6. Terminology gate

Run the search defined in [contracts/terminology-gate.md](./contracts/terminology-gate.md). Every surviving hit must fall into one of its three allowed categories. This is the acceptance criterion for SC-001, SC-002, and SC-007.

Also confirm no placeholder plurals remain:

```powershell
Get-ChildItem -Path src, tests -Recurse -File -Include *.cs, *.razor |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'item\(s\)|event\(s\)'
```

Expect no results.

---

## Rollback

The change is a single commit with no external side effects. Reverting the code reverts the wire contract. The database is the exception: once `012` has run, `parsed_item_drafts` holds the data and the legacy table is gone, so reverting the code against a migrated database will break the drafts feature. Either revert the database from backup as well, or roll forward.
