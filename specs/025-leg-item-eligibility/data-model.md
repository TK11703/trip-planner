# Data Model: Travel Leg Modes and Item Eligibility

## Trip Leg

Represents either time spent at a destination or movement between two places.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `TripLegId` | UUID | Yes | Stable identity |
| `TripId` | UUID | Yes | Existing trip relationship |
| `LegKind` | string | Yes | `stay` or `travel` |
| `TransportationMode` | string | Conditional | Required for Travel; null for Stay; `flight`, `train`, `bus`, `boat`, or `car` |
| `Title` | string | Yes | Existing title rules |
| `Origin` | string | Conditional | Nonblank for Travel; null for Stay |
| `Destination` | string | Conditional | Nonblank for Travel; null for Stay |
| `StartLocal` | local date/time | Yes | Existing trip-range rules |
| `StartTimeZoneId` | string | Yes | Existing valid-timezone rule |
| `EndLocal` | local date/time | Yes | End instant is not before start instant |
| `EndTimeZoneId` | string | Yes | Existing valid-timezone rule |
| `TravelCost` | decimal(12,2) | No | Optional and nonnegative for every Travel mode; null for Stay |
| `ConfirmationCode` | string | No | Optional, trimmed, and max 255 for every Travel mode; null for Stay |
| `Notes` | string | No | Existing limit and behavior |

### Derived Eligibility

`CanContainItems` is derived, not stored:

| Leg state | Can contain items |
|---|---:|
| Stay | Yes |
| Travel / Car | Yes |
| Travel / Flight | No |
| Travel / Train | No |
| Travel / Bus | No |
| Travel / Boat | No |

### Row Invariants

- Stay: mode, origin, destination, travel cost, and confirmation code are null.
- Travel: mode, origin, and destination are nonblank.
- Every Travel mode: travel cost and confirmation code may be null; supplied cost is nonnegative with at most two decimal places, and supplied confirmation is nonblank after trimming and at most 255 characters.
- Cost has at most two decimal places and the same upper bound as existing estimated costs.

### State Transitions

| From | To | Result |
|---|---|---|
| Stay | Car | Allowed with valid Travel route/mode fields, even when items exist |
| Stay | Flight/Train/Bus/Boat | Allowed only when no items exist and all supplied booking fields are valid |
| Car | Stay | Allowed; clears mode, origin, cost, and confirmation code |
| Car | Flight/Train/Bus/Boat | Allowed only when no items exist and all supplied booking fields are valid |
| Restricted mode | Car | Allowed; booking fields may be retained or cleared |
| Restricted mode | Stay | Allowed; clears all travel-only fields |
| Restricted mode | another restricted mode | Allowed when all supplied booking fields remain valid |

Mode eligibility is rechecked atomically when the row is updated. A failed transition changes neither the leg nor its items.

## Item

The existing tracked item model is unchanged. Its optional `TripLegId` relationship gains one invariant:

- Null remains valid and places the item in the unassigned area.
- A nonnull leg must belong to the same editable trip, contain the item's instants, and have `CanContainItems = true`.
- Creating, reassigning, or confirming an email draft applies this rule.

## Placement Candidate

The transient email-placement row adds enough leg semantics to enforce eligibility, or is filtered before mapping:

- Trips remain candidates when they have no eligible legs, preserving gap/outside-trip explanations and unassigned confirmation.
- Flight, Train, Bus, and Boat legs never become placement candidates.
- Stay and Car legs retain the existing instant-containment matching behavior.

## Timeline Leg

The timeline projection adds:

- `LegKind`
- `TransportationMode`
- `TravelCost`
- `ConfirmationCode`
- `CanContainItems` (derived in shared code or projection)
- Existing `EstimatedCostTotal` remains the child-item subtotal.

The trip estimated total is the sum of item estimated costs plus leg travel costs, with each value counted once.

## Migration

Migration `014_trip_leg_modes.sql` performs these steps in one schema migration:

1. Add nullable columns.
2. Normalize whitespace-only origins to null.
3. Backfill rows with origin as `travel/car`; rows without origin as `stay`.
4. Make `leg_kind` nonnull.
5. Add allowed-value, shape, length, precision, and nonnegative checks.
6. Add a trigger on tracked-item insert/update to reject restricted target legs.
7. Add a trigger on leg kind/mode update to reject a restricted state while related items exist.

No existing item or relationship is changed.