# Implementation Plan: Trip Location Maps

**Branch**: `019-trip-location-maps` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-trip-location-maps/spec.md`

## Summary

Give travelers control over how a single location opens and add an in-app map of a whole trip, building on the existing location "globe" action (feature 018) and the existing server-side Azure Maps integration:

1. **Profile default mapping tool (map output preference).** Add a profile setting that lets the traveler choose their default mapping tool. The choices are **Bing** and **Google**, with **Bing** as the default. The setting is persisted on the user profile.
2. **Location button honors the preference.** When the traveler clicks the location (globe) button in the event details form/modal, the entered address opens in **Bing Maps** or **Google Maps** according to their profile choice, in a new browser context (as today).
3. **Built-in trip map modal.** The trip details page gains a "View map" action that opens a large modal containing an in-app, interactive map. Every event location entered on the trip's legs is plotted as a marker. If no location data has been entered anywhere on the trip, the action is disabled (the map is not launchable). When launchable, the map zooms to fit all locations within the visible window on first launch, and the traveler can then freely pan and zoom.

Rendering the built-in interactive map uses **Leaflet with OpenStreetMap tiles** (a front-end asset — no browser-exposed key), while resolving each event's free-text location to coordinates is done **server-side** through a new authenticated, trip-owner-scoped endpoint that reuses the existing Azure Maps Search integration. The Azure Maps subscription key stays server-side and environment-driven, consistent with feature 018.

This feature touches all four projects as small vertical slices: a persisted profile field (`TripPlanner.Database`, `TripPlanner.Api`, `TripPlanner.Contracts`), the profile UI and the globe action (`TripPlanner.Web`), a new trip-map geocoding endpoint (`TripPlanner.Api`), and a new built-in map modal with a Leaflet interop module (`TripPlanner.Web`).

## Technical Context

**Language/Version**: C# on .NET 10 (Blazor Web App, Interactive Server render mode; ASP.NET Core Minimal APIs)

**Primary Dependencies**: Blazor (`TripPlanner.Web`), Minimal APIs (`TripPlanner.Api`), Dapper/Npgsql (`TripPlanner.Database`), shared DTOs (`TripPlanner.Contracts`), existing `IProfileApiClient`/`ITripApiClient`, existing `AzureMapsPlaceSuggestionLookup` (Azure Maps Search), Bootstrap 5 modal patterns already used in `TripDetails`. New front-end dependency: **Leaflet** (interactive map control + OpenStreetMap tiles), bundled under `wwwroot/lib/leaflet` alongside the existing bundled Bootstrap.

**Storage**: PostgreSQL. One new column on `users`: `map_provider text NOT NULL DEFAULT 'Bing'`. No coordinate persistence is introduced — event locations remain free text and are geocoded on demand when the built-in map opens (consistent with feature 018's "no stored geocoordinate field" assumption).

**Testing**: `TripPlanner.Api.Tests` (profile map-provider round-trip + validation; trip-map geocoding endpoint incl. ownership, empty, unresolved, and not-configured cases), `TripPlanner.Database.Tests` (profile map_provider persistence), `TripPlanner.Web.Tests` (bUnit: profile map select, globe URL by provider, "View map" enable/disable, map modal empty state), `TripPlanner.E2E.Tests` (Playwright: set default map → globe opens the chosen provider; open the built-in map for a trip with locations)

**Target Platform**: Modern evergreen browsers rendering the Blazor Web front end; API and database deployable as containers to Azure Container Apps via Aspire

**Project Type**: Web application (Blazor front end + Minimal API + PostgreSQL + shared contracts)

**Performance Goals**: The globe action and the profile read are immediate user actions. Opening the built-in map issues one server call that geocodes the trip's distinct locatable events (bounded by the trip's event count, deduplicated by location text); the map renders and fits bounds client-side without per-frame work.

**Constraints**: Keep the Azure Maps subscription key server-side and environment-driven (`AzureMaps:SubscriptionKey`); never expose it to the browser. Preserve existing globe behavior (opens in a new context with `rel="noopener noreferrer"`). Respect existing trip ownership on the new map endpoint. Keep the built-in map, its markers, and the map-preference control keyboard-operable with accessible labels and consistent with the current light/dark theme and branding. Degrade gracefully when Azure Maps is unconfigured/unavailable (empty or partial map) and when a chosen external provider is unreachable (open the entered address anyway).

**Scale/Scope**: One profile scalar preference (two allowed values), one new read-only trip-map endpoint, one new front-end map modal + interop module. A single trip's locatable events (typically a handful to a few dozen), geocoded on demand.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Visualizes and acts on the locations of trip events; strictly within itinerary planning. |
| II. .NET Application Stack | PASS | Blazor front end + Minimal API on .NET 10; Aspire orchestration untouched. Leaflet is a front-end static asset, not a stack change. |
| III. Minimal API Vertical Slices | PASS | Adds a `GET /api/trips/{tripId}/map` vertical slice and extends the existing profile slice; requests/DTOs/handlers colocated by feature. |
| IV. PostgreSQL with Dapper | PASS | New `map_provider` column added via a SQL migration and read/written with Dapper in `TripPlanner.Database`; no EF. |
| V. Container App Readiness | PASS | Azure Maps key stays server-side and environment-driven; Leaflet/OSM tiles are static assets (attribution shown, tile source swappable); no local-only assumptions. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 keeps contracts in shared DTOs, data access as Dapper SQL files in the database project, the geocoding endpoint as a Minimal API slice reusing the existing Azure Maps client, and all UI (profile select, provider-aware globe, map modal, Leaflet interop) inside `TripPlanner.Web`. No new projects, no browser-exposed secrets, no coordinate persistence.

## Project Structure

### Documentation (this feature)

```text
specs/019-trip-location-maps/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── profile-map-preference.md   # Profile map_provider contract + GET/PUT surface
│   ├── trip-map-endpoint.md        # GET /api/trips/{tripId}/map response + rules
│   ├── map-output-behavior.md      # Provider-aware globe URL behavior (Bing/Google)
│   └── built-in-map-interop.md     # tripMap.js Leaflet interop contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   ├── Profile/
│   │   └── UserProfileContracts.cs         # Add MapProvider to UserProfileResponse + UpdateUserProfileRequest; add MapProviders constants (Bing default)
│   └── Trips/
│       └── TripMapContracts.cs             # (new) TripMapResponse + TripMapLocation
├── TripPlanner.Database/
│   ├── Scripts/Schema/
│   │   └── 009_user_profile_map_provider.sql   # (new) ALTER users ADD COLUMN map_provider text NOT NULL DEFAULT 'Bing'
│   ├── Scripts/Queries/UserProfiles/
│   │   └── GetUserProfile.sql               # Select map_provider
│   ├── Scripts/Commands/UserProfiles/
│   │   ├── UpdateUserProfile.sql            # Set map_provider
│   │   └── EnsureUserProfileFromClaims.sql  # Return map_provider on ensure
│   └── UserProfiles/
│       └── UserProfileRepository.cs         # Map the new column
├── TripPlanner.Api/
│   ├── Features/UserProfiles/
│   │   ├── GetProfileEndpoint.cs            # Include MapProvider
│   │   ├── UpdateProfileEndpoint.cs         # Persist MapProvider
│   │   └── UserProfileValidator.cs          # Validate/normalize map provider to {Bing, Google}, default Bing
│   ├── Features/Places/
│   │   └── PlaceSuggestionLookup.cs         # Add geocode-to-coordinates capability (reuses Azure Maps Search)
│   └── Features/TripMaps/                   # (new vertical slice)
│       ├── GetTripMapEndpoint.cs            # GET /api/trips/{tripId}/map (auth + owner-scoped)
│       └── TripMapEndpointRouteBuilderExtensions.cs
└── TripPlanner.Web/
    ├── Components/Pages/
    │   └── Profile.razor                    # "Default map" select (Bing/Google)
    ├── Components/Pages/Trips/
    │   └── TripDetails.razor                # "View map" button (enabled only when a location exists) + host the map modal
    ├── Components/Trips/
    │   └── TripMapModal.razor               # (new) large modal hosting the Leaflet map
    ├── Components/TripItems/
    │   └── TrackedItemForm.razor            # Globe URL chosen by profile map provider (Bing/Google)
    ├── Features/Trips/
    │   └── TripApiClient.cs                 # Add GetTripMapAsync
    ├── Features/Maps/
    │   └── MapPreferenceProvider.cs         # (new) scoped cache of the profile's map provider for the globe action
    └── wwwroot/
        ├── js/
        │   └── tripMap.js                   # (new) init Leaflet, add markers, fitBounds, allow pan/zoom
        ├── lib/leaflet/                     # (new) bundled Leaflet CSS/JS assets
        └── css/
            └── app.css                      # Map modal + marker styling

