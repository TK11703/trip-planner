# Contract: Built-In Map Interop (`tripMap.js` + `TripMapModal`)

Defines the front-end contract for the in-app interactive trip map rendered with Leaflet/OpenStreetMap.

## Component: `TripMapModal.razor` (`TripPlanner.Web/Components/Trips/`)

- A large Bootstrap modal (`modal-lg`/`modal-xl`) hosting a full-width map container element.
- Opened from `TripDetails` via a "View map" button that is **enabled only when** the loaded trip has ≥1 tracked item with a map-capable `Location` (client-side check; no server call to decide).
- On open:
  1. Calls `ITripApiClient.GetTripMapAsync(tripId)`.
  2. If `Locations` is non-empty, initializes the Leaflet map via JS interop and plots markers.
  3. If `Locations` is empty (all unresolved / provider unavailable), shows a clear empty state inside the modal instead of a map.
- On close: disposes the map instance (JS `dispose`) to free the Leaflet map and listeners.

## Launchability rule (`TripDetails`)

```text
CanOpenMap = _trip is not null
          && _trip.TrackedItems.Any(i => IsLocationMappable(i.Location))
```

`IsLocationMappable` reuses the same rule as the globe (non-empty, contains a letter/digit, ≤200 chars).

## JS module: `tripMap.js` (`TripPlanner.Web/wwwroot/js/`)

Exported functions called via `IJSRuntime`/module import:

```js
// Initialize a Leaflet map into `element`, plot markers, fit bounds, enable pan/zoom.
// points: [{ trackedItemId, title, location, latitude, longitude }]
// dotNetRef: optional DotNetObjectReference to notify on marker activation.
// returns an opaque handle id.
export function init(element, points, dotNetRef) { ... }

// Tear down the map created by init.
export function dispose(handleOrElement) { ... }
```

Behavior requirements:
- Create the map with OpenStreetMap tiles: `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png` with the standard `© OpenStreetMap contributors` attribution. The tile URL/attribution are single constants (swappable tile source).
- Add one marker per point; bind a popup showing `title` (and `location` as subtext).
- **First launch**: compute a bounds from all marker coordinates and call `map.fitBounds(bounds, { padding })`. For a single point, use `setView([lat, lon], reasonableZoom)`.
- After fit, leave Leaflet's default drag + scroll/zoom controls active so the traveler can move and zoom freely (no re-fitting on interaction).
- When a marker/popup action is activated, invoke `dotNetRef.invokeMethodAsync("OnMarkerActivated", trackedItemId)` so the modal can open/select that event (Story 3). This callback is optional; markers still render without it.

## Assets

- Leaflet CSS/JS bundled under `wwwroot/lib/leaflet/` (mirrors the vendored Bootstrap). The modal ensures the Leaflet stylesheet is present when the map is shown.

## Accessibility & theming

- The map container has an accessible label (e.g., `aria-label="Map of trip locations"`).
- The "View map" button exposes its disabled reason via `title` when no locations exist.
- Marker popups are keyboard-reachable; the modal traps focus per existing modal behavior.
- Map chrome respects the app's light/dark theme where feasible (container/border styling in `app.css`).

## Tests

- bUnit: "View map" is disabled when no tracked item has a location; enabled when one does. Empty `Locations` renders the empty state (interop init not called).
- E2E (Playwright): for a trip with locations, opening the modal shows a map with markers framing all points; the map remains pannable/zoomable.
