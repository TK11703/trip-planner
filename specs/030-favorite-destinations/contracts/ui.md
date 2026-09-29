# UI Contract: Favorite Destinations

## Navigation and Favorites Page

- Show a `Favorites` link in the signed-in account dropdown in the upper-right navigation. Anonymous users do not see it.
- Route the link to `/favorites`, a signed-in-only page listing only the current caller's favorites.
- Provide create, view, edit, and delete actions. Deletion requires a confirmation state. Empty state includes an add action.
- Below the page heading, present favorites in a rounded card. The card header holds the search box (left half) and a right-aligned toolbar of small icon buttons with tooltips: New, Import, and Delete. The header stays sticky while the list scrolls. Search filters name, address, city, country, and notes. Delete is disabled until at least one row is selected and shows the selected count.
- The card body holds the import panel (when open), status messages, empty/no-match states, and the favorites table, which spans the full card width with striped rows on the card background.
- The table has a selection checkbox column (plus a select-all header checkbox for the listed rows) followed by Name, Address, City, and Country. Name opens the edit dialog. Bulk delete acts only on selected rows currently listed and requires confirmation in a dialog.
- Display results in country, city, name order. Notes are reviewed and edited in the dialog rather than the table.
- New and edit use a modal dialog that asks for name and address (plus optional notes, which also hold any source or recommendation). City, country, and coordinates are calculated from the address on save and shown read-only; they are not editable. An unresolved address remains saveable with blank calculated fields. The edit dialog also offers Delete with confirmation.
- Import opens a card with a JSON/CSV file picker, format hint, and Import file / Cancel buttons. Show parse/validation errors by row, identify possible duplicates, require explicit duplicate confirmation, and report the number of records imported. Invalid batches do not partially import.

## Tracked-Item Dialog

- Directly under the trip leg selection, add a labeled searchable favorite picker (combobox) to the tracked-item creation dialog for signed-in callers. It filters the caller's favorites by name, address, city, or country and shows each option's address, city, and country.
- Selecting a favorite fills item title from favorite name, location from favorite address, and notes when present.
- Keep all copied values editable. Do not reset or overwrite the selected trip, leg, schedule, item type, timezone, color, confirmation code, or estimated cost.
- Saving continues through the existing tracked-item API and validation. Canceling or changing the form does not alter the favorite.
- Loading and API failure states do not block manual tracked-item entry. If the caller has no favorites, omit the selector or present a non-blocking empty state with a link to Favorites.

## Interaction States

The page and dialog expose loading, empty, filtered-no-results, validation, duplicate-warning, save/import success, and API-failure states. Controls have labels and keyboard-accessible actions; destructive confirmation and import duplicate confirmation are explicit.