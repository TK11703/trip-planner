# Phase 0 Research: Trip Location Maps

This feature has no open `NEEDS CLARIFICATION` items in the spec. The user's plan input fixed the key decisions (Bing/Google choice with Bing default; a launchable-only-when-locations-exist trip map that fits bounds then allows pan/zoom). The research below records the technical decisions and the alternatives considered.

## Decision 1 — Map output preference is a profile-owned scalar

**Decision**: Add a single `MapProvider` field to the user profile with allowed values `Bing` and `Google`, defaulting to `Bing`. It flows through the existing `GET/PUT /api/profile` surface and is persisted as a new `map_provider` column on `users`.

**Rationale**:
- Mirrors the existing profile model, where scalars like `TimeZoneId` live directly on `UserProfileResponse`/`UpdateUserProfileRequest` and columns on `users`. No new preference table is needed.
- The user explicitly scoped the choice to Bing and Google, with Bing default — a two-value scalar is the simplest faithful model.
- A per-user default (not per-trip or per-event) matches "a profile configuration entry."

**Alternatives considered**:
- *A dedicated `MapPreferences` record (like `NotificationPreferences`)*: rejected as over-modeled for a single value; a scalar keeps parity with `TimeZoneId`.
- *Per-trip or per-event map choice*: rejected — the user asked for a profile-level default.
- *Free-form provider string*: rejected — validation to a known set (`Bing`, `Google`) keeps the globe URL builder total and safe.

## Decision 2 — Provider-aware globe URL, unchanged open behavior

**Decision**: Keep the globe action opening the entered address in a new browser context (`target="_blank" rel="noopener noreferrer"`), but choose the destination URL from the profile's `MapProvider`:
- Bing: `https://www.bing.com/maps?q={escaped address}` (current behavior).
- Google: `https://www.google.com/maps/search/?api=1&query={escaped address}`.

The current provider is supplied to `TrackedItemForm` by a small scoped `MapPreferenceProvider` that reads the profile once per circuit (via the existing `IProfileApiClient`) and caches it, falling back to `Bing` if the profile can't be read.

**Rationale**:
- Reuses the existing, already-tested globe markup and only swaps the URL base — minimal, low-risk change.
- Both providers accept a free-text address query, matching the app's free-text location model; no coordinates are needed for the single-location action.
- A scoped cache avoids fetching the profile on every keystroke/form open while keeping the form decoupled from profile-loading concerns.

**Alternatives considered**:
- *Injecting `IProfileApiClient` directly into the form and fetching each open*: rejected to avoid repeated calls and to keep the fallback logic in one place.
- *Passing the provider as a parameter from every host*: rejected — `TrackedItemForm` is hosted in multiple places; a scoped provider centralizes it.
- *Google `maps.google.com/?q=` legacy URL*: rejected in favor of the documented `search/?api=1&query=` Maps URL scheme.

## Decision 3 — Render the built-in map with Leaflet + OpenStreetMap tiles

**Decision**: Render the in-app interactive trip map with **Leaflet** using **OpenStreetMap** raster tiles, bundled under `wwwroot/lib/leaflet` and driven by a new `tripMap.js` interop module. Leaflet's `map.fitBounds(bounds)` frames all markers on first launch; default drag/scroll/zoom controls then let the traveler pan and zoom freely.

**Rationale**:
- **Keeps secrets server-side.** OSM tiles require no API key, so nothing sensitive reaches the browser. The Azure Maps subscription key remains server-side (used only for geocoding — Decision 4), consistent with feature 018 and Constitution Principle V.
- Leaflet is a small, dependency-free, widely used map control with first-class `fitBounds`, markers, and popups — a direct match for "zoom to fit … then allow the user to move and zoom."
- Bundling under `wwwroot/lib` mirrors how Bootstrap is already vendored, keeping the container self-contained (no hard CDN runtime dependency) and CSP-friendly.

**Alternatives considered**:
- *Azure Maps Web SDK in the browser*: rejected for now because secret-safe browser auth requires either exposing the subscription key (unacceptable) or standing up an Entra token endpoint + managed-identity role assignment on an Entra-enabled Azure Maps account — significant infra for this slice. Recorded as a future option if a single-provider stack is later desired.
- *Google Maps JS SDK*: rejected — requires a browser-exposed, billable API key and key restriction management; conflicts with the server-side-secret constraint.
- *Static map image*: rejected — the user requires interactive pan/zoom.

