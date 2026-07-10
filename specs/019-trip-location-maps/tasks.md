---
description: "Task list for Trip Location Maps"
---

# Tasks: Trip Location Maps

**Input**: Design documents from `/specs/019-trip-location-maps/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included. The plan specifies test coverage across `TripPlanner.Api.Tests`, `TripPlanner.Database.Tests`, `TripPlanner.Web.Tests`, and `TripPlanner.E2E.Tests`, matching the repository convention (feature 018). Test tasks are listed per story and should be written/updated alongside the implementation of that story.

**Organization**: Tasks are grouped by user story (from spec.md) so each story can be implemented, tested, and delivered independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 (built-in trip map), US2 (map output preference), US3 (open event from marker)
- Every task includes an exact file path

## Story → priority map

- **US1 (P1) 🎯 MVP**: See the whole trip on a built-in map
- **US2 (P2)**: Choose which map opens a location (profile default: Bing/Google, Bing default)
- **US3 (P3)**: Identify and open an event from a map point

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline verification and one-time asset acquisition.

- [x] T001 Confirm the solution builds and tests pass before changes: run `dotnet build TripPlanner.slnx` from the repo root and note the baseline.
- [x] T002 [P] Vendor Leaflet into `src/TripPlanner.Web/wwwroot/lib/leaflet/` (Leaflet `leaflet.css`, `leaflet.js`, and marker image assets), mirroring how Bootstrap is vendored under `wwwroot/lib/`. Keep the tile URL/attribution out of the bundle (set in `tripMap.js`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-story prerequisites that must precede all user stories.

**Note**: This feature has no cross-story blocking work beyond Setup. Each user story below is self-contained (US1 = new map endpoint + Leaflet UI; US2 = profile preference; US3 extends US1). After Phase 1, stories may proceed in parallel, subject to the within-story ordering. No tasks in this phase.

**Checkpoint**: Foundation ready — user story implementation can begin.

---

## Phase 3: User Story 1 - See the Whole Trip on a Built-In Map (Priority: P1) 🎯 MVP

**Goal**: A "View map" action on the trip details page opens a large modal with an interactive map that plots every event location on the trip, framed to fit all points on first launch, then freely pan/zoomable. Disabled when the trip has no location data.

**Independent Test**: Open a trip with several located events, click **View map**, confirm all locatable events appear as markers framed in view and the map pans/zooms; confirm a trip with no locations cannot launch the map (button disabled) and that an all-unresolved case shows an empty state.

### Tests for User Story 1

- [x] T003 [P] [US1] Geocoder unit tests (parse `position`, null on no-result/failure/blank/not-configured) in `tests/TripPlanner.Api.Tests/Places/AzureMapsPlaceGeocoderTests.cs`.
- [x] T004 [P] [US1] Trip-map endpoint tests (owner returns resolved points; unresolved texts omitted; shared text fans out; non-owner/unknown trip → not found; not-configured/provider-failure → empty/partial, 200 OK) in `tests/TripPlanner.Api.Tests/TripMaps/GetTripMapEndpointTests.cs`.
- [x] T005 [P] [US1] bUnit: "View map" disabled when no tracked item has a location and enabled when one does; empty `Locations` renders the empty state without initializing interop, in `tests/TripPlanner.Web.Tests/Trips/TripMapModalTests.cs`.
- [x] T006 [P] [US1] E2E (Playwright): open the built-in map for a trip with locations and assert markers render and the map is interactive, in `tests/TripPlanner.E2E.Tests/Trips/TripMapTests.cs`.

### Implementation for User Story 1

- [x] T007 [P] [US1] Add `TripMapResponse` and `TripMapLocation` records in `src/TripPlanner.Contracts/Trips/TripMapContracts.cs` (per [contracts/trip-map-endpoint.md](contracts/trip-map-endpoint.md)).
- [x] T008 [US1] Add `IPlaceGeocoder` + `GeoPoint` and implement `GeocodeAsync` (Azure Maps Search fuzzy, `limit=1`, read `results[0].position.lat/lon`; returns null on no-result/non-success/missing-key/exception) on the existing lookup in `src/TripPlanner.Api/Features/Places/PlaceSuggestionLookup.cs`, reusing the `azuremaps` HttpClient and `subscription-key` header.
- [x] T009 [US1] Implement `GetTripMapEndpoint` (`GET /api/trips/{tripId:guid}/map`, authenticated, trip-owner scoped: select map-capable locations, dedupe distinct texts, geocode once, fan results to events, omit unresolved, return possibly-empty list) and `TripMapEndpointRouteBuilderExtensions` in `src/TripPlanner.Api/Features/TripMaps/`.
- [x] T010 [US1] Register the geocoder in DI and map the new endpoint in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs` (and the endpoint route registration in `Program.cs`), reusing the existing Azure Maps HttpClient registration.
- [x] T011 [US1] Add `GetTripMapAsync(Guid tripId, CancellationToken)` to `ITripApiClient` and implement it in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`.
- [x] T012 [P] [US1] Create the Leaflet interop module `src/TripPlanner.Web/wwwroot/js/tripMap.js` with `init(element, points, dotNetRef)` (OSM tiles + attribution constant, one marker per point, `fitBounds` on first render, `setView` for a single point, native pan/zoom left enabled) and `dispose(handle)` (per [contracts/built-in-map-interop.md](contracts/built-in-map-interop.md)).
- [x] T013 [US1] Create `src/TripPlanner.Web/Components/Trips/TripMapModal.razor`: on open call `GetTripMapAsync`, render the Leaflet map via `tripMap.js` when `Locations` is non-empty, show a clear empty state otherwise, and dispose the map on close (depends on T011, T012).
- [x] T014 [US1] Add the **View map** button to `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, enabled only when `_trip.TrackedItems.Any(i => IsLocationMappable(i.Location))` (with a disabled-reason tooltip), and host `TripMapModal` (depends on T013).
- [x] T015 [P] [US1] Add map modal container and marker/empty-state styles in `src/TripPlanner.Web/wwwroot/css/app.css`.

