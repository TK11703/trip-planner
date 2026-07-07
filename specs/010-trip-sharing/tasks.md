# Tasks: Trip Sharing and Collaboration

**Input**: Design documents from `specs/010-trip-sharing/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [contracts/ui.md](./contracts/ui.md), [quickstart.md](./quickstart.md)

**Tests**: Dedicated test-authoring tasks are not included because neither the feature spec nor the user request asked for TDD. Validation tasks are included in the final phase using the existing test projects and quickstart scenarios.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare project references and configuration needed by the sharing implementation.

- [x] T001 Add Microsoft Graph SDK package reference for tenant user lookup in src/TripPlanner.Api/TripPlanner.Api.csproj
- [x] T002 Add AzureEntra Graph lookup configuration placeholders and scopes notes in src/TripPlanner.Api/appsettings.json
- [x] T003 [P] Add trip-sharing route registration stub in src/TripPlanner.Api/Features/TripSharing/TripSharingEndpointRouteBuilderExtensions.cs
- [x] T004 [P] Add TripSharing feature folder registration comments to src/TripPlanner.Api/Features/Trips/TripEndpointRouteBuilderExtensions.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data, contracts, and access-resolution infrastructure that MUST be complete before any user story can be implemented.

**Critical**: No user story work can begin until this phase is complete.

- [x] T005 Add TripAccessLevel, TripShareMember, DirectoryUserResult, UpsertTripShareRequest, and UpdateTripShareAccessRequest contracts in src/TripPlanner.Contracts/Trips/TripContracts.cs
- [x] T006 Extend TripSummary and TripDetail with accessLevel, isOwner, and sharedPeople fields in src/TripPlanner.Contracts/Trips/TripContracts.cs
- [x] T007 Create trip_shares schema with uniqueness, access-level check, indexes, and cascade delete in src/TripPlanner.Database/Scripts/Schema/007_trip_sharing.sql
- [x] T008 [P] Create share upsert SQL command in src/TripPlanner.Database/Scripts/Commands/TripSharing/UpsertTripShare.sql
- [x] T009 [P] Create share access update SQL command in src/TripPlanner.Database/Scripts/Commands/TripSharing/UpdateTripShareAccess.sql
- [x] T010 [P] Create share delete SQL command in src/TripPlanner.Database/Scripts/Commands/TripSharing/DeleteTripShare.sql
- [x] T011 [P] Create trip share list SQL query in src/TripPlanner.Database/Scripts/Queries/TripSharing/GetTripShares.sql
- [x] T012 [P] Create trip caller access SQL query in src/TripPlanner.Database/Scripts/Queries/TripSharing/GetTripAccess.sql
- [x] T013 Implement TripSharingRepository with share CRUD and access lookup methods in src/TripPlanner.Database/TripSharing/TripSharingRepository.cs
- [x] T014 Register TripSharingRepository in the database service registration file src/TripPlanner.Database/Extensions/ServiceCollectionExtensions.cs
- [x] T015 Implement TripAccessResolver for owner/collaborator/viewer/no-access checks in src/TripPlanner.Api/Security/TripAccessResolver.cs
- [x] T016 Register TripAccessResolver in API service registration in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs
- [x] T017 Update TripReadRepository to return owned and shared access metadata for detail/list queries in src/TripPlanner.Database/Trips/TripReadRepository.cs
- [x] T018 Update GetTripsPage SQL to include owned and shared trips with caller access metadata in src/TripPlanner.Database/Scripts/Queries/Trips/GetTripsPage.sql
- [x] T019 Update GetTripDetail SQL to allow owner/member reads and return caller access metadata in src/TripPlanner.Database/Scripts/Queries/Trips/GetTripDetail.sql

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Share a Trip as Viewer or Collaborator (Priority: P1) - MVP

**Goal**: A trip owner can open a Share modal from trip details, search Azure tenant users, and create viewer or collaborator shares without duplicates or self-shares.

**Independent Test**: Open a trip as owner, use Share near Edit trip, add one viewer and one collaborator from tenant search, and confirm both appear in the current shared people list with the correct access level.

### Implementation for User Story 1

- [x] T020 [P] [US1] Implement Microsoft Graph-backed tenant user lookup abstraction in src/TripPlanner.Api/Features/TripSharing/UserDirectoryLookup.cs
- [x] T021 [P] [US1] Add request validation for share upsert and directory query inputs in src/TripPlanner.Api/Features/TripSharing/TripSharingValidator.cs
- [x] T022 [US1] Implement owner-only directory user search endpoint in src/TripPlanner.Api/Features/TripSharing/SearchDirectoryUsersEndpoint.cs
- [x] T023 [US1] Implement owner-only create/update share endpoint in src/TripPlanner.Api/Features/TripSharing/UpsertTripShareEndpoint.cs
- [x] T024 [US1] Map trip-sharing endpoints under /api/trips/{tripId}/shares in src/TripPlanner.Api/Features/TripSharing/TripSharingEndpointRouteBuilderExtensions.cs
- [x] T025 [US1] Add share and directory methods to ITripApiClient in src/TripPlanner.Web/Features/Trips/TripApiClient.cs
- [x] T026 [P] [US1] Create ShareTripModal component with current people, tenant search, and new-share permission selector in src/TripPlanner.Web/Components/Trips/ShareTripModal.razor
- [x] T027 [US1] Add owner-only Share button beside Edit trip and wire ShareTripModal in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [x] T028 [US1] Add shared people card as the third bottom detail column in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [x] T029 [US1] Record audit events for successful and denied share creation in src/TripPlanner.Api/Features/TripSharing/UpsertTripShareEndpoint.cs

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Access a Shared Trip After Signing In (Priority: P1)

**Goal**: Shared viewers can read but not edit, shared collaborators can edit itinerary content, and unauthenticated or unauthorized users cannot see trip contents.

**Independent Test**: Sign in as a viewer and confirm trip content is readable with no edit controls, then sign in as a collaborator and confirm leg/event edits save while owner-only actions remain unavailable.

### Implementation for User Story 2

- [x] T030 [US2] Update GetTripDetailEndpoint to use TripAccessResolver for owner/collaborator/viewer reads in src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs
- [x] T031 [US2] Update GetTripTimelineEndpoint to use TripAccessResolver for owner/collaborator/viewer reads in src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs
- [x] T032 [US2] Update TripLegEndpoints to allow owner/collaborator writes and deny viewer writes in src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs
- [x] T033 [US2] Update TrackedItemEndpoints to allow owner/collaborator writes and deny viewer writes in src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs
- [x] T034 [US2] Keep UpdateTripEndpoint owner-only through TripAccessResolver in src/TripPlanner.Api/Features/Trips/UpdateTrip/UpdateTripEndpoint.cs
- [x] T035 [US2] Update TripDetails page to hide Edit trip, Share, Add leg, Add event, and timeline edit affordances based on _trip.AccessLevel in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [x] T036 [US2] Pass read-only or edit-disabled state into TripTimeline interactions for viewers in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [x] T037 [US2] Show viewer/collaborator access messaging through TripAccessState or inline alerts in src/TripPlanner.Web/Components/Trips/TripAccessState.razor
- [x] T038 [US2] Record audit events for denied viewer edits and denied no-access reads in src/TripPlanner.Api/Security/TripAccessResolver.cs

**Checkpoint**: User Stories 1 and 2 both work independently and enforce access server-side.

---

## Phase 5: User Story 3 - Manage Who Can Access a Trip (Priority: P2)

**Goal**: A trip owner can review current members, change permission levels, and remove access; non-owners cannot manage sharing.

**Independent Test**: Open the share modal as owner, change a viewer to collaborator, remove another member, and confirm the changed member has new permissions while the removed member loses access on the next action.

### Implementation for User Story 3

- [x] T039 [US3] Implement owner-only share access update endpoint in src/TripPlanner.Api/Features/TripSharing/UpdateTripShareAccessEndpoint.cs
- [x] T040 [US3] Implement owner-only share removal endpoint in src/TripPlanner.Api/Features/TripSharing/DeleteTripShareEndpoint.cs
- [x] T041 [US3] Add update and delete share client methods to TripApiClient in src/TripPlanner.Web/Features/Trips/TripApiClient.cs
- [x] T042 [US3] Add permission edit controls, remove actions, and save states to ShareTripModal in src/TripPlanner.Web/Components/Trips/ShareTripModal.razor
- [x] T043 [US3] Refresh trip detail sharedPeople after share updates/removals in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [x] T044 [US3] Ensure share-management endpoints reject collaborator/viewer callers in src/TripPlanner.Api/Features/TripSharing/TripSharingEndpointRouteBuilderExtensions.cs
- [x] T045 [US3] Record audit events for share access changes and removals in src/TripPlanner.Api/Features/TripSharing/UpdateTripShareAccessEndpoint.cs

**Checkpoint**: User Story 3 works independently after the foundation and preserves owner-only sharing controls.

---

## Phase 6: User Story 4 - Discover Trips Shared With Me (Priority: P3)

**Goal**: A signed-in user can find shared trips on the trips page, separate from ownership state, with clear badges showing shared vs owned and viewer/collaborator permission.

**Independent Test**: Sign in as a member with shared trips and confirm the trips page shows shared entries with access badges and removes revoked trips from the list.

### Implementation for User Story 4

- [x] T046 [US4] Update GetTripsEndpoint to return the caller's owned and shared trips with access metadata in src/TripPlanner.Api/Features/Trips/GetTrips/GetTripsEndpoint.cs
- [x] T047 [US4] Update GetRecentTripsEndpoint access behavior to either include accessible trips or explicitly remain owner-only in src/TripPlanner.Api/Features/Trips/GetRecentTrips/GetRecentTripsEndpoint.cs
- [x] T048 [US4] Add Owned/Shared and Viewer/Collaborator badges to trip cards in src/TripPlanner.Web/Components/Pages/Trips/TripsIndex.razor
- [x] T049 [US4] Update trips empty/loading copy to cover both owned and shared trips in src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor
- [x] T050 [US4] Update RecentTripsList badges or filtering to match the selected recent-trips behavior in src/TripPlanner.Web/Components/Trips/RecentTripsList.razor

**Checkpoint**: All user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, hardening, and consistency work across the trip-sharing feature.

- [x] T051 [P] Add API coverage for owner-only share management and viewer/collaborator/no-access behavior in tests/TripPlanner.Api.Tests/TripSharing/TripSharingEndpointTests.cs
- [x] T052 [P] Add database coverage for trip_shares persistence and owned/shared trip queries in tests/TripPlanner.Database.Tests/TripSharing/TripSharingRepositoryTests.cs
- [x] T053 [P] Add Blazor component coverage for ShareTripModal states and trip card badges in tests/TripPlanner.Web.Tests/TripSharing/TripSharingComponentTests.cs
- [x] T054 [P] Add E2E coverage for owner share, viewer read-only, collaborator edit, and revocation flows in tests/TripPlanner.E2E.Tests/TripSharing/TripSharingE2ETests.cs
- [x] T055 Review Microsoft Graph configuration for least-privilege scopes, no hardcoded credentials, and environment-driven settings in src/TripPlanner.Api/appsettings.json
- [x] T056 Run API, database, web, and E2E validation commands from specs/010-trip-sharing/quickstart.md
- [x] T057 Run full solution build for TripPlanner.slnx from TripPlanner.slnx

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories.
- **User Stories (Phase 3+)**: Depend on Foundational completion.
- **Polish (Phase 7)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational; delivers owner sharing, tenant lookup, modal, and shared people display.
- **User Story 2 (P1)**: Can start after Foundational; depends on access metadata from the foundation, but can be validated independently with seeded shares.
- **User Story 3 (P2)**: Depends on User Story 1 share creation and current people modal surface.
- **User Story 4 (P3)**: Depends on Foundational owned/shared query support; can be implemented after or alongside US1 once access metadata exists.

### Within Each User Story

- Contracts and schema before repositories.
- Repositories before access resolver and endpoints.
- Endpoints before Blazor client methods.
- Client methods before components/pages.
- UI behavior before quickstart/manual validation.

### Parallel Opportunities

- T003 and T004 can run in parallel during setup.
- SQL command/query tasks T008-T012 can run in parallel once T007 is understood.
- T020 and T021 can run in parallel with T026 in US1 after client/server contract names are stable.
- US2 endpoint enforcement tasks T030-T034 can be split across API files after T015 is complete.
- Polish test tasks T051-T054 can run in parallel after feature behavior is implemented.

---

## Parallel Example: User Story 1

```text
Task: "Implement Microsoft Graph-backed tenant user lookup abstraction in src/TripPlanner.Api/Features/TripSharing/UserDirectoryLookup.cs"
Task: "Add request validation for share upsert and directory query inputs in src/TripPlanner.Api/Features/TripSharing/TripSharingValidator.cs"
Task: "Create ShareTripModal component with current people, tenant search, and new-share permission selector in src/TripPlanner.Web/Components/Trips/ShareTripModal.razor"
```

## Parallel Example: User Story 2

```text
Task: "Update GetTripDetailEndpoint to use TripAccessResolver for owner/collaborator/viewer reads in src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs"
Task: "Update GetTripTimelineEndpoint to use TripAccessResolver for owner/collaborator/viewer reads in src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs"
Task: "Update TripLegEndpoints to allow owner/collaborator writes and deny viewer writes in src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs"
Task: "Update TrackedItemEndpoints to allow owner/collaborator writes and deny viewer writes in src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs"
```

## Implementation Strategy

### MVP First (User Stories 1 and 2)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational access model and resolver.
3. Complete Phase 3: User Story 1 share creation and modal.
4. Complete Phase 4: User Story 2 read/write enforcement.
5. Stop and validate owner share creation, viewer read-only access, collaborator itinerary editing, and no-access denial.

### Incremental Delivery

1. Foundation ready: schema, contracts, repositories, access resolver.
2. US1: Owner can share with viewer/collaborator and see shared people.
3. US2: Shared users can access according to permission.
4. US3: Owner can update/remove shares.
5. US4: Shared trips are discoverable with badges on `/trips`.
6. Polish: focused automated tests and quickstart validation.

### Parallel Team Strategy

1. One developer handles database/contracts foundation.
2. One developer handles API access resolver and endpoint enforcement.
3. One developer handles Blazor modal/list/detail UI once contracts are stable.
4. Test tasks split by project during polish.

## Notes

- `[P]` means the task touches a different file and has no dependency on an incomplete same-phase task.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` map directly to user stories in [spec.md](./spec.md).
- Every API authorization decision must remain server-side; UI hiding is not sufficient.
- Keep Graph lookup least-privilege and environment-driven; do not hardcode credentials.
- Preserve Minimal API vertical slices, Dapper SQL files, Blazor, .NET 10, and Aspire composition.
