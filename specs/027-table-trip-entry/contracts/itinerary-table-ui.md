# UI Contract: Editable Itinerary Table

## View selection

- Trip details exposes a two-option `Timeline` / `Table` segmented control within the primary planning surface.
- `Timeline` is initially selected to preserve current behavior.
- Switching views does not navigate, fetch another representation, or mutate trip data.
- Timeline-only map and date-navigation controls are hidden while Table is selected.

## Table structure

- Render one semantic `<table>` with caption `Itinerary`.
- Columns, in order: `Type`, `Title`, `Location`, `Start`, `End`, `Confirmation`, `Est. Cost`.
- Render legs chronologically as full-width row-group dividers spanning every data column.
- Render each leg's items chronologically immediately after its divider.
- Render a full-width `No items for this leg.` row for an empty leg.
- Render unmatched and null-leg items beneath a trailing full-width `Unassigned` divider.
- Render the same structure and formatted values in interactive Table view and Print view.

## Leg divider content

Each leg divider presents, when applicable:

- title
- transportation mode
- origin-to-destination route
- start and end with timezone abbreviations
- confirmation code
- travel cost

An item-eligible leg may expose an interactive `Add item to {leg title}` button. A traveler with edit permission may expose an `Edit leg {leg title}` button. These controls are not part of printed output.

## Item-row interaction

- A traveler with edit permission receives an explicit `Edit item {item title}` button associated with each row.
- Invoking edit passes the stable `TrackedItemId` to the trip-details owner, which opens the existing tracked-item modal.
- No table cell becomes a form field.
- View-only travelers and Print view receive the same data rows without edit controls.

## Table-level actions

- Interactive Table view may expose `Add leg` and `Add item` actions above or below the table.
- Actions open the existing create modals.
- A leg-specific add action preselects that leg using its stable ID.
- Existing modal success reloads the trip and keeps Table selected.

## Accessibility

- Column headers use `<th scope="col">`.
- Leg and Unassigned dividers use a header cell spanning all columns with row-group semantics.
- Native buttons provide all activation; table rows do not receive simulated button roles.
- Every action has a contextual accessible name.
- Focus indicators remain visible.
- A labeled horizontal-scroll region contains the table at narrow widths; all columns remain available and aligned.

## Empty and failure behavior

- A trip with no legs or items shows the same empty message in Table and Print contexts.
- Empty legs remain visible with their no-items row.
- Failed modal saves remain inside existing modal behavior; the table continues to show last-saved data.
- Missing optional values render as empty cells, not placeholders or errors.