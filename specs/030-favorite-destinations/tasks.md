# Tasks: Favorite Destinations

**Input**: Design documents from `/specs/030-favorite-destinations/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`, and `contracts/api.md` plus `contracts/ui.md`

**Tests**: Included because the implementation plan requires API, database, Web, and E2E coverage.

**Organization**: Tasks are grouped by the four prioritized user stories. Shared contract and persistence primitives are established first; each story then has its own tests and implementation checkpoint.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Tasks can proceed in parallel because they work in separate files and have no incomplete dependencies.
- **[Story]**: User story implemented by the task. Setup and foundational tasks do not carry a story label.
- Every task names its exact workspace-relative file path(s).

## Phase 1: Setup

**Purpose**: Project initialization

The .NET 10 solution, Aspire composition, test projects, and central package management already exist. No setup task is needed; feature-specific package setup is listed under User Story 3.

---

## Phase 2: Foundational (Shared Contract and Persistence)

**Purpose**: Establish the owner-scoped favorite resource needed by all four stories.

- [X] T001 [P] Define favorite destination DTOs, create/update request types, and shared response contracts in `src/TripPlanner.Contracts/FavoriteDestinations/FavoriteDestinationContracts.cs`
- [X] T002 [P] Create the owner-scoped `favorite_destinations` table, constraints, and supporting indexes in `src/TripPlanner.Database/Scripts/Schema/018_favorite_destinations.sql`
- [X] T003 Add repository tests for owner isolation and favorite persistence in `tests/TripPlanner.Database.Tests/FavoriteDestinations/FavoriteDestinationRepositoryTests.cs`
- [X] T004 Implement the Dapper repository contract, favorite persistence, and owner-filtered SQL in `src/TripPlanner.Database/FavoriteDestinations/IFavoriteDestinationRepository.cs`, `src/TripPlanner.Database/FavoriteDestinations/FavoriteDestinationRepository.cs`, `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/get-favorite-destinations.sql`, `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/get-favorite-destination.sql`, `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/insert-favorite-destination.sql`, `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/update-favorite-destination.sql`, and `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/delete-favorite-destination.sql`
- [X] T005 Register the favorite repository and its dependencies in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`

**Checkpoint**: Shared contracts, migration, and repository are ready. No owner identity is accepted from request data; every read and mutation is scoped to the authenticated owner.

---

## Phase 3: User Story 1 - Save a Researched Destination (Priority: P1)

**Goal**: Let a signed-in traveler create a favorite independently of trips and revisit saved details.

**Independent Test**: Save a named, addressed destination with optional notes/source, leave and reopen Favorites, and confirm the saved data belongs only to that traveler. Verify missing required fields, duplicate confirmation, address-resolution success, and unresolved-address fallback.

### Tests for User Story 1

- [X] T006 [P] [US1] Test authenticated create/list, required-field errors, owner isolation, duplicate confirmation, and address-resolution outcomes in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationEndpointsTests.cs`
- [X] T007 [P] [US1] Test Favorites empty state, create form validation, saved research context, and duplicate-warning interaction in `tests/TripPlanner.Web.Tests/FavoriteDestinations/FavoritesPageTests.cs`
- [X] T008 [P] [US1] Test saving and revisiting a favorite through the signed-in UI in `tests/TripPlanner.E2E.Tests/FavoriteDestinations/FavoriteDestinationCreateFlowTests.cs`

### Implementation for User Story 1