tests/
├── TripPlanner.Api.Tests/                   # Profile map-provider round-trip/validation; trip-map endpoint (owner, empty, unresolved, not-configured)
├── TripPlanner.Database.Tests/              # map_provider persistence
├── TripPlanner.Web.Tests/                   # bUnit: profile select, provider-aware globe URL, View-map enable/disable, modal empty state
└── TripPlanner.E2E.Tests/                   # Playwright: default-map choice drives globe; built-in map opens with markers
```

**Structure Decision**: Web application with coordinated contracts, database, API, and Blazor changes delivered as independent vertical slices. The **map output preference** is a profile-owned scalar (`MapProvider`) that flows through the existing profile GET/PUT surface and is consumed by the globe action via a small scoped `MapPreferenceProvider` cache so `TrackedItemForm` picks the Bing or Google URL. The **built-in map** is a new read-only `GET /api/trips/{tripId}/map` slice that geocodes the trip's locatable events server-side (reusing the Azure Maps client, key stays server-side) and a new `TripMapModal` that renders markers with Leaflet/OSM, fits bounds on first launch, and then allows free pan/zoom. Launchability is decided on the client from the already-loaded `TrackedItemDto.Location` values, so the button is disabled when the trip has no location data.

## Complexity Tracking

> No constitution violations. Section intentionally left empty.
