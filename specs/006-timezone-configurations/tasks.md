# Tasks: Timezone Configurations

**Input**: Design documents from `specs/006-timezone-configurations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [quickstart.md](./quickstart.md)

**Tests**: The feature specification defines independent acceptance criteria, but does not request TDD. Tasks therefore include focused validation commands instead of pre-implementation test-writing tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish shared timezone building blocks and service registration points used by later phases.

- [X] T001 Add shared timezone option contracts in src/TripPlanner.Contracts/Common/TimezoneContracts.cs
- [X] T002 [P] Add API timezone validation helper in src/TripPlanner.Api/Features/Timezones/TimezoneIdValidator.cs
- [X] T003 [P] Add web timezone option provider in src/TripPlanner.Web/Features/Timezones/TimezoneOptionsProvider.cs
- [X] T004 Register timezone helpers in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs and src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Update shared contracts, database shape, SQL projections, and repository surfaces before story work begins.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add profile timezone fields to response and update request contracts in src/TripPlanner.Contracts/Profile/UserProfileContracts.cs
- [X] T006 [P] Replace trip leg instant request fields with start/end local timezone fields in src/TripPlanner.Contracts/TripItems/TripItemContracts.cs
- [X] T007 [P] Add start/end local timezone fields to trip leg DTOs in src/TripPlanner.Contracts/Trips/TripContracts.cs
- [X] T008 [P] Add calendarStart/calendarEnd and start/end timezone metadata to timeline events in src/TripPlanner.Contracts/Timeline/TimelineContracts.cs
- [X] T009 Add timezone schema migration and legacy backfill in src/TripPlanner.Database/Scripts/Schema/004_timezone_configurations.sql
- [X] T010 Update user profile SQL to persist and read profile timezones in src/TripPlanner.Database/Scripts/Commands/UserProfiles/EnsureUserProfileFromClaims.sql, src/TripPlanner.Database/Scripts/Commands/UserProfiles/UpdateUserProfile.sql, and src/TripPlanner.Database/Scripts/Queries/UserProfiles/GetUserProfile.sql
- [X] T011 Update trip leg SQL to persist start/end local values and timezone ids in src/TripPlanner.Database/Scripts/Commands/TripLegs/UpsertAndDeleteTripLegs.sql
- [X] T012 Update timeline SQL projection to return wall-clock calendar values and start/end timezone metadata in src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql
- [X] T013 Update user profile repository mapping for profile timezone fields in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T014 Update trip item repository mapping for start/end local values and timezone ids in src/TripPlanner.Database/TripItems/TripItemRepository.cs
- [X] T015 Update timeline repository mapping for wall-clock calendar fields and timezone metadata in src/TripPlanner.Database/Timeline/TimelineRepository.cs
- [X] T016 Build the solution after shared contract and database changes using TripPlanner.slnx

**Checkpoint**: Foundation ready - user story implementation can now begin in priority order or in parallel by story.

---

## Phase 3: User Story 1 - Set Profile Timezone (Priority: P1) MVP

**Goal**: A signed-in traveler can select, save, revisit, and update their default profile timezone.

**Independent Test**: Sign in, set a profile timezone, save it, leave and return to the profile page, and confirm the selected timezone remains visible and invalid timezone submissions leave the previous valid value unchanged.

### Implementation for User Story 1

- [X] T017 [US1] Validate profile timezone updates in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs
- [X] T018 [US1] Include profile timezone in profile get/update endpoint handling in src/TripPlanner.Api/Features/UserProfiles/GetProfileEndpoint.cs and src/TripPlanner.Api/Features/UserProfiles/UpdateProfileEndpoint.cs
- [X] T019 [US1] Send and receive profile timezone values in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs
- [X] T020 [US1] Add a required timezone select to the profile form in src/TripPlanner.Web/Components/Pages/Profile.razor
- [X] T021 [US1] Validate profile timezone behavior with focused API and web test runs using tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: User Story 1 is independently functional and provides the MVP profile default timezone.

---

## Phase 4: User Story 2 - Set Start and End Timezones Per Trip Leg (Priority: P2)

**Goal**: A traveler can create or edit a trip leg with separate required start and end timezones, including first-leg profile defaults and later-leg defaults from the previous leg's end timezone.

**Independent Test**: Create or edit a trip leg whose start timezone differs from its end timezone and confirm both selections display and persist independently; then add a later leg and confirm both defaults come from the previous leg's end timezone.

### Implementation for User Story 2

- [X] T030 [US2] Validate trip leg start/end timezone behavior with focused API, database, and web test runs using tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj, tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj, and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: User Stories 1 and 2 both work independently; trip legs persist required start and end timezones.

---

## Phase 5: User Story 3 - Adjust Existing Trip Timing With Timezone Changes (Priority: P3)

**Goal**: Existing leg start/end timezone selections are preserved after profile changes, edited leg timing remains associated with the selected timezone, and calendar display preserves scheduled wall-clock start and end times.

**Independent Test**: Change a profile timezone after legs exist, confirm saved leg start/end timezones remain unchanged, then view a cross-timezone leg on the calendar and confirm start/end wall-clock times are not shifted to the viewer timezone.

### Implementation for User Story 3

- [X] T031 [US3] Preserve saved start/end timezone values during profile changes in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T032 [US3] Show start/end timezone labels in trip leg detail display in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T033 [US3] Use calendarStart and calendarEnd for trip leg events in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T034 [US3] Keep FullCalendar event rendering on wall-clock local values in src/TripPlanner.Web/wwwroot/js/tripTimeline.js
- [X] T035 [US3] Include start/end timezone labels in timeline selection metadata in src/TripPlanner.Database/Timeline/TimelineRepository.cs
- [X] T036 [US3] Validate wall-clock timeline behavior with focused database and web test runs using tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: All user stories are independently functional and existing trip timing is preserved across profile and calendar flows.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation checks, and small consistency fixes across completed stories.

- [X] T037 [P] Review timezone labels and validation messages for user-facing clarity in src/TripPlanner.Web/Components/Pages/Profile.razor and src/TripPlanner.Web/Components/TripItems/TripLegForm.razor
- [X] T038 [P] Review API error response consistency for timezone validation in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs and src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs
- [X] T039 Run quickstart scenario validation from specs/006-timezone-configurations/quickstart.md
- [X] T040 Run full solution validation with TripPlanner.slnx

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion and is the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational completion and uses profile timezone defaults from US1, but can be implemented against seeded profile timezone data if worked in parallel.
- **User Story 3 (Phase 5)**: Depends on Foundational completion and the trip leg fields from US2.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - no dependency on US2 or US3.
- **User Story 2 (P2)**: Can start after Foundational - depends conceptually on profile timezone defaults but remains testable through seeded profile data.
- **User Story 3 (P3)**: Starts after US2 because wall-clock calendar preservation depends on saved trip leg start/end timezone fields.

### Parallel Opportunities

- T002 and T003 can run in parallel after T001 is understood because they touch API and web helper files.
- T005, T006, T007, and T008 can run in parallel because they update separate contract files.
- T017 through T020 are mostly separate API/client/UI files within US1, after T005, T010, and T013 are complete.
- T022 can run in parallel with T026 because the default query and validator touch separate files.
- T032, T033, and T034 can run in parallel after timeline contracts and SQL projection are complete.
- T037 and T038 can run in parallel during polish because they touch web copy and API validation separately.

---

## Parallel Example: User Story 1

```text
Task: "T017 [US1] Validate profile timezone updates in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs"
Task: "T019 [US1] Send and receive profile timezone values in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs"
Task: "T020 [US1] Add a required timezone select to the profile form in src/TripPlanner.Web/Components/Pages/Profile.razor"
```

## Parallel Example: User Story 2

```text
Task: "T022 [P] [US2] Add trip leg default timezone query in src/TripPlanner.Database/Scripts/Queries/TripLegs/GetTripLegDefaults.sql"
Task: "T026 [US2] Validate start/end timezone ids and chronological derived instants in src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs"
Task: "T027 [US2] Send start/end local values and timezone ids through the trip API client in src/TripPlanner.Web/Features/Trips/TripApiClient.cs"
```

## Parallel Example: User Story 3

```text
Task: "T032 [US3] Show start/end timezone labels in trip leg detail display in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor"
Task: "T033 [US3] Use calendarStart and calendarEnd for trip leg events in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor"
Task: "T034 [US3] Keep FullCalendar event rendering on wall-clock local values in src/TripPlanner.Web/wwwroot/js/tripTimeline.js"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate profile timezone behavior independently.
5. Demo the profile default timezone before adding trip leg behavior.

### Incremental Delivery

1. Complete Setup + Foundational to prepare shared contracts, schema, and repositories.
2. Add User Story 1 so users can save a default profile timezone.
3. Add User Story 2 so trip legs require start/end timezones with correct defaulting.
4. Add User Story 3 so existing leg timing and calendar wall-clock display behave correctly.
5. Run quickstart and full solution validation.

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup and Foundational together.
2. Developer A implements US1 profile timezone form/API behavior.
3. Developer B starts US2 trip leg contracts/default endpoint after foundational contracts and schema land.
4. Developer C prepares US3 timeline projection/UI changes after US2 fields are stable.
5. Integrate by running story-focused validation at each checkpoint.

---

## Notes

- [P] tasks use different files and have no dependency on incomplete tasks in the same phase.
- [US1], [US2], and [US3] labels map tasks to the user stories in [spec.md](./spec.md).
- Every trip leg must persist both `startTimeZoneId` and `endTimeZoneId`.
- Calendar event display must use offset-free `calendarStart` and `calendarEnd` for trip legs.
- Avoid changing tracked item timezone behavior unless required to keep timeline projection compatible.