- [X] T009 [US1] Extend the place contracts and Azure Maps lookup to return structured city/country components while preserving the entered address in `src/TripPlanner.Contracts/Places/PlaceContracts.cs` and `src/TripPlanner.Api/Features/Places/PlaceSuggestionLookup.cs`
- [X] T010 [US1] Implement create validation, authenticated-owner derivation, duplicate detection/confirmation, and non-blocking address resolution in `src/TripPlanner.Api/Features/FavoriteDestinations/CreateFavoriteDestinationEndpoint.cs` and `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationValidator.cs`; register the validator in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T011 [US1] Implement the authenticated owner-scoped Favorites list endpoint in `src/TripPlanner.Api/Features/FavoriteDestinations/GetFavoriteDestinationsEndpoint.cs`
- [X] T012 [US1] Register create/list routes in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationEndpointRouteBuilderExtensions.cs` and `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T013 [US1] Implement the authenticated Favorites API client and typed HTTP client registration in `src/TripPlanner.Web/Features/FavoriteDestinations/FavoriteDestinationApiClient.cs` and `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T014 [US1] Implement the signed-in Favorites page, empty state, and create form in `src/TripPlanner.Web/Components/Pages/Favorites.razor` and `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationForm.razor`
- [X] T015 [US1] Add the signed-in Favorites link to the account dropdown in `src/TripPlanner.Web/Components/Layout/NavMenu.razor`

**Checkpoint**: US1 works without a trip and can be tested without US2, US3, or US4.

---

## Phase 4: User Story 2 - Review and Maintain Favorite Destinations (Priority: P2)

**Goal**: Help travelers find, review, edit, and remove their own favorites.

**Independent Test**: Create multiple favorites; verify owner-scoped case-insensitive search across every specified field, country/city/name ordering with blank components last, full detail review, edits, confirmed deletion, and unchanged trip items.

### Tests for User Story 2

- [X] T016 [P] [US2] Test owner-scoped search, deterministic ordering, update validation, not-found semantics, and delete behavior in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationMaintenanceTests.cs`
- [X] T017 [P] [US2] Test search, full saved-context review, edit persistence, deletion confirmation, and filtered-empty state in `tests/TripPlanner.Web.Tests/FavoriteDestinations/FavoriteDestinationMaintenanceTests.cs`
- [X] T018 [P] [US2] Test searching, editing, and confirming deletion from a long Favorites list in `tests/TripPlanner.E2E.Tests/FavoriteDestinations/FavoriteDestinationMaintenanceFlowTests.cs`

### Implementation for User Story 2

- [X] T019 [P] [US2] Add case-insensitive search across name, address, city, country, notes, and source plus stable country/city/name ordering with blank values last in `src/TripPlanner.Api/Features/FavoriteDestinations/GetFavoriteDestinationsEndpoint.cs` and `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/get-favorite-destinations.sql`
- [X] T020 [P] [US2] Implement validated owner-scoped favorite updates with duplicate checking that excludes the edited ID in `src/TripPlanner.Api/Features/FavoriteDestinations/UpdateFavoriteDestinationEndpoint.cs`
- [X] T021 [P] [US2] Implement owner-scoped deletion with indistinguishable missing/other-owner behavior in `src/TripPlanner.Api/Features/FavoriteDestinations/DeleteFavoriteDestinationEndpoint.cs`
- [X] T022 [US2] Add update/delete routes and client methods in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationEndpointRouteBuilderExtensions.cs` and `src/TripPlanner.Web/Features/FavoriteDestinations/FavoriteDestinationApiClient.cs`
- [X] T023 [US2] Complete detail review, edit, delete, and explicit deletion confirmation interactions in `src/TripPlanner.Web/Components/Pages/Favorites.razor` and `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationForm.razor`
- [X] T024 [US2] Keep the single Favorites search box sticky while results scroll in `src/TripPlanner.Web/Components/Pages/Favorites.razor` and `src/TripPlanner.Web/Components/Pages/Favorites.razor.css`

**Checkpoint**: US2 can be tested using favorites created by its test fixture; it does not require the import or tracked-item flows.

---

## Phase 5: User Story 3 - Import Favorite Destinations (Priority: P2)

**Goal**: Import complete JSON or CSV lists with row-specific validation, duplicate confirmation, and all-or-nothing persistence.

**Independent Test**: Import valid JSON and CSV; verify owner assignment and address parsing. Submit malformed files, invalid rows, and unconfirmed duplicates; verify actionable errors and zero partial writes. Confirm duplicate imports explicitly and verify the full batch is inserted atomically.

### Tests for User Story 3

- [X] T025 [P] [US3] Test JSON shape, CSV headers/escaping, row numbering, and parser errors in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationImportParserTests.cs`
- [X] T026 [P] [US3] Test import authorization, validation, duplicate confirmation, address-resolution fallback, and all-or-nothing responses in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationImportEndpointTests.cs`
- [X] T027 [P] [US3] Test transactional batch insert and rollback on failure in `tests/TripPlanner.Database.Tests/FavoriteDestinations/FavoriteDestinationImportTransactionTests.cs`
- [X] T028 [P] [US3] Test file selection, row-level errors, duplicate confirmation, and successful import feedback in `tests/TripPlanner.Web.Tests/FavoriteDestinations/FavoriteDestinationImportTests.cs`
- [X] T029 [P] [US3] Test valid JSON/CSV imports and invalid/duplicate batches through the Favorites workflow in `tests/TripPlanner.E2E.Tests/FavoriteDestinations/FavoriteDestinationImportFlowTests.cs`

### Implementation for User Story 3

- [X] T030 [US3] Add a stable CsvHelper package version through `Directory.Packages.props` and reference it from `src/TripPlanner.Api/TripPlanner.Api.csproj`
- [X] T031 [US3] Implement JSON-array and standards-compliant CSV parsing with source row tracking in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationImportParser.cs`
- [X] T032 [US3] Validate every parsed row, resolve addresses consistently with manual entry, and report row errors and possible duplicates without writing in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationImportService.cs`
- [X] T033 [US3] Implement one-transaction batch persistence in `src/TripPlanner.Database/FavoriteDestinations/FavoriteDestinationRepository.cs` and `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/insert-favorite-destinations.sql`
- [X] T034 [US3] Implement the authenticated multipart import endpoint, upload limits, validation response, and duplicate-confirmation response in `src/TripPlanner.Api/Features/FavoriteDestinations/ImportFavoriteDestinationsEndpoint.cs`
- [X] T035 [US3] Register the import route in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationEndpointRouteBuilderExtensions.cs` and `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T036 [US3] Implement the import UI and API client call, expose it from Favorites, and show row errors, duplicate confirmation, and success count in `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationImport.razor`, `src/TripPlanner.Web/Features/FavoriteDestinations/FavoriteDestinationApiClient.cs`, and `src/TripPlanner.Web/Components/Pages/Favorites.razor`

**Checkpoint**: Import is independently verifiable with API/database fixtures. Its entry point is integrated into the completed Favorites page after US2 to avoid concurrent edits to that page.

---

## Phase 6: User Story 4 - Use a Favorite When Planning a Trip (Priority: P3)

**Goal**: Prefill reusable favorite details in the existing tracked-item form without changing the favorite or bypassing existing trip rules.

**Independent Test**: On a modifiable trip, select an owned favorite, change prefilled values, complete required trip-specific fields, and save. Verify existing permissions, schedule, and leg eligibility still govern the save; verify cancellation and later favorite edits/deletion do not alter saved trip items.

### Tests for User Story 4

- [X] T037 [P] [US4] Test favorite-driven tracked-item saves retain existing trip permissions, required fields, schedule, selected-leg eligibility, the allowed unassigned-leg behavior, and independent item values in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteTrackedItemEligibilityTests.cs`
- [X] T038 [P] [US4] Test favorite selection prefills only title, location, and notes without overwriting trip-specific form state in `tests/TripPlanner.Web.Tests/TripItems/TrackedItemFavoritePrefillTests.cs`
- [X] T039 [P] [US4] Test successful save, edited prefill, cancellation, and favorite/item independence in `tests/TripPlanner.E2E.Tests/FavoriteDestinations/FavoriteDestinationTrackedItemFlowTests.cs`

