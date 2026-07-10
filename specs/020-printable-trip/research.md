# Phase 0 Research: Printable Trip View

This feature is a front-end-only projection of data the API already returns. The open questions are all about *how* to render and print, not *what* data to fetch. Each decision below resolves a Technical Context choice.

## 1. Where does the print data come from?

- **Decision**: Reuse the existing `GET /api/trips/{tripId}` endpoint via `ITripApiClient.GetDetailAsync`, which returns a `TripDetail` containing trip metadata, `Legs` (`TripLegDto`), and `TrackedItems` (`TrackedItemDto`). Add **no** new API endpoint, contract, or database change.
- **Rationale**: `TripDetail` already carries every field the printout needs — name, description, start/end dates, `EstimatedCostTotal`, each leg's title/origin/destination/start/end/timezone, and each event's type, title, location, start/end (+ timezone ids), confirmation code, estimated cost, and notes. Building the printout purely from this DTO keeps the feature a small vertical slice and honors the constitution's "small vertical slice" principle without touching Api/Database/Contracts.
- **Alternatives considered**:
  - *A dedicated `GET /api/trips/{tripId}/print` projection*: rejected — adds an endpoint and contract for data already available; no server-side rendering benefit since the browser prints client-rendered HTML.
  - *Server-generated PDF (e.g., a headless renderer)*: rejected — the spec explicitly scopes to browser-native print of an HTML page and "without requiring a separate export tool"; a PDF service adds infrastructure and secrets against Container App Readiness goals.

## 2. How is the app chrome removed for print ("minimal UI chrome")?