**Operational note**: OpenStreetMap's public tile server requires visible attribution (Leaflet adds it by default) and is subject to a fair-use policy; the tile URL template is a single configurable constant so the tile source can be swapped for a keyed/hosted provider if volume grows.

## Decision 4 — Geocode free-text locations server-side, on demand

**Decision**: Resolve each locatable event's free-text `Location` to coordinates **server-side**, reusing the existing Azure Maps Search integration. Add a geocoding capability to the Places feature (a method that returns the top result's `position` lat/lon for a query) and a new authenticated, trip-owner-scoped endpoint `GET /api/trips/{tripId}/map` that returns the resolved points. Geocoding happens when the map opens; coordinates are **not** persisted.

**Rationale**:
- Reuses the already-wired Azure Maps client, HTTP client, and `AzureMaps:SubscriptionKey` config — no new provider or secret.
- No schema change for coordinates keeps parity with feature 018's "no stored geocoordinate field" assumption and avoids a geocode-on-write migration of existing data.
- Doing it server-side keeps the key off the browser and lets the endpoint enforce trip ownership before returning any location data.
- Distinct location texts are deduplicated before geocoding, so the cost is bounded by the number of unique locations on the trip (typically small).

**Alternatives considered**:
- *Persist coordinates on the tracked item*: rejected for this version (added schema, backfill, and write-path geocoding); noted as a future optimization if map opens become hot.
- *Client-side geocoding*: rejected — would expose the key or require a second browser-side provider.
- *Batch geocoding API*: not needed at this scale; sequential/parallel single lookups over distinct texts are sufficient and simpler to reason about.

## Decision 5 — Launchability decided on the client from existing data

**Decision**: The trip details "View map" button is enabled only when at least one of the trip's already-loaded tracked items has a map-capable `Location` value (non-empty, contains a letter/digit, ≤200 chars — the same rule the globe uses). No server round-trip is needed to decide launchability.

**Rationale**:
- `TripDetail` already carries `TrackedItems` with `Location`, so the button state is computable immediately on the page.
- Matches the requirement "if there are no location data entered, then the map should not be launchable" using presence of location text as the trigger.
- If the button is enabled but Azure Maps resolves none of the texts (e.g., unconfigured/unavailable), the opened modal shows a clear empty/partial state rather than failing — this is the graceful-degradation path (FR-006, spec edge cases).

**Alternatives considered**:
- *Deciding launchability from geocoded results*: rejected — it would require geocoding before the user opens the map and would hide the button whenever Azure Maps is down, even though locations exist.

## Decision 6 — Marker identity enables "open the event" (Story 3)

**Decision**: Each `TripMapLocation` carries the `TrackedItemId` and a human label (event title + location). Markers render a popup with the title; selecting it can route back into the existing event-detail/selection flow the timeline already uses. Near-duplicate coordinates are kept as separate markers (optionally slightly spread) so each event stays selectable.

**Rationale**:
- Reuses the existing item-selection path in `TripDetails`, so opening an event from the map adds little new surface.
- Carrying the id (not just coordinates) is what makes the map a navigation aid rather than a static picture (Story 3, FR-013/FR-014 of the spec).

**Alternatives considered**:
- *Clustering overlapping markers*: deferred — unnecessary at expected trip scale; a simple keep-all approach preserves per-event selectability (FR-015) without extra dependencies.

## Resolved unknowns summary

| Question | Resolution |
|----------|------------|
| Which providers for the single-location output? | Bing and Google only; Bing is the default (per user input). |
| Where is the preference stored? | `users.map_provider` scalar; surfaced via the existing profile GET/PUT. |
| How is the built-in map rendered without exposing a key? | Leaflet + OpenStreetMap tiles in the browser; no key needed. |
| How are free-text locations placed on the map? | Server-side geocoding via the existing Azure Maps Search client; on demand, not persisted. |
| When is the trip map launchable? | Client-side, when ≥1 tracked item has map-capable location text. |
| How does zoom-to-fit + free navigation work? | Leaflet `fitBounds` on first render; native pan/zoom afterward. |