### Implementation for User Story 4

- [X] T040 [US4] Add an owner-scoped favorite selector to the existing tracked-item form; prefill editable title, address/location, and notes while keeping the existing save/validation path unchanged in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`

**Checkpoint**: US4 can be tested with a favorite fixture and the existing tracked-item workflow; it has no persistent relationship to a favorite.

---

## Phase 7: Polish and Cross-Cutting Validation

**Purpose**: Verify the integrated feature against the repository's quickstart scenarios and quality gates.

- [X] T041 Run the build and all four focused test projects in `specs/030-favorite-destinations/quickstart.md`, including `tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`, `tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj`, `tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`, and `tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj`

---

## Phase 8: Post-Review Refinements

**Purpose**: Changes made during on-screen review of the implemented feature.

### Review fixes

- [X] T042 Fix the edit form error banner rendering the literal `_formError` by binding `ErrorMessage="@_formError"` in `src/TripPlanner.Web/Components/Pages/Favorites.razor`
- [X] T043 Read Azure Maps Search v1 `country` and prefer `localName` over `municipality` for city, with realistic response fixtures, in `src/TripPlanner.Api/Features/Places/PlaceSuggestionLookup.cs` and `tests/TripPlanner.Api.Tests/Places/AzureMapsPlaceSuggestionLookupTests.cs`

### Calculated location

- [X] T044 Add nullable, paired, range-checked `latitude`/`longitude` columns in `src/TripPlanner.Database/Scripts/Schema/019_favorite_destination_coordinates.sql` and return them from every favorite query in `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/`
- [X] T045 Return coordinates from address resolution and add them to `FavoriteDestinationDto` in `src/TripPlanner.Contracts/Places/PlaceContracts.cs`, `src/TripPlanner.Contracts/FavoriteDestinations/FavoriteDestinationContracts.cs`, and `src/TripPlanner.Api/Features/Places/PlaceSuggestionLookup.cs`
- [X] T046 Make city, country, latitude, and longitude server-calculated only: remove them from create/update requests, pass the resolved location to `src/TripPlanner.Database/FavoriteDestinations/FavoriteDestinationRepository.cs`, and clear and recalculate them when an update changes the address in `src/TripPlanner.Api/Features/FavoriteDestinations/UpdateFavoriteDestinationEndpoint.cs`
- [X] T047 [P] Test calculated-location create, unchanged-address retention, changed-address clearing/recalculation, and coordinate persistence in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationEndpointsTests.cs` and `tests/TripPlanner.Database.Tests/FavoriteDestinations/FavoriteDestinationRepositoryTests.cs`

