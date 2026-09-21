# Phase 0 Research: Editable Itinerary Table View

## 1. Data source and service boundaries

- **Decision**: Build Table view from the `TripDetail` already loaded by `TripDetails`; keep `GET /api/trips/{tripId}`, mutation endpoints, contracts, and PostgreSQL unchanged.
- **Rationale**: `TripDetail` includes all leg and item fields, record IDs, grouping keys, access level, and totals needed by the table. Modal saves already call existing mutations and `HandleModalSavedAsync` reloads the trip.
- **Alternatives considered**: A dedicated table endpoint was rejected because it duplicates the trip projection and creates another consistency boundary. Loading the timeline response was rejected because the print/table columns require fields already present on `TripDetail`.

## 2. Maximum sharing with print

- **Decision**: Extract one `TripItineraryTable` Razor component from the table body currently owned by `TripPrintDocument`. Both Table view and Print view render this component from the same `ItineraryTableModel`.
- **Rationale**: Sharing only helpers or CSS would still leave duplicate row markup that could drift. One renderer guarantees identical hierarchy, columns, labels, ordering, empty rows, and formatted values.
- **Alternatives considered**: Separate interactive and print components were rejected because they violate the maximum-shareability goal. Rendering `TripPrintDocument` directly inside trip details was rejected because its metadata wrapper and print-document semantics are not part of the switchable view.

## 3. Shared projection shape

- **Decision**: Refactor `PrintableTrip`, `PrintableLeg`, and `PrintableItem` into a neutral ID-bearing itinerary projection. Retain `TripLegId` and add `TrackedItemId` and `TripLegId` to item rows; keep formatted display strings for all shared columns.
- **Rationale**: The current print projection has the correct grouping and formatting but discards tracked-item IDs, making reliable modal selection impossible. A neutral projection prevents the renderer from depending on API DTO details and remains side-effect free.
- **Alternatives considered**: Looking up rows by title or array position was rejected as ambiguous and unstable. Parallel print and interactive models were rejected because their field sets and formatting would drift.

## 4. Existing modal reuse

- **Decision**: `TripItineraryTable` exposes optional `EventCallback<Guid>` leg/item selection callbacks plus add-leg and add-item callbacks. `TripDetails` resolves IDs against its current `TripDetail` and calls existing `OpenEditLegModal`, `OpenEditItemModal`, `OpenCreateLegModal`, and leg-prefilled create-item logic.
- **Rationale**: `TripDetails` already owns modal state, permission checks, forms, save handling, and reload behavior. Keeping ownership there avoids a second state machine and preserves all validation and error recovery.
- **Alternatives considered**: Inline cell editing and table-specific forms were rejected as direct duplication. Moving modal ownership into the table was rejected because the timeline and page actions already share page-level modal state.

## 5. View selection

- **Decision**: Add a two-option Timeline/Table segmented control in the trip-planning card header. Keep both views in the same trip-details route and render only the active view. Timeline-only date and map controls appear only while Timeline is active.
- **Rationale**: A segmented mode control matches two mutually exclusive views of the same data and avoids navigation or a second fetch. It also gives Table the full card width requested.
- **Alternatives considered**: A separate route was rejected because it fragments modal ownership and page context. Rendering both views at once was rejected because it is visually dense and keeps unnecessary timeline interop active.

## 6. Accessible interaction

- **Decision**: Preserve native table semantics and place explicit edit/add buttons inside cells rather than making `<tr>` elements simulate buttons. Give controls contextual accessible names such as `Edit leg Arrival` and `Edit item Hotel`.
- **Rationale**: Native buttons provide keyboard activation and focus behavior without adding conflicting interactive roles to table rows. Headers retain `scope="col"`; leg dividers retain `scope="rowgroup"` and full-column span.
- **Alternatives considered**: `role="button"` and `tabindex="0"` on `<tr>` were rejected because they require custom keyboard handling and can weaken table navigation semantics. Click-only rows were rejected as inaccessible and undiscoverable.

## 7. Responsive behavior

- **Decision**: Wrap the semantic table in a labeled region with contained horizontal scrolling and a stable minimum table width. Do not hide shared columns or convert rows into cards.
- **Rationale**: Horizontal scrolling preserves column alignment, print parity, and access to every field. Hiding columns conflicts with inspectability; card conversion would introduce a second markup hierarchy.
- **Alternatives considered**: Hiding low-priority columns and mobile cards were rejected because both reduce parity with print and duplicate responsive rendering logic.

## 8. Styling and print behavior

- **Decision**: Rename print-only table classes to neutral itinerary-table classes, then layer interactive hover/focus/action styles and `d-print-none` suppression on top. Keep the print metadata wrapper styles separate.
- **Rationale**: Structure and data styling should be shared; only controls and print page metadata differ. Existing print rules already repeat headers and avoid awkward row breaks.
- **Alternatives considered**: Maintaining parallel `.tp-print-*` and `.tp-table-*` structures was rejected because it duplicates layout rules.

## 9. Validation strategy

- **Decision**: Use pure projection tests for ordering/grouping/formatting/IDs, bUnit tests for semantic markup and callbacks, trip-details component tests for view/modal integration, and focused browser tests for horizontal overflow and keyboard access.
- **Rationale**: Each layer has a cheap discriminating test. Existing `TripPrintDocumentTests`, `TripDetailsPrintButtonTests`, and timeline tests provide local fixtures and patterns.
- **Alternatives considered**: E2E-only validation was rejected as slow and poor at isolating projection defects. Snapshot-only markup tests were rejected as brittle.

## Resolved technical context

All technical choices are resolved, and no new external dependency is required.