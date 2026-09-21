# Quickstart: Validate Travel Leg Modes and Item Eligibility

## Prerequisites

- .NET 10 SDK
- Docker-compatible container runtime for the Aspire-managed PostgreSQL resource
- Repository dependencies restored

## Start the Application

```powershell
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the Web endpoint shown by Aspire and sign in with a traveler who can edit a trip.

## Scenario 1: Travel Details by Mode

1. Add Flight, Train, Bus, and Boat legs.
2. For each, verify origin, destination, start/end, and both time zones are required while travel cost and confirmation number are optional.
3. Save each mode once without booking details and once with valid booking details; reopen the legs and verify supplied values are retained.
4. Try negative or over-precision cost and a whitespace-only or overlength confirmation; verify invalid supplied values are refused.
5. Add a Stay leg; verify no mode, route, or other travel-only fields are offered or persisted.

Expected: all six resulting leg shapes follow [data-model.md](data-model.md), and invalid requests produce the field-level outcomes in [contracts/api.md](contracts/api.md).

## Scenario 2: Item Eligibility

1. On Stay and Car legs, use Add item and save an item whose times are inside the leg.
2. On Flight, Train, Bus, and Boat, verify Add item is unavailable.
3. Open manual item edit and email-draft review; verify restricted legs are absent from selectors.
4. Submit a direct item request naming a restricted leg.

Expected: Stay and Car accept valid items. Every restricted-mode path refuses assignment, including direct requests.

## Scenario 3: Safe Mode Changes

1. With an item on a Car leg, attempt to change its mode to Flight.
2. Verify the save is refused and no field or relationship changes.
3. Move the item to an eligible leg or unassign it, then retry with or without optional cost and confirmation details.

Expected: the second change succeeds only after no items remain. This also validates the database race-safe invariant rather than only the browser state.

## Scenario 4: Migration

Using data created before migration `014`:

1. Verify each nonblank-origin leg becomes Travel/Car.
2. Verify each leg without origin becomes Stay.
3. Verify every item remains assigned and editable.
4. Verify migrated Car legs still permit valid item creation.

Expected: no traveler intervention, fabricated booking data, or lost relationships.

## Scenario 5: Timeline, Print, and Totals

1. View and print a trip containing a ticketed leg with cost and an item with estimated cost.
2. Verify mode, route, confirmation, and travel cost display on the leg.
3. Verify item subtotal and travel cost are distinguishable.
4. Verify the trip estimated total includes each cost exactly once.

## Focused Automated Validation

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj --filter "TripLeg|TrackedItem|DraftPlacement"
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj --filter "TripLeg|TrackedItem|Timeline"
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "TripLeg|TrackedItem|Timeline|Print|Inbox"
dotnet build TripPlanner.slnx
```

Expected: all focused tests and the full solution build pass.

## Validation Results

Recorded after the implementation of this feature. Each scenario was exercised through the
automated suites listed below; the runs are reproducible with the commands above.

| Command | Result |
| --- | --- |
| `dotnet build TripPlanner.slnx` | Build succeeded, no warnings |
| API focused (`TripLeg\|TrackedItem\|DraftPlacement`) | Passed — 76/76 |
| Database focused (`TripLeg\|TrackedItem\|Timeline`) | Passed — 39 passed, 1 pre-existing skip |
| Web focused (`TripLeg\|TrackedItem\|Timeline\|Print\|Inbox`) | Passed — 173/173 |
| Full solution test run | Passed — 451 passed, 25 pre-existing skips, 0 failed |

### Scenario 1: Travel Details by Mode — PASS

Every mode's required and optional fields are covered by `TripLegFormTests` (form shape, mode list,
optional booking details, hydration on edit, switching modes) and `TripLegEndpointTests` (server-side
refusal of a missing mode, a missing origin, negative or over-precision cost, and a blank or
overlength confirmation). Saving a travel leg without booking details is accepted, and a stay leg
persists no mode or travel-only values.

### Scenario 2: Item Eligibility — PASS

`TripTimelineTests` proves restricted legs expose no add-item action and ignore lane clicks, while
stay and car legs keep theirs. `TrackedItemFormLegWindowTests` and `InboxReviewPageTests` prove
restricted legs are absent from the manual and email-draft leg pickers, including the
all-restricted empty state. `TrackedItemEndpointTests` refuses a direct request naming a restricted
leg, and `TripLegModeInvariantTests` proves the database rejects the insert even if the API is
bypassed.

### Scenario 3: Safe Mode Changes — PASS

`TripLegEndpointTests` and `TripLegFormTests` cover the refusal and the preserved edits.
`TripLegModeInvariantTests` covers the database side, including the two interleaving orders of a
concurrent item assignment and mode change: each blocks the other and the loser is refused with the
`trip_legs_item_eligibility` constraint. An emptied leg accepts every restricted mode afterwards.

### Scenario 4: Migration — PASS

`TripLegModeMigrationTests` applies every schema script before `014`, seeds legacy legs and items,
then applies `014` alone. Origin-bearing legs become Travel/Car, blank and whitespace-only origins
become Stay, booking columns stay empty, every item keeps its leg, and re-applying the migration
changes nothing. `TimelineQueryTests` reads that same migrated database and confirms migrated car
legs still report `CanContainItems`.

### Scenario 5: Timeline, Print, and Totals — PASS

`TripTimelineTests` confirms mode label, route, confirmation, and travel cost render, and that
travel cost is shown separately from the item estimate. `TripPrintFormattingTests` and
`TripPrintDocumentTests` confirm the same values reach the printable document and that a stay leg
prints no mode.

### Remaining manual confirmation

The automated coverage above asserts markup, contracts, and database behaviour. A final pass in a
running browser against the Aspire-hosted app is still worth doing for visual layout of the new
mode selector, restriction notice, and printed leg block.
