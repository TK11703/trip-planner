# Phase 0 Research: Timeline Event Entry Experience Enhancements

**Feature**: 018-timeline-event-entry-ux
**Date**: 2026-07-10

The specification contained no `[NEEDS CLARIFICATION]` markers, and the planning input from the user added concrete design direction (centered active date tracked on scroll and jump; add-event defaults start and end to the active date; end reacts to one hour after a chosen start; location recommended as an address with a globe icon opening a map; location validated as a map-capable format; item type maps to an icon shown on the timeline). Research below confirms how that direction maps onto the existing implementation and resolves the two decisions that required a chosen default.

## Existing implementation facts (verified)

- `TripTimeline.razor` renders one horizontally scrollable grid with fixed geometry: `SlotWidthPx = 26`, `SlotMinutes = 30`, `SlotsPerDay = 48` (day width = 1248px). Day index `d`'s left offset is `d * SlotsPerDay * SlotWidthPx`.
- The component already tracks `_currentDate` (exposed as `CurrentDate`) but only sets it from `ScrollToDateAsync` (jump/step/shortcut). Free scrolling does **not** update it today.
- `tripTimeline.js` already computes the viewport center (`centerX = labelW + (clientWidth - labelW) / 2`) for sticky day labels and already exposes `scrollToDate(scrollEl, offsetX)`. There is currently no scroll event listener.
- `TripDetails.razor` hosts the timeline via `@ref="_timeline"` and drives the header "Jump to date"/step/shortcut controls. Adding an event happens via the per-leg "+ Add event" button and lane clicks, which raise `OnLegSlotSelected` with a start; the create modal binds `InitialStartsAt`, and `TrackedItemForm` already sets `EndLocal = start.AddHours(1)` when it applies the initial start.
- `TrackedItemForm.razor` has a plain `InputText` for `Location` (no validation, no map action) and an `InputSelect` for `ItemType` over the fixed set (`activity`, `reservation`, `event`, `reminder`).
- The timeline projection `TimelineItem` already includes `ItemType` (`TripPlanner.Contracts/Timeline/TimelineContracts.cs`), so type icons need no contract/API/DB change.
- The app has no icon-font dependency; existing icons are inline SVG (`BrandMark.razor`, brand globes) or SVG-in-CSS (nav menu). New icons will follow the inline-SVG pattern.

## Decision 1: Track the active (centered) date on scroll