**Checkpoint**: The built-in trip map is fully functional and independently testable (MVP).

---

## Phase 4: User Story 2 - Choose Which Map Opens a Location (Priority: P2)

**Goal**: A profile "Default map" setting (Bing or Google, Bing default) is persisted, and clicking the location globe in the event form opens the entered address in the chosen provider.

**Independent Test**: Set the profile default to Google and confirm the globe opens `google.com/maps/search/?api=1&query=...`; switch to Bing and confirm it opens `bing.com/maps?q=...`; a new profile defaults to Bing.

### Tests for User Story 2

- [x] T016 [P] [US2] API tests: `GET /api/profile` returns `Bing` for a new profile; `PUT` `Google` round-trips; `PUT` of an unknown value stores `Bing`; unrelated updates preserve `MapProvider`, in `tests/TripPlanner.Api.Tests/UserProfiles/UserProfileMapProviderTests.cs`.
- [x] T017 [P] [US2] Database test: `map_provider` persists and reads back canonically, in `tests/TripPlanner.Database.Tests/UserProfiles/UserProfileMapProviderPersistenceTests.cs`.
- [x] T018 [P] [US2] bUnit: with profile Bing (or unreadable) the globe href starts with `https://www.bing.com/maps?q=`; with Google it starts with `https://www.google.com/maps/search/?api=1&query=`; query is the escaped trimmed location; disabled state unchanged, in `tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormMapProviderTests.cs`.
- [x] T019 [P] [US2] E2E (Playwright): change the profile default map and confirm the event globe opens the chosen provider, in `tests/TripPlanner.E2E.Tests/Profile/MapProviderTests.cs`.

### Implementation for User Story 2

