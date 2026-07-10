# Contract: Print Page Route

The print view is an internal UI contract (a routable Blazor page), not an HTTP API. It defines the route, layout, data source, and observable states.

## Route

- **Path**: `/trips/{TripId:guid}/print`
- **Component**: `TripPrint.razor` (`TripPlanner.Web/Components/Pages/Trips/`)
- **Authorization**: `@attribute [Authorize]` — anonymous users get the existing sign-in-required experience.
- **Layout**: `@layout PrintLayout` — a chrome-free layout that renders only `@Body` (no `NavMenu`, footer, or theme selector).
- **Render mode**: `@rendermode InteractiveServer` (consistent with `TripDetails`).

## Data source

- Loads via `ITripApiClient.GetDetailAsync(TripId)` — the **existing** `GET /api/trips/{tripId}` endpoint.
- No new endpoint, DTO, or database access is introduced.

## States

| State | Condition | Rendered output |
|-------|-----------|-----------------|
| Loading | Fetch in progress | Minimal loading indicator (no chrome). |
| Denied / Not found | `GetDetailAsync` returns `null` (non-owner or missing trip) | Existing not-found/denied message, chrome-free. Satisfies FR-013. |
| Empty | `TripDetail` has no legs and no tracked items | Metadata block + a clear "no legs or events" message. Satisfies FR-009. |
| Populated | `TripDetail` has legs and/or events | Metadata block + document table (legs as dividers, events as rows). Satisfies FR-001/003/004/005. |

## On-screen controls (hidden in print output via `@media print`)

| Control | Behavior | Requirement |
|---------|----------|-------------|
| **Print** button | Calls `tripPrint.print()` → `window.print()` | FR-007 |
| **Back** link | Navigates to `/trips/{TripId}` | FR-011 |

Both controls MUST be excluded from the printed output (they live inside a `.tp-print-actions` region styled `display:none` under `@media print`), so the printout contains only trip content (FR-002).

## Entry point (on `TripDetails.razor`)

- An owner-only **Print** button (gated on `_trip.IsOwner`) in the header action group navigates to `/trips/{TripId}/print`. Satisfies FR-010; aligns entry with ownership.

## Accessibility

- The page exposes a single `<h1>` (trip name) for `FocusOnNavigate`.
- The document body is a semantic `<table>` with `<caption>`, `<thead>` column headers using `scope="col"`, and leg divider rows using a spanning header cell — giving a logical reading order for assistive tech (FR-014).