- **Decision**: Add a debounced `scroll` listener in `tripTimeline.js` that computes the centered day index from `scrollLeft` and the fixed day width, then reports it to the component through a `DotNetObjectReference` calling a `[JSInvokable]` method (e.g. `SetCenteredDayIndex(int)`). The component maps the index to a `DateOnly` and updates `_currentDate` (clamped to the trip range). Jump/step/shortcut actions continue to set `_currentDate` directly.
- **Rationale**: The user defines the active date as the centered date and wants it tracked for both scrolling and the jump action. Deriving the day from `scrollLeft` is O(1) against fixed geometry and needs no layout work; debouncing (e.g. ~120ms) keeps interop chatter low. Reusing the existing `centerX` math keeps a single definition of "center."
- **Alternatives considered**:
  - Update `_currentDate` only on jump/step (today's behavior) — rejected: the user explicitly wants scrolling to update the active date.
  - Track via Blazor `@onscroll` on the element — rejected: it fires very frequently and would round-trip raw scroll positions to the server every event; a debounced JS reducer reporting only the day index is far cheaper and matches how `tripTimeline.js` already owns scroll geometry.

## Decision 2: Center vs. left-edge as the "active" position

- **Decision**: The active date is the day whose column contains the horizontal center of the visible track. `ScrollToDateAsync` continues to align a chosen day to the left edge (unchanged), and after such a jump the reported centered day is reconciled so the displayed active date matches the user's selection when the target is reachable; near the trip's end where the last days cannot reach center, the active date clamps to the last day.
- **Rationale**: The user said "an active date is one in which the date is centered." Keeping the existing left-edge scroll target avoids reworking navigation while still deriving the active date from the center. Clamping at the trip end avoids a stuck/blank active date.
- **Alternatives considered**: Redefine jump to center the target day — rejected: changes established navigation geometry and existing tests for left-edge alignment (feature 014) with no user request to do so.

## Decision 3: Default new-event start and end to the active date

- **Decision**: When the Add-event flow opens, default the start to the active date (the timeline's `CurrentDate`, or the trip start when no position is established), at a sensible default time, and default the end to one hour after the start. A lane click that carries a specific slot start continues to use that precise start (the traveler clicked an exact time), while the per-leg "+ Add event" and any header add path default to the active date.
- **Rationale**: FR-002/FR-006 require defaulting to the active day with a sensible fallback; the user asked for both start and end to default. `TrackedItemForm` already derives `EndLocal = start + 1h` from the initial start, so the host only needs to pass the active date as the initial start.
- **Alternatives considered**: Default only the start — rejected: the user explicitly asked for start and end. Always use the leg start (today's per-leg behavior) — rejected: it ignores the day the traveler is actually viewing.

## Decision 4: Reactive end time when start changes

- **Decision**: In `TrackedItemForm`, when the traveler changes the start date/time, automatically set the end to one hour after the new start — but only while the traveler has not manually edited the end. Once the end is edited directly, the traveler's end value is preserved and no longer auto-follows the start (consistent with the existing "keep mine" copy-from-leg behavior).
- **Rationale**: The user asked the end to react to one hour after the selected start. Suppressing auto-adjust after a manual end edit prevents silently discarding a deliberate end value, matching FR-003's "keep the traveler's manual choice" intent for dates.
- **Alternatives considered**: Always force end = start + 1h on every start change — rejected: it would overwrite a deliberate end. Only set end on first fill — rejected: the user wants the end to react each time the start changes (until the end is manually set).

## Decision 5: Location as an address, validated as map-capable (chosen default)

- **Decision**: Present the location as an address (label/placeholder guiding an address-style entry) and validate it only when non-empty: it must be a trimmed value of reasonable length that contains at least one letter or digit (i.e. not solely whitespace or punctuation/symbols), so it is plausibly resolvable on a map. Empty remains allowed (location stays optional). Validation is advisory and shown inline via the form's existing validation pattern; it does not attempt real geocoding.
- **Rationale**: The user asked to "validate it's in a location-capable format" without specifying an exact grammar. A lenient, well-defined rule flags clearly non-mappable input (e.g. `"!!!"` or a lone symbol) while accepting real place names and addresses that vary widely by country, satisfying "recommended as an address" without over-constraining. Keeping location optional preserves existing data and FR-012 (no map action when empty).
- **Alternatives considered**:
  - Strict address grammar (require number + street + city) — rejected: place names ("Eiffel Tower") and international addresses would fail; too brittle and not requested.
  - Live geocoding validation — rejected: adds an external dependency and backend call, violating the front-end-only, no-new-infrastructure scope and the spec's assumption of no geocoding accuracy guarantees.

## Decision 6: Globe icon opens the location in an external map

- **Decision**: Render a globe icon button next to the location field (and offer the equivalent action wherever the event location is shown) that opens the entered location text in an external mapping experience in a new browser context (a maps search URL with the URL-encoded location as the query), via a target that is safe against reverse tabnabbing (new tab with `rel="noopener"` semantics). The action is present only when a valid location is entered (FR-011/FR-012) and uses the location text exactly as entered (FR-013).
- **Rationale**: Matches the spec assumption that "mappable" means an actionable shortcut opening a map of the entered text, with no new stored geocoordinate and no embedded map. Opening in a new context lets the traveler return to the event without losing unsaved work (FR-014).
- **Alternatives considered**:
  - Embedded interactive map with a pin — rejected: out of scope per the spec assumptions and adds a dependency.
  - Navigate the app away to a map — rejected: risks losing unsaved event edits (FR-014).

## Decision 7: Item type maps to an inline-SVG icon shown on the timeline

- **Decision**: Introduce a small `TrackedItemIcon` component that maps each known `ItemType` (event, reservation, activity, reminder) to a distinct inline-SVG glyph, with a clear default glyph for unknown/missing types. Render the icon next to the item title on each timeline entry and alongside the type selection in the event form so the association is visible at entry time. The color swatch behavior is unchanged; the icon is an additional type cue.
- **Rationale**: `TimelineItem.ItemType` already exists, so this is presentation-only. Inline SVG matches the app's existing icon approach and needs no new dependency (Constitution V). A default glyph satisfies FR-010 so no entry is ever iconless.
- **Alternatives considered**:
  - Add a Bootstrap Icons / icon-font package — rejected: introduces a new dependency for four glyphs; inline SVG is lighter and consistent with existing patterns.
  - Encode the icon per item in the API/DB — rejected: type already implies the icon; storing it duplicates data and needs contract changes.

## Decision 8: Accessibility and theming

- **Decision**: The globe/map action and type icons expose accessible labels (`aria-label`/`title`), keep decorative SVGs `aria-hidden`, and remain keyboard-operable (the globe is a real `button`/link). All new visuals reuse existing theme CSS variables for light/dark support.
- **Rationale**: Satisfies FR-015 and the spec assumption to reuse current branding/theme, consistent with the accessibility approach established in feature 014.
- **Alternatives considered**: Icon-only affordances without labels — rejected: fails accessibility requirements.

## Open Questions

None. All spec requirements and the user's planning direction map to the confirmed decisions above.
