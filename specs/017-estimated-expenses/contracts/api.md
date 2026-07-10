# API Contract: Estimated Expenses

**Feature**: 017-estimated-expenses | **Date**: 2026-07-10 | **Phase**: 1

This feature adds an optional `estimatedCost` to existing tracked item create/update/read payloads and derived estimated totals to the timeline and trip detail projections. No new endpoints are introduced; existing owner-scoped, authenticated routes are extended. All amounts are non-negative decimals in a single display currency. Wording is fixed as "estimated cost" (per item) and "estimated total" (rollups).

## Conventions

- Authentication: existing Entra bearer auth; all routes remain owner-scoped.
- `estimatedCost` is a JSON number with up to two decimal places, or `null`.
  - `null` / omitted = no estimate recorded (excluded from totals).
  - `0` / `0.00` = explicit zero estimate (included in totals).
- Derived total fields (`estimatedCostTotal`) are read-only and default to `0`.

## Create Tracked Item

`POST` existing tracked item create route.

**Request** (`CreateTrackedItemRequest`, new field appended):

```json
{
  "tripLegId": "8f2c...",
  "itemType": "activity",
  "title": "City walking tour",
  "location": "Old Town",
  "startLocal": "2026-08-01T09:00:00",
  "startTimeZoneId": "America/New_York",
  "endLocal": "2026-08-01T11:00:00",
  "endTimeZoneId": "America/New_York",
  "displayColor": "teal",
  "confirmationCode": null,
  "notes": null,
  "estimatedCost": 45.00
}
```

**Validation** (added to existing rules, `TrackedItemValidator`):

| Rule | Failure field | Message (representative) |
|------|---------------|--------------------------|
| `estimatedCost` >= 0 when provided | `estimatedCost` | "Estimated cost cannot be negative." |
| `estimatedCost` within `NUMERIC(12,2)` bound and at most two decimals | `estimatedCost` | "Enter an estimated cost with up to two decimal places." |

**Responses**: `201/200` with the created `TrackedItemDto` including `estimatedCost`; `400` with a validation problem for invalid amounts.

## Update Tracked Item

`PUT` existing tracked item update route.

**Request** (`UpdateTrackedItemRequest`, new field appended): same shape as create.

- Sending `"estimatedCost": null` clears a previously recorded estimate.
- Sending a number replaces the prior value.
- Same validation as create.

**Responses**: `200` with updated `TrackedItemDto`; `400` on invalid amount; existing `404`/`403` behaviors unchanged.

## Read: Tracked Item DTO

`TrackedItemDto` gains `estimatedCost` (`decimal?`), round-tripping the stored value so the event modal can display and edit it.

```json
{
  "trackedItemId": "a1b2...",
  "tripId": "c3d4...",
  "tripLegId": "8f2c...",
  "itemType": "activity",
  "title": "City walking tour",
  "estimatedCost": 45.00
}
```

## Read: Timeline Projection

The timeline response is extended so the travel leg column can show an estimated total.

- `TimelineItem` gains `estimatedCost` (`decimal?`).
- `TimelineLeg` gains `estimatedCostTotal` (`decimal`) — the sum of its items' `estimatedCost` (NULLs ignored; `0` when none).

```json
{
  "tripId": "c3d4...",
  "legs": [
    {
      "tripLegId": "8f2c...",
      "title": "Rome",
      "estimatedCostTotal": 320.00,
      "items": [
        { "trackedItemId": "a1b2...", "title": "City walking tour", "estimatedCost": 45.00 }
      ]
    }
  ]
}
```

## Read: Trip Detail

`TripDetail` gains `estimatedCostTotal` (`decimal`) — the overall estimated total, equal to the sum of all leg estimated totals.

```json
{
  "tripId": "c3d4...",
  "name": "Italy 2026",
  "estimatedCostTotal": 1275.50,
  "legs": [ /* ... */ ],
  "trackedItems": [ /* each includes estimatedCost */ ]
}
```

**Invariants**:
- `TripDetail.estimatedCostTotal` == sum of `TimelineLeg.estimatedCostTotal` across the trip's legs (SC-002, SC-003).
- Items with `estimatedCost == null` do not affect any total; items with `0` are counted as zero.
