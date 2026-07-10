# Quickstart: Trip Location Maps

A runnable validation guide for the three capabilities in this feature. For data shapes and rules, see [data-model.md](data-model.md) and [contracts/](contracts/).

## Prerequisites

- .NET 10 SDK and the repo building normally.
- Local PostgreSQL provided by Aspire (the AppHost applies `TripPlanner.Database` migrations, including `009_user_profile_map_provider.sql`).
- An Azure Maps subscription key set for the API so geocoding/typeahead work: `AzureMaps:SubscriptionKey` via user-secrets on `TripPlanner.Api` (or environment). Without it, the built-in map degrades to an empty state (still valid to test the disabled/empty paths).
- A signed-in user with at least one trip.

## Run

```pwsh
# From the repo root — starts API, Web, and PostgreSQL with hot reload
dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Open the Web app from the Aspire dashboard and sign in.

## Scenario 1 — Choose the default mapping tool (P2 / FR-007, FR-008)

1. Go to the **Profile** page.
2. Find the new **Default map** setting. Confirm it shows **Bing** selected by default for a profile that has never set it.
3. Change it to **Google** and save.
4. Reload the profile — the selection persists as **Google**.

**Expected**: The profile round-trips `MapProvider`; unknown values coerce to `Bing` (see [contracts/profile-map-preference.md](contracts/profile-map-preference.md)).

## Scenario 2 — Location button opens the chosen map (P2 / FR-009–FR-011)

1. Open a trip, add or edit an event, and enter an address in **Location** (e.g., `Space Needle, Seattle`).
2. With the profile set to **Bing**, click the globe button.
   - **Expected**: a new tab opens `https://www.bing.com/maps?q=Space%20Needle...`.
3. Change the profile **Default map** to **Google**, return to the event, click the globe again.
   - **Expected**: a new tab opens `https://www.google.com/maps/search/?api=1&query=Space%20Needle...`.
4. Clear the location — the globe button is disabled (unchanged behavior).

**Expected**: The globe destination follows the profile provider; the address is the free text as entered. See [contracts/map-output-behavior.md](contracts/map-output-behavior.md).

## Scenario 3 — Built-in trip map: not launchable with no locations (P1 / FR-005)

1. Open a trip that has **no** event locations entered.
2. Locate the **View map** action on the trip details page.

**Expected**: The **View map** button is **disabled** (with a tooltip explaining a location is needed). No modal opens.

## Scenario 4 — Built-in trip map: launch, fit, then navigate (P1 / FR-001–FR-004)

1. Open a trip that has several events across different places, each with a **Location**.
2. Click **View map**.

**Expected**:
- A large modal opens with an interactive map.
- Every event with a resolvable location appears as a marker.
- On first launch the map is framed so **all** markers are visible at once.
- You can drag to pan and scroll/buttons to zoom in and out freely afterward.
- Events without a location do not appear.

## Scenario 5 — Graceful degradation (FR-006, edge cases)

1. With Azure Maps **unconfigured** (empty `AzureMaps:SubscriptionKey`), open a trip that has location text and click **View map** (the button is still enabled because location text exists).
   - **Expected**: the modal opens and shows a clear **empty state** rather than an error.
2. With Azure Maps configured but one event's location is gibberish, open the map.
   - **Expected**: the resolvable markers still render; the unresolved one is silently omitted; the map does not fail.

## Scenario 6 — Identify and open an event from a marker (P3 / FR-013, FR-014)

1. On the built-in map, select a marker.

**Expected**: the marker's popup identifies the event (its title/location); choosing it opens/selects that event's details. See [contracts/built-in-map-interop.md](contracts/built-in-map-interop.md).

## Automated tests

```pwsh
# API: profile map-provider round-trip/validation + trip-map endpoint (owner, empty, unresolved, not-configured)
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj

# Database: map_provider persistence
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj

# Web (bUnit): profile select, provider-aware globe URL, View-map enable/disable, modal empty state
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

# E2E (Playwright): default-map drives globe; built-in map opens with markers
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

## Success check

- [ ] Profile shows a **Default map** setting defaulting to Bing; Google round-trips.
- [ ] Globe opens Bing or Google per the profile; address is the entered text.
- [ ] **View map** is disabled when the trip has no locations, enabled otherwise.
- [ ] The built-in map fits all markers on first open, then pans/zooms freely.
- [ ] Unresolved/unconfigured cases degrade to a partial/empty map, never an error.
- [ ] The Azure Maps key is never sent to the browser (only OSM tiles load client-side).