- [x] T020 [P] [US2] Add `MapProvider` to `UserProfileResponse` and `UpdateUserProfileRequest`, and add the `MapProviders` constants/`Normalize` helper (Bing default) in `src/TripPlanner.Contracts/Profile/UserProfileContracts.cs` (per [contracts/profile-map-preference.md](contracts/profile-map-preference.md)). Update all positional call sites.
- [x] T021 [US2] Add migration `src/TripPlanner.Database/Scripts/Schema/009_user_profile_map_provider.sql` with idempotent `ALTER TABLE users ADD COLUMN IF NOT EXISTS map_provider text NOT NULL DEFAULT 'Bing'`.
- [x] T022 [US2] Select/write the new column: update `Scripts/Queries/UserProfiles/GetUserProfile.sql` (`map_provider AS MapProvider`), `Scripts/Commands/UserProfiles/UpdateUserProfile.sql` (`map_provider = @MapProvider`), `Scripts/Commands/UserProfiles/EnsureUserProfileFromClaims.sql` (return `map_provider AS MapProvider`), and the mapping in `src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs`.
- [x] T023 [US2] Surface and persist the field in the API: include `MapProvider` in `src/TripPlanner.Api/Features/UserProfiles/GetProfileEndpoint.cs`, persist `MapProviders.Normalize(...)` in `UpdateProfileEndpoint.cs`, and normalize (never reject) in `UserProfileValidator.cs` (depends on T020, T022).
- [x] T024 [P] [US2] Add `IMapPreferenceProvider`/`MapPreferenceProvider` (scoped, caches the profile's provider via `IProfileApiClient`, Bing fallback, `Invalidate()`) in `src/TripPlanner.Web/Features/Maps/MapPreferenceProvider.cs` and register it in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`.
- [x] T025 [US2] Make the globe URL provider-aware in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`: read the provider from `IMapPreferenceProvider` and build the Bing or Google URL (escaped trimmed location); keep `target="_blank" rel="noopener noreferrer"` and the existing enable/disable rule (depends on T024).
- [x] T026 [US2] Add the **Default map** select (Bing/Google) to `src/TripPlanner.Web/Components/Pages/Profile.razor`, bind it into the update request, and call `IMapPreferenceProvider.Invalidate()` after a successful save (depends on T020, T024).

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Identify and Open an Event from a Map Point (Priority: P3)

**Goal**: Markers on the built-in map identify their event and let the traveler open that event's details from the map.

**Independent Test**: On the built-in map, select a marker and confirm it shows the event's title/location and that choosing it opens/selects that event.

### Tests for User Story 3

- [x] T027 [P] [US3] bUnit: activating a marker invokes the modal's `OnMarkerActivated` and triggers event selection/open, in `tests/TripPlanner.Web.Tests/Trips/TripMapMarkerActivationTests.cs`.
- [x] T028 [P] [US3] E2E (Playwright): select a marker and confirm the corresponding event's details open, in `tests/TripPlanner.E2E.Tests/Trips/TripMapMarkerTests.cs`.

### Implementation for User Story 3

- [x] T029 [US3] Extend `src/TripPlanner.Web/wwwroot/js/tripMap.js`: bind each marker to a popup showing `title` (with `location` subtext) and invoke `dotNetRef.invokeMethodAsync("OnMarkerActivated", trackedItemId)` on activation; keep markers rendering when no `dotNetRef` is supplied (depends on T012).
- [x] T030 [US3] Add `[JSInvokable] OnMarkerActivated(Guid trackedItemId)` to `src/TripPlanner.Web/Components/Trips/TripMapModal.razor` and wire it (via a callback to `TripDetails.razor`) into the existing event selection/detail flow so selecting a marker opens that event; keep near-duplicate coordinates individually selectable (depends on T014, T029).

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Accessibility, theming, security verification, and final validation across stories.

- [x] T031 [P] Accessibility pass: accessible label on the map container (`aria-label="Map of trip locations"`), keyboard-reachable marker popups, and labeled **Default map** select and **View map** button, across `TripMapModal.razor`, `TripDetails.razor`, and `Profile.razor`.
- [x] T032 [P] Light/dark theme polish for the map modal chrome/borders and empty state in `src/TripPlanner.Web/wwwroot/css/app.css`.
- [x] T033 Verify the Azure Maps subscription key is never sent to the browser: confirm only OSM tiles load client-side and geocoding stays in the API (review network calls per [research.md](research.md) Decision 3/4).
- [x] T034 Run the [quickstart.md](quickstart.md) scenarios 1–6 end to end and confirm each success check.
- [x] T035 Run the full build and test suite (`dotnet build TripPlanner.slnx`; `dotnet test` across `TripPlanner.Api.Tests`, `TripPlanner.Database.Tests`, `TripPlanner.Web.Tests`, `TripPlanner.E2E.Tests`).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: None for this feature (see note).
- **User Stories (Phases 3–5)**: Depend only on Setup. US3 additionally depends on US1 (it extends the built-in map). US1 and US2 are independent of each other.
- **Polish (Phase 6)**: After the targeted stories are complete.

### User Story Dependencies

- **US1 (P1)**: Depends on Setup (T002 Leaflet vendoring). Independent of US2.
- **US2 (P2)**: Depends on Setup only. Independent of US1.
- **US3 (P3)**: Depends on US1 (built-in map + modal must exist).

### Within Each User Story

- Tests are written/updated alongside implementation for that story.
- Contracts (records) before endpoints/clients that use them (T007 before T009/T011; T020 before T023).
- Database migration + SQL before repository/endpoint reads (T021→T022→T023).
- JS interop and API client before the modal that consumes them (T012, T011 before T013); modal before its host wiring (T013 before T014).
- Preference service before the components that read it (T024 before T025, T026).

### Parallel Opportunities

- Setup: T002 can run alongside T001's review.
- US1 tests T003–T006 are all [P]. Implementation T007, T012, T015 are [P] (distinct files); T008/T009/T010/T011 touch server code and should follow contract T007.
- US2 tests T016–T019 are all [P]. Implementation T020 and T024 are [P]; DB chain T021→T022→T023 is sequential.
- With capacity, **US1 and US2 can be built in parallel** by different people once Setup is done.

---

## Parallel Example: User Story 1

```text
# Tests together (all [P]):
T003 Geocoder unit tests (Api.Tests/Places)
T004 Trip-map endpoint tests (Api.Tests/TripMaps)
T005 bUnit View-map enable/disable + empty state (Web.Tests/Trips)
T006 E2E open built-in map (E2E.Tests/Trips)

# Independent implementation files together:
T007 TripMapContracts.cs (Contracts)
T012 tripMap.js (Web/wwwroot/js)
T015 app.css map styles (Web/wwwroot/css)
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup (T001–T002).
2. Complete Phase 3: User Story 1 (T003–T015).
3. **STOP and VALIDATE**: open the built-in map for a trip with locations; confirm fit-to-bounds and pan/zoom, disabled state with no locations, and empty state when unresolved.

### Incremental Delivery

1. Ship US1 (built-in map) as the MVP.
2. Add US2 (profile default map + provider-aware globe) — independent, ships next.
3. Add US3 (open event from a marker) — builds on US1.
4. Finish with Phase 6 polish and full validation.