### Source folded into notes

- [X] T048 Move any existing source text into notes and drop the `source` column in `src/TripPlanner.Database/Scripts/Schema/020_favorite_destination_source_into_notes.sql`; remove source from contracts, queries, search, import format (`name,address,notes`), and the form in `src/TripPlanner.Api/Features/FavoriteDestinations/FavoriteDestinationImportParser.cs` and `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationForm.razor`

### Bulk delete

- [X] T049 Add an owner-scoped bulk delete (1-1000 IDs, single statement) in `src/TripPlanner.Api/Features/FavoriteDestinations/DeleteFavoriteDestinationsEndpoint.cs` and `src/TripPlanner.Database/Scripts/Queries/FavoriteDestinations/delete-favorite-destinations.sql`, with a client method in `src/TripPlanner.Web/Features/FavoriteDestinations/FavoriteDestinationApiClient.cs`
- [X] T050 [P] Test bulk delete owner isolation, ID validation, and deleted count in `tests/TripPlanner.Api.Tests/FavoriteDestinations/FavoriteDestinationMaintenanceTests.cs` and `tests/TripPlanner.Database.Tests/FavoriteDestinations/FavoriteDestinationRepositoryTests.cs`

### Favorites page layout

- [X] T051 Present favorites in a Profile-style rounded card: sticky header with half-width search and a right-aligned New/Import/Delete icon toolbar with tooltips; body with the import card, messages, and a full-width striped table on the card background in `src/TripPlanner.Web/Components/Pages/Favorites.razor`, `src/TripPlanner.Web/Components/Pages/Favorites.razor.css`, and `src/TripPlanner.Web/Components/Shared/ActionIcon.razor`
- [X] T052 Show the table as checkbox, Name, Address, City, Country with select-all and confirmed bulk delete of listed selections; Name opens a modal add/edit dialog with read-only calculated location and in-dialog delete in `src/TripPlanner.Web/Components/Pages/Favorites.razor` and `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationForm.razor`
- [X] T053 Show import as a toggled card with Import file / Cancel actions and a format hint in `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationImport.razor`
- [X] T054 [P] Test table rendering, modal edit/delete, import toggle, and bulk selection/deletion in `tests/TripPlanner.Web.Tests/FavoriteDestinations/FavoritesPageTests.cs` and `tests/TripPlanner.Web.Tests/FavoriteDestinations/FavoriteDestinationMaintenanceTests.cs`

### Tracked-item picker

