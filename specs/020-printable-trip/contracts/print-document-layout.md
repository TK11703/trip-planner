# Contract: Print Document Layout

Defines the structure and ordering of the printable HTML document rendered by `TripPrint.razor` / `TripPrintDocument.razor`.

## Section order (top to bottom)

1. **Metadata block** (FR-003)
   - `<h1>` trip name
   - Date range: `MM/dd/yyyy` – `MM/dd/yyyy`
   - Description (omitted entirely when null/whitespace)
   - Estimated cost total (currency-formatted)

2. **Itinerary table** (FR-004/005) — a single `<table class="tp-print-table">` containing:
   - A `<caption>` (e.g., "Itinerary") for assistive tech.
   - A `<thead>` with column headers (`scope="col"`), which browsers repeat across printed pages (FR-012).
   - For **each leg in chronological order**, a **divider row**: a single `<th scope="colgroup" colspan="{columnCount}">` cell containing the leg title, optional `Origin → Destination`, and the leg's own `Start` / `End` (combined datetime+TZ). This is the requested "tabular row divider".
   - Immediately after each divider, that leg's **event rows** in chronological order.
   - If any events have no leg, a trailing **"Unassigned"** divider row followed by those events.

3. **Empty state** (FR-009) — when the trip has no legs and no events, the table is replaced by a clear message ("This trip has no legs or events yet.") beneath the metadata block.

## Event row columns (one per collected field) (FR-005/006)

| # | Column header | Source | When missing |
|---|---------------|--------|--------------|
| 1 | Type | `ItemType` (title-cased) | — |
| 2 | Title | `Title` | — |
| 3 | Location | `Location` | empty cell |
| 4 | Start | `FormatDateTimeWithZone(StartLocal, StartTimeZoneId)` | — |
| 5 | End | `FormatDateTimeWithZone(EndLocal, EndTimeZoneId ?? StartTimeZoneId)` | empty cell when no end |
| 6 | Confirmation | `ConfirmationCode` | empty cell |
| 7 | Est. Cost | `EstimatedCost` (currency) | empty cell |

Notes are intentionally **excluded** from the printout: a free-text note can be long and would break the tabular layout. Notes remain visible in the normal trip view.

`columnCount` for divider `colspan` MUST equal the number of event columns (7).

## Ordering rules

- Legs: ascending `StartLocal`, tie-break `SortOrder`, then title. (FR-004)
- Events within a leg: ascending `StartLocal`, tie-break `SortOrder`. (FR-004)

## Print/pagination behavior (FR-012)

- No fixed heights or `overflow:hidden` on the document; content flows naturally.
- `@media print` rules:
  - Hide `.tp-print-actions` (Print/Back controls) and any residual app chrome.
  - `thead { display: table-header-group; }` so headers repeat on each page.
  - `tr, .tp-print-leg { break-inside: avoid; }` where reasonable to reduce awkward splits; long note cells wrap (`overflow-wrap: anywhere`) rather than clip (edge cases: long titles/notes).
  - Ink-friendly: high-contrast text on white, borders for table separation.

## Theming

- On screen, reuse existing theme tokens (light/dark). Under `@media print`, force a light, ink-efficient palette regardless of the on-screen theme.