- **Decision**: Create a dedicated **`PrintLayout.razor`** that renders only `@Body` (no `NavMenu`, no footer, no theme selector) and assign it to the print page with `@layout PrintLayout`. Additionally add `@media print` CSS that hides the page's own on-screen controls (the Print and Back buttons) so only the document prints.
- **Rationale**: A per-page layout is the idiomatic Blazor way to opt a route out of `MainLayout` (the router's `DefaultLayout`). Combining a chrome-free layout with `@media print` rules guarantees FR-002 (no navigation/menus/action buttons in the output) both on screen and on paper, while still letting the traveler trigger printing and navigate back on screen (FR-011).
- **Alternatives considered**:
  - *Keep `MainLayout` and hide chrome only with `@media print`*: rejected — the on-screen page would still show the full app frame, and print reliability across browsers is worse than simply not rendering chrome.
  - *A separate popup window built in JS*: rejected — loses Blazor routing, auth, and accessibility structure.

## 3. How is a datetime combined with its timezone into one column?

- **Decision**: Format each datetime cell as **`MM/dd/yyyy HH:mm TZ`** using the event/leg's local wall-clock value (`StartLocal` / `EndLocal`) and appending the **timezone id** (`StartTimeZoneId` / `EndTimeZoneId`) as the `TZ` token — e.g. `07/14/2026 09:30 America/New_York`. A pure helper `TripPrintFormatting.FormatDateTimeWithZone(DateTime local, string timeZoneId)` produces the string with `InvariantCulture`. Time is rendered in **24-hour** form (`HH:mm`) to avoid AM/PM ambiguity, matching the request's literal `hh:mm` with no `tt`.
- **Rationale**: The traveler already sees `StartLocal`/`EndLocal` as wall-clock times in the app; reusing those exact values keeps the printout consistent (FR-008, SC-005) while the appended zone token removes ambiguity for multi-timezone trips. Using the timezone id as the token matches the existing convention where trip legs already expose `StartTimeZoneLabel`/`EndTimeZoneLabel` equal to the timezone id (per the leg query aliasing `start_time_zone_id AS "StartTimeZoneLabel"`), so the printout stays consistent with data the rest of the app already carries. `TrackedItemDto` has no label field, so deriving the token from its `StartTimeZoneId`/`EndTimeZoneId` is the only self-contained source and needs no API change.
- **Alternatives considered**:
  - *Short abbreviations (EDT/PST)*: rejected as the default — .NET does not expose reliable, cross-platform zone abbreviations for arbitrary IANA ids, and DST correctness would require extra logic; the timezone id is unambiguous and already used as the leg label. (A friendlier token via `TimezoneOptions` remains an optional future refinement.)
  - *Converting everything to UTC or one zone*: rejected — would misrepresent the schedule the traveler planned and contradicts FR-008.
  - *12-hour `hh:mm tt`*: rejected — the request wrote `hh:mm` without `tt`; 24-hour avoids AM/PM ambiguity in a dense table.

## 4. How is the print document structured (metadata → legs → events)?

- **Decision**: Render a single semantic document: a **metadata block** first (trip name as heading, date range, description, estimated total), then a **table** where each leg is a full-width **row-divider** row (spanning all columns) followed by that leg's event rows. Events with no leg (`TripLegId == null`, if any) render under a trailing "Unassigned" divider. Columns per event: Type, Title, Location, Start, End, Confirmation, Est. Cost, Notes.
- **Rationale**: Matches the requested ordering exactly (metadata first, legs chronological as row dividers, events tabular with one column per collected field) and gives assistive tech a logical reading order via a real `<table>` with `<thead>`/`<caption>`/`scope` (FR-014). A single table with `thead` also lets the browser repeat headers across printed pages for large trips (FR-012).
- **Alternatives considered**:
  - *One nested table per leg*: rejected — repeated column headers per leg add visual noise and complicate page breaks; a single table with divider rows paginates more cleanly.
  - *Card/flex layout instead of a table*: rejected — the request explicitly asks for an HTML table with columns per field.

## 5. Leg and event ordering

- **Decision**: Order legs chronologically by `StartLocal` (tie-break by `SortOrder`, then title) and order events within a leg by `StartLocal` (tie-break by `SortOrder`). Encapsulate in `TripPrintFormatting.OrderLegsChronologically` / `OrderEventsWithinLeg` / `GroupEventsByLeg` so ordering is unit-testable independent of rendering.
- **Rationale**: FR-004 requires legs in chronological order with events grouped and ordered under them; deriving order from `StartLocal` (with `SortOrder` as a stable tie-break) mirrors how the timeline already reasons about item order.
- **Alternatives considered**: Ordering solely by `SortOrder` — rejected because the request specifies *chronological* order; `SortOrder` is retained only as a deterministic tie-break.

## 6. Missing optional details

- **Decision**: For any absent optional field (location, end datetime, confirmation code, estimated cost, notes, description), render an empty cell / omit the element — never an error or placeholder error text.
- **Rationale**: FR-006 requires events to appear with the details they have; optional fields on `TrackedItemDto` are already nullable, so the renderer simply guards each cell.

## 7. Triggering print and returning to the app

- **Decision**: Provide an on-screen **Print** button on the print page that calls a one-line `tripPrint.print()` JS interop wrapping `window.print()`, plus a **Back** link to `/trips/{tripId}`. Both controls are hidden under `@media print`.
- **Rationale**: Keeps printing a deliberate user action (FR-007) and satisfies FR-011 (return without losing place) while ensuring the controls themselves never print (FR-002).
- **Alternatives considered**: Auto-calling `window.print()` on load — rejected as surprising and hostile to review-before-print; the button keeps the traveler in control.

## 8. Entry point from trip details

- **Decision**: Add an owner-only **Print** button in the `TripDetails` header action group that navigates to `/trips/{TripId}/print` (same-tab navigation; the Back link returns).
- **Rationale**: FR-010 requires a clearly labeled action from the trip view; gating on `_trip.IsOwner` aligns the button with the ownership rule (FR-013). Access is still enforced server-side because the print page fetches through the authenticated client.
- **Alternatives considered**: Opening in a new tab — acceptable but same-tab with a Back link is simpler and matches the existing "All trips" link pattern; left as an implementation nicety, not required.

## Summary of resolved unknowns

| Technical Context item | Resolution |
|------------------------|-----------|
| Data source | Existing `GET /api/trips/{tripId}` → `TripDetail`; no new API/DB |
| Chrome removal | New `PrintLayout` (renders `@Body` only) + `@media print` hiding page controls |
| Datetime+TZ column | `MM/dd/yyyy HH:mm {timeZoneId}`, 24-hour, `InvariantCulture`, via pure helper |
| Document structure | Metadata block, then one table with leg row-dividers and per-field event columns |
| Ordering | Legs by `StartLocal` (SortOrder tie-break); events by `StartLocal` within leg |
| Missing optionals | Empty cell / omitted element, never an error |
| Print trigger / return | On-screen Print (`window.print()`) + Back link, both hidden in print CSS |
| Entry point | Owner-only Print button on `TripDetails` → `/trips/{tripId}/print` |