- [X] T055 Replace the favorite select with a searchable combobox filtering by name, address, city, or country, placed under Trip leg, in `src/TripPlanner.Web/Components/Favorites/FavoriteDestinationPicker.razor` and `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T056 [P] Test picker filtering, selection prefill, and Escape behavior in `tests/TripPlanner.Web.Tests/TripItems/TrackedItemFavoritePrefillTests.cs`

### Documentation

- [X] T057 Add favorites FAQ entries in `src/TripPlanner.Web/Components/Pages/Faq.razor` and align `specs/030-favorite-destinations/spec.md`, `data-model.md`, `contracts/api.md`, `contracts/ui.md`, and `quickstart.md` with the refined behavior

### Deferred

- Large-import throughput (sequential address lookup per row) is deferred to a later feature.

**Checkpoint**: API (348), Database (121), and Web (349 passed, 3 pre-existing skips) test suites pass.

---

## Requirements Traceability

| Requirement | Tasks |
|---|---|
| FR-001 | T010, T014 |
| FR-002 | T001, T010, T014 |
| FR-003 | T001, T010, T014, T048 |
| FR-004 | T006, T010 |
| FR-005 | T002-T005, T006, T016, T026, T049-T050 |
| FR-006 | T003-T005, T006, T016, T026, T037, T049-T050 |
| FR-007 | T006, T010, T020 |
| FR-008 | T011, T014-T015 |
| FR-009 | T014, T017, T023, T052 |
| FR-010 | T007, T014 |
| FR-011 | T016-T019, T023-T024, T048, T051 |
| FR-012 | T016-T017, T020, T023, T046, T052 |
| FR-013 | T016-T018, T021, T023, T049-T050, T052, T054 |
| FR-014 | T038-T040, T055-T056 |
| FR-015 | T037, T040 |
| FR-016 | T038-T040 |
| FR-017 | T037, T039-T040 |
| FR-018 | T037, T039-T040 |
| FR-019 | T037, T039-T040 |
| FR-020 | T018, T021, T039-T040 |
| FR-021 | T039-T040 |
| FR-022 | T006, T009-T010, T026, T032, T043-T047 |
| FR-023 | T006, T009-T010, T026, T032, T046-T047 |
| FR-024 | T016, T019 |
| FR-025 | T017-T018, T024, T051 |
| FR-026 | T025-T026, T030-T034, T048, T053 |
| FR-027 | T025-T029, T032-T036 |
| FR-028 | T026-T029, T032-T036 |

---

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No tasks; the existing solution and test infrastructure are already initialized.
- **Foundational (Phase 2)**: T001 and T002 can start in parallel. T003 follows the shared contract/schema definitions; T004 follows T001-T003; T005 follows T004. This phase blocks all user stories.
- **User Story 1 (Phase 3)**: Starts after Phase 2 and establishes the create/list experience that later stories reuse.
- **User Story 2 (Phase 4)**: Starts after Phase 2 and uses the shared favorite resource; its page work is sequenced before US3's import entry-point integration.
- **User Story 3 (Phase 5)**: Parser, validation, and transactional import depend on Phase 2 and the shared address-resolution behavior from US1. Import processing remains isolated from US2 maintenance behavior.
- **User Story 4 (Phase 6)**: Starts after Phase 2 and uses the owner-scoped list/API from US1 plus the existing tracked-item workflow; it does not depend on US2 search/edit/delete or US3 import.
- **Polish (Phase 7)**: Depends on all four stories being integrated.
- **Post-Review Refinements (Phase 8)**: Follows Phase 7; applied iteratively during on-screen review.

### User Story Dependencies

- **US1 (P1)**: Phase 2 only; no dependency on another story.
- **US2 (P2)**: Phase 2 and the shared resource contracts; independently testable with its own favorite fixtures.
- **US3 (P2)**: Phase 2 plus US1's shared owner, validation, and address-resolution primitives. Its final Favorites-page link is sequenced after US2 to avoid editing the page concurrently.
- **US4 (P3)**: Phase 2 and US1's owner-scoped list endpoint; does not depend on US2 or US3.

### Within Each User Story

- Write the story's tests before its implementation and verify the new behavior fails before implementation.
- Keep API, Web, database, and E2E tests in their designated test projects and use separate files for parallel work.
- Implement service/repository behavior before wiring endpoints or UI that consumes it.
- Preserve existing owner authorization, tracked-item validation, schedule, and leg rules; do not add favorite-to-item persistence links.

### Parallel Opportunities

- T001 and T002 can run in parallel. Story test files within a phase can be authored in parallel because each is a separate file.
- In US2, T019, T020, and T021 touch separate endpoint/query files and can be implemented in parallel after their tests; T022 and page integration follow.
- In US3, the API parser, endpoint, database transaction, Web, and E2E test files can be authored in parallel. Package setup precedes parser implementation; parser implementation precedes import validation/service work, while database batch persistence can proceed alongside both. The endpoint follows validation and persistence, then route/UI integration follows the endpoint.
- US4 can proceed after US1 even if US2 and US3 are not selected for delivery.

### Parallel Example: User Story 2

```text
After T016-T018 are authored, implement in parallel:
- T019: search and ordering query/endpoint files
- T020: update endpoint
- T021: delete endpoint
Then complete T022-T024 in dependency order.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete the shared contract and persistence foundation (Phase 2).
2. Complete US1 create/list, address resolution, and Favorites navigation.
3. Validate US1 independently with its API, Web, database foundation, and E2E checks.
4. Deliver US1 as the MVP; it provides reusable favorites without requiring import or trip-item integration.

### Incremental Delivery

1. Complete Phase 2, then deliver US1 as the first usable increment.
2. Add US2 search, ordering, editing, deletion, and sticky-list behavior.
3. Add US3 atomic JSON/CSV import, then integrate its entry point into Favorites.
4. Add US4 tracked-item prefill through existing authorization and validation.
5. Run the complete quickstart validation after all selected stories are integrated.

---

## Notes

- `[P]` marks work that can safely proceed in parallel on separate files with prerequisites complete.
- Story labels map directly to the four scenarios in `specs/030-favorite-destinations/spec.md`.
- Owner IDs come from authenticated identity only. Imports and favorite queries never trust caller-supplied owner IDs.
- Tests must confirm favorites and tracked items remain independent after either is edited or deleted.
