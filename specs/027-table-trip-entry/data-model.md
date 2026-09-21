# Phase 1 Data Model: Editable Itinerary Table View

## Persisted model

No persisted entity changes are required. The feature reads the existing `TripDetail`, `TripLegDto`, and `TrackedItemDto` contracts and delegates every mutation to existing modal forms and endpoints.

## Shared transient projection

### ItineraryTableModel

One immutable display projection for both Table and Print views.

| Field | Type | Source / rule |
|-------|------|---------------|
| `Legs` | `IReadOnlyList<ItineraryLegRow>` | Existing legs ordered by `StartLocal`, then `SortOrder`, then title |
| `UnassignedItems` | `IReadOnlyList<ItineraryItemRow>` | Items with null or unmatched `TripLegId`, ordered by `StartLocal`, then `SortOrder` |
| `HasContent` | `bool` | True when any leg or unassigned item exists |

### ItineraryLegRow

Full-width row-group divider followed by zero or more item rows.

| Field | Type | Source / rule |
|-------|------|---------------|
| `TripLegId` | `Guid` | `TripLegDto.TripLegId`; interactive edit key |
| `Title` | `string` | Leg title |
| `ModeText` | `string?` | Friendly transportation mode for Travel legs |
| `RouteText` | `string?` | Trimmed origin and destination joined by an arrow; absent for Stay legs |
| `StartText` | `string` | Existing local start plus short timezone display |
| `EndText` | `string` | Existing local end plus short timezone display |
| `ConfirmationCode` | `string?` | Omitted when blank |
| `TravelCostText` | `string?` | Existing display-currency formatting; omitted when null |
| `CanContainItems` | `bool` | Existing derived eligibility rule; controls add-item affordance only |
| `Items` | `IReadOnlyList<ItineraryItemRow>` | Matching items ordered by `StartLocal`, then `SortOrder` |

### ItineraryItemRow

One tracked-item row aligned to the shared table columns.

| Field | Type | Source / rule |
|-------|------|---------------|
| `TrackedItemId` | `Guid` | `TrackedItemDto.TrackedItemId`; interactive edit key |
| `TripLegId` | `Guid?` | Existing relationship; null or unmatched means Unassigned |
| `TypeText` | `string` | Existing item type in title case |
| `Title` | `string` | Item title |
| `Location` | `string?` | Omitted when blank |
| `StartText` | `string` | Existing local start plus short timezone display |
| `EndText` | `string?` | Existing local end plus end/start timezone fallback; absent when no end |
| `ConfirmationCode` | `string?` | Omitted when blank |
| `EstimatedCostText` | `string?` | Existing display-currency formatting; omitted when null |

## Presentation state

### TripPlanningView

Page-local state with exactly two values: `Timeline` and `Table`. It is not persisted and does not alter trip data. Timeline remains the initial view to preserve current behavior.

### Interaction capabilities

The shared table receives optional callbacks rather than storing edit state:

- edit leg by `TripLegId`
- edit item by `TrackedItemId`
- add leg
- add item with optional initial `TripLegId`

When callbacks are absent, as in Print view or viewer-only Table view, interactive controls are omitted.

## Validation and state transitions

The projection has no mutation state and performs no domain validation.

```text
TripDetail loaded
  -> Build shared projection
  -> Render Timeline or Table
  -> User invokes table action
  -> Existing modal opens
  -> Existing modal validates and saves
  -> TripDetails reloads TripDetail
  -> Rebuild projection and render current view
```

Failed or canceled modal operations leave the current persisted `TripDetail` unchanged. Existing form and API rules remain authoritative.