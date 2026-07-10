# Contract: Event Form Behaviors

**Feature**: 018-timeline-event-entry-ux
**Component**: `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
**Host**: `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`

Defines the date-default, reactive-end, location-validation, location-map, and type-icon behaviors added to the event form. Fields and persistence are unchanged.

## 1. Add-event date defaults (create only)

- **Host wiring** (`TripDetails`): when opening the Add-event modal from the per-leg "+ Add event" (and any header add path), set `_initialItemStart` to the timeline's active date via `_timeline?.CurrentDate`, combined with a sensible default time. A lane click continues to pass its precise slot start (unchanged).
- **Form** (`TrackedItemForm`): existing behavior already sets `EndLocal = start.AddHours(1)` when `InitialStartsAt` is applied — so start and end both default from the active date.
- **Fallback**: when `CurrentDate` is null (no navigation yet), fall back to the trip start day (FR-006).
- **Edit**: `Item is not null` path is unchanged — saved dates are shown and never overwritten by the active date (FR-005).

## 2. Reactive end time

- **State**: add `_endManuallySet` (bool), initialized false on create; true when editing an existing item (its end is authoritative).
- **Start change handler**: bind `StartLocal` through a setter/handler that, when `_endManuallySet == false`, sets `EndLocal = StartLocal + 1h`.
- **End change handler**: when the traveler edits `EndLocal` directly, set `_endManuallySet = true` (auto-adjust stops).
- **Interaction with "Copy from trip leg"**: unchanged; copying the leg end sets the end explicitly (treated as a manual set).

## 3. Location presented as an address + validation

- **Presentation**: the Location field is labeled/hinted as an address (placeholder guiding address-style entry). Field remains optional.
- **Validation** (advisory, only when non-empty): a custom validation attribute or `Validate` rule on `Location`:
  - Trim the value.
  - Valid when it contains at least one letter or digit and its length is within a reasonable bound (e.g. ≤ 200 chars).
  - Invalid when it is solely whitespace/punctuation/symbols.
  - Empty is valid (optional).
- **Message**: inline via the existing `ValidationMessage` pattern, e.g. "Enter a place or address that can be shown on a map."

## 4. Globe icon → open location on a map

- **Control**: a globe icon `button`/link rendered adjacent to the Location field, enabled only when `Location` is non-empty and valid (FR-011/FR-012).
- **Action**: open `https://www.bing.com/maps?q={UrlEncode(Location)}` (an external maps search URL) in a new browser context.
- **Safety**: new tab opened with reverse-tabnabbing-safe semantics (`target="_blank"` + `rel="noopener noreferrer"`, or JS `window.open(url, '_blank', 'noopener')`).
- **Accessibility**: `aria-label="Open location in a map"`; the SVG glyph is `aria-hidden`.
- **Unsaved work**: opening in a new context leaves the event form intact (FR-014).

## 5. Type icon in the form

- Render `<TrackedItemIcon Type="_model.ItemType" />` next to the Type selector so the selected type's icon is visible at entry time (matches the timeline icon).

## Validation summary

| Rule | Enforced where | Outcome |
|------|----------------|---------|
| Empty location allowed | validation | Valid; no map action offered |
| Non-empty must be map-capable | validation | Invalid input blocks save + hides map action |
| End auto-follows start (+1h) | start handler | Only until end manually set |
| Dates default from active date | host + form | Create only |
