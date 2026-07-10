# Research: Estimated Expenses

**Feature**: 017-estimated-expenses | **Date**: 2026-07-10 | **Phase**: 0

This document resolves the approach for capturing an optional estimated cost per tracked item and rolling it up into leg and trip estimated totals, grounded in the existing tracked item, timeline, and trip-detail implementation.

## Decision 1: Where the estimated cost lives

**Decision**: Add a single optional `estimated_cost` money value to the existing `tracked_items` record rather than creating a separate expense entity or table.

**Rationale**: The spec and user direction attach a "potential cost for each event" collected in the event detail modal form. The existing `tracked_items` table already backs every event/activity/reservation/reminder and is edited through one modal (`TrackedItemForm.razor`) and one upsert command (`UpsertAndDeleteTrackedItems.sql`). A nullable column keeps expenses a secondary detail, avoids a new vertical slice, and requires no join to compute rollups.

**Alternatives considered**:
- *Separate `estimated_expenses` table keyed to items*: Rejected. Adds a table, repository, and join for a single scalar per item; over-engineered for a non-primary feature.
- *Trip-level or leg-level manual expense entries independent of items*: Rejected. Contradicts the user's direction to collect a cost per event and the spec assumption that estimates attach to itinerary items.

## Decision 2: Storage type and precision

**Decision**: Store `estimated_cost` as `NUMERIC(12,2) NULL` with a `CHECK (estimated_cost IS NULL OR estimated_cost >= 0)` constraint, mapped to a C# `decimal?`.

**Rationale**: Money requires exact decimal precision, so `NUMERIC` (not floating point) is correct. Two decimal places matches the single display currency. `NUMERIC(12,2)` comfortably holds any realistic trip estimate (up to 9,999,999,999.99) while bounding layout risk. Nullable preserves the required distinction between "no estimate" (NULL, excluded from totals) and "estimate of zero" (0.00, included). The non-negative check enforces FR-004 at the storage layer, mirroring existing length-check constraints on `confirmation_code`/`notes`.

**Alternatives considered**:
- *Integer minor units (cents)*: Rejected. Adds conversion complexity at every boundary for no benefit given Dapper/Npgsql map `NUMERIC` to `decimal` cleanly.
- *`money` PostgreSQL type*: Rejected. Locale-dependent and discouraged; `NUMERIC` is the standard recommendation.

## Decision 3: Missing vs. zero

**Decision**: Treat `NULL` as "no estimate recorded" (excluded from totals and shown as an empty field) and `0.00` as an explicit recorded value (included in totals).

**Rationale**: FR-008 and the edge cases require this distinction. The web form binds an optional `decimal?`; clearing the input sends `null` (removes the estimate), while entering `0` sends `0.00`.

**Alternatives considered**:
- *Default missing to 0*: Rejected. Loses the distinction the spec explicitly requires and would misrepresent items with no estimate.

## Decision 4: How totals are computed

**Decision**: Compute leg and trip estimated totals on demand rather than persisting them. The trip estimated total equals the sum of all leg estimated totals, and each leg estimated total equals the sum of the estimated costs of the items assigned to that leg.

**Rationale**: The user direction is explicit: "the total can be rolled up in each leg" and "an overall estimated cost ... summing up the estimated costs of all the legs." Derived totals guarantee SC-002/SC-003 (totals always equal the underlying sums) with no risk of drift. The timeline query (`GetTripTimeline.sql`) and trip-detail query already load the items needed, so aggregation is either a SQL `SUM(...) FILTER (WHERE trip_leg_id = ...)` or an in-memory rollup after mapping. NULL values are naturally ignored by SQL `SUM`; a leg with no estimates yields `0`.

**Alternatives considered**:
- *Persist and maintain running totals*: Rejected. Adds write-time complexity and drift risk for a read-only summary; violates the "keep it simple, secondary detail" intent.
- *Include leg-unassigned (legacy) items in the trip total*: Rejected as the default. Because the trip total is defined as the sum of leg totals, unassigned items are excluded to keep SC-003 exact. New events must belong to a leg, so this only affects legacy data; documented in data-model.

## Decision 5: Validation placement

**Decision**: Reuse the existing `TrackedItemValidator` in the TripItems API slice to validate `estimated_cost`: reject values below zero, and reject values that exceed the storage bound or have more than two decimal places (normalizing precision before persistence).

**Rationale**: Validation for tracked item fields already lives here (title, timezones, colors, length limits) and returns friendly `ValidationResult` messages. Adding the estimated cost rule keeps validation in one place and consistent with FR-004. Client-side, the Blazor form uses a numeric input with `min="0"` and `step="0.01"` plus a DataAnnotations `[Range]` to give immediate feedback, but the API remains the authority.

**Alternatives considered**:
- *Client-only validation*: Rejected. The API is the trust boundary and must reject invalid values regardless of client.

## Decision 6: Currency and formatting

**Decision**: Present all estimated amounts in a single, consistent display currency using the application's existing culture/number formatting; no per-trip or per-item currency selection.

**Rationale**: The spec assumes a single currency and the user direction does not mention currency. A fixed display currency keeps the feature lightweight and the totals directly summable. Formatting uses standard currency display so amounts read naturally in both the modal and the rollups.

**Alternatives considered**:
- *Per-trip currency selection*: Rejected/out of scope. Adds settings and conversion concerns disproportionate to a non-primary feature; can be revisited later.

## Decision 7: Where totals appear and wording

**Decision**: Show the per-leg **estimated total** in the travel leg column of the timeline (`TripTimeline.razor`) and the overall **estimated total** on the trip details page (`TripDetails.razor`), both in a secondary, non-dominant position. The per-item field is labeled "Estimated cost" in the event modal.

**Rationale**: Matches the user's exact placement direction and the FR-010 requirement that expenses stay secondary. Consistent wording ("estimated cost" per item, "estimated total" for rollups) reinforces that amounts are estimates, not actuals, satisfying the user's explicit terminology request.

**Alternatives considered**:
- *Prominent budget banner or dedicated expenses tab*: Rejected. Contradicts the "not a primary focus" requirement.
