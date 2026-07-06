# Tasks: User Information Capture

**Input**: Design documents from `specs/005-user-info-capture/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Included because the plan and success criteria require acceptance coverage for create-only Azure seeding, owner scoping, validation, and profile persistence.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish profile feature locations and shared contracts used by API, database, web, and tests.

- [X] T001 Create profile feature directories in src/TripPlanner.Api/Features/UserProfiles, src/TripPlanner.Contracts/Profile, src/TripPlanner.Database/UserProfiles, src/TripPlanner.Database/Scripts/Commands/UserProfiles, src/TripPlanner.Database/Scripts/Queries/UserProfiles, src/TripPlanner.Web/Features/Profile, tests/TripPlanner.Api.Tests/UserProfiles, tests/TripPlanner.Database.Tests/UserProfiles, and tests/TripPlanner.Web.Tests/Profile
- [X] T002 [P] Create shared user profile contract records in src/TripPlanner.Contracts/Profile/UserProfileContracts.cs
- [X] T003 [P] Create profile API client interface skeleton in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core identity, persistence, validation, and registration work that must exist before user-story behavior can be implemented.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Extend authenticated user claim accessors for first name and last name in src/TripPlanner.Api/Security/ICurrentUser.cs and src/TripPlanner.Api/Security/CurrentUser.cs
- [X] T005 Create user profile schema evolution script for the existing users table in src/TripPlanner.Database/Scripts/Schema/003_user_profiles.sql
- [X] T006 [P] Create user profile query SQL stub in src/TripPlanner.Database/Scripts/Queries/UserProfiles/GetUserProfile.sql
- [X] T007 [P] Create create-only Azure seed SQL stub in src/TripPlanner.Database/Scripts/Commands/UserProfiles/EnsureUserProfileFromClaims.sql
- [X] T008 [P] Create editable profile update SQL stub in src/TripPlanner.Database/Scripts/Commands/UserProfiles/UpdateUserProfile.sql
- [X] T009 Create user profile repository interface and implementation shell in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T010 Register the user profile repository in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs
- [X] T011 Create shared user profile validation helper in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Sign In and Confirm Azure-Seeded Profile (Priority: P1) MVP

**Goal**: A signed-in user with no saved profile gets a database profile seeded from Azure account details, and later sign-ins do not overwrite saved Trip Planner profile values.

**Independent Test**: Sign in as a new user with Azure profile claims, load the profile, confirm the saved profile is created, then simulate changed Azure claims and confirm saved values remain unchanged.

### Tests for User Story 1

- [X] T012 [P] [US1] Add repository tests for create-only Azure seeding and no-overwrite behavior in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileRepositorySeedTests.cs
- [X] T013 [P] [US1] Add API tests for GET /api/profile creation from claims and returning existing saved values in tests/TripPlanner.Api.Tests/UserProfiles/GetProfileEndpointTests.cs
- [X] T014 [P] [US1] Add web component test for displaying Azure-seeded profile values in tests/TripPlanner.Web.Tests/Profile/ProfilePageSeedTests.cs
- [X] T015 [P] [US1] Add E2E scenario for first profile load from authenticated claims in tests/TripPlanner.E2E.Tests/ProfileFlowTests.cs

### Implementation for User Story 1

- [X] T016 [US1] Implement create-only ensure and get behavior in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T017 [US1] Implement GET /api/profile handler in src/TripPlanner.Api/Features/UserProfiles/GetProfileEndpoint.cs
- [X] T018 [US1] Map authenticated profile route group and GET endpoint in src/TripPlanner.Api/Features/UserProfiles/UserProfileEndpointRouteBuilderExtensions.cs and src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs
- [X] T019 [US1] Implement GetAsync on the typed profile API client in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs
- [X] T020 [US1] Register IProfileApiClient with the authenticated API token handler in src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs
- [X] T021 [US1] Create the authenticated profile page that loads and displays saved profile values in src/TripPlanner.Web/Components/Pages/Profile.razor
- [X] T022 [US1] Add a signed-in Profile navigation link in src/TripPlanner.Web/Components/Layout/NavMenu.razor

**Checkpoint**: User Story 1 is independently functional and proves Azure claim values seed only missing profiles.

---

## Phase 4: User Story 2 - Manage Notification Preferences (Priority: P2)

**Goal**: A signed-in user can save notification preferences, and contact-dependent notification channels cannot be enabled without valid contact details.

**Independent Test**: Update notification preferences for a signed-in user, verify the saved preferences display on the profile page, and confirm invalid email-dependent settings are rejected without losing previous valid values.

### Tests for User Story 2

- [X] T023 [P] [US2] Add repository tests for notification preference persistence in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileNotificationPreferenceTests.cs
- [X] T024 [P] [US2] Add API validation tests for email-dependent notification preferences in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfileNotificationTests.cs
- [X] T025 [P] [US2] Add web component tests for notification preference controls and validation errors in tests/TripPlanner.Web.Tests/Profile/ProfileNotificationPreferenceTests.cs

### Implementation for User Story 2

- [X] T026 [US2] Implement notification preference fields in profile update SQL in src/TripPlanner.Database/Scripts/Commands/UserProfiles/UpdateUserProfile.sql
- [X] T027 [US2] Implement notification preference mapping in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T028 [US2] Add contact-dependent notification validation rules in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs
- [X] T029 [US2] Implement PUT /api/profile notification preference update behavior in src/TripPlanner.Api/Features/UserProfiles/UpdateProfileEndpoint.cs
- [X] T030 [US2] Add SaveAsync support for notification preferences in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs
- [X] T031 [US2] Add notification preference controls and validation messages in src/TripPlanner.Web/Components/Pages/Profile.razor

**Checkpoint**: User Story 2 is independently testable after US1 and supports notification preference updates.

---

## Phase 5: User Story 3 - Apply Personalization Settings (Priority: P3)

**Goal**: A signed-in user can save optional personalization preferences without blocking core trip planning when those preferences are blank.

**Independent Test**: Add, change, and remove personalization preferences for a signed-in user and confirm the current preferences remain scoped to that user.

### Tests for User Story 3

- [X] T032 [P] [US3] Add repository tests for personalization preference persistence and clearing optional values in tests/TripPlanner.Database.Tests/UserProfiles/UserProfilePersonalizationTests.cs
- [X] T033 [P] [US3] Add API tests for saving and clearing personalization preferences in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfilePersonalizationTests.cs
- [X] T034 [P] [US3] Add web component tests for personalization fields in tests/TripPlanner.Web.Tests/Profile/ProfilePersonalizationTests.cs

### Implementation for User Story 3

- [X] T035 [US3] Implement personalization preference fields in profile update SQL in src/TripPlanner.Database/Scripts/Commands/UserProfiles/UpdateUserProfile.sql
- [X] T036 [US3] Implement personalization mapping in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T037 [US3] Extend PUT /api/profile personalization update behavior in src/TripPlanner.Api/Features/UserProfiles/UpdateProfileEndpoint.cs
- [X] T038 [US3] Extend profile API client save support for personalization preferences in src/TripPlanner.Web/Features/Profile/ProfileApiClient.cs
- [X] T039 [US3] Add personalization preference controls to the profile page in src/TripPlanner.Web/Components/Pages/Profile.razor

**Checkpoint**: User Story 3 is independently testable after US1 and supports optional personalization changes.

---

## Phase 6: User Story 4 - Review and Update Captured Information (Priority: P4)

**Goal**: A signed-in user can review and update captured identity/contact profile fields, with validation preserving the last valid saved values when a submission fails.

**Independent Test**: Edit an existing profile field, save it, sign out and back in, confirm the updated value remains visible, and confirm invalid updates do not replace the previous valid profile.

### Tests for User Story 4

- [X] T040 [P] [US4] Add repository tests for identity/contact profile updates and validation-preserved previous values in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileUpdateTests.cs
- [X] T041 [P] [US4] Add API tests for owner-scoped profile updates, invalid email rejection, and user id immutability in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfileEndpointTests.cs
- [X] T042 [P] [US4] Add web component tests for profile save confirmation and validation failure rendering in tests/TripPlanner.Web.Tests/Profile/ProfileEditTests.cs
- [X] T043 [P] [US4] Add E2E scenario for profile edit persistence across reload or return sign-in in tests/TripPlanner.E2E.Tests/ProfileFlowTests.cs

### Implementation for User Story 4

- [X] T044 [US4] Implement identity/contact field update behavior in src/TripPlanner.Database/Scripts/Commands/UserProfiles/UpdateUserProfile.sql
- [X] T045 [US4] Complete identity/contact update mapping in src/TripPlanner.Database/UserProfiles/UserProfileRepository.cs
- [X] T046 [US4] Complete profile validation for display name, email shape, and user id immutability in src/TripPlanner.Api/Features/UserProfiles/UserProfileValidator.cs
- [X] T047 [US4] Complete PUT /api/profile response and error handling in src/TripPlanner.Api/Features/UserProfiles/UpdateProfileEndpoint.cs
- [X] T048 [US4] Add editable identity/contact fields, save confirmation, and retryable error states in src/TripPlanner.Web/Components/Pages/Profile.razor

**Checkpoint**: User Story 4 is independently testable after US1 and supports full profile review and update behavior.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and integration checks that affect the complete feature.

- [X] T049 [P] Update profile feature documentation and scenario notes in specs/005-user-info-capture/quickstart.md
- [X] T050 [P] Add profile feature styling refinements in src/TripPlanner.Web/wwwroot/app.css
- [X] T051 Review profile errors for consistency with existing ApiError patterns in src/TripPlanner.Contracts/Errors/ApiError.cs
- [X] T052 Run database test suite for profile persistence in tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
- [X] T053 Run API test suite for profile endpoints in tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
- [X] T054 Run web component test suite for profile page behavior in tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
- [X] T055 Run E2E profile flow validation in tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
- [X] T056 Run full solution validation in TripPlanner.slnx

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion and blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion and is the MVP.
- **User Story 2 (Phase 4)**: Depends on US1 for the profile route, repository, API client, and profile page shell.
- **User Story 3 (Phase 5)**: Depends on US1 for the profile route, repository, API client, and profile page shell.
- **User Story 4 (Phase 6)**: Depends on US1 and benefits from US2/US3 if full profile editing is delivered together.
- **Polish (Phase 7)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: Required MVP; no dependency on other stories after Foundation.
- **US2 (P2)**: Requires US1 profile load/save infrastructure; independently verifies notification preference behavior.
- **US3 (P3)**: Requires US1 profile load/save infrastructure; independently verifies optional personalization behavior.
- **US4 (P4)**: Requires US1 profile load/save infrastructure; independently verifies profile review and identity/contact updates.

### Within Each User Story

- Write tests first and confirm they fail before implementation.
- Database behavior before API endpoint behavior.
- API endpoints before Blazor client/page integration.
- Core behavior before E2E validation.
- Complete each story checkpoint before moving to the next priority when working sequentially.

### Parallel Opportunities

- T002 and T003 can run in parallel after T001.
- T006, T007, and T008 can run in parallel after T005 defines the target schema shape.
- Test tasks within each user story marked [P] can run in parallel.
- US2 and US3 can proceed in parallel after US1 if developers coordinate edits to shared files such as `UpdateProfileEndpoint.cs`, `UserProfileRepository.cs`, `ProfileApiClient.cs`, and `Profile.razor`.
- Polish documentation and styling tasks T049 and T050 can run in parallel after user-story UI behavior is stable.

---

## Parallel Example: User Story 1

```bash
Task: "T012 [P] [US1] Add repository tests for create-only Azure seeding and no-overwrite behavior in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileRepositorySeedTests.cs"
Task: "T013 [P] [US1] Add API tests for GET /api/profile creation from claims and returning existing saved values in tests/TripPlanner.Api.Tests/UserProfiles/GetProfileEndpointTests.cs"
Task: "T014 [P] [US1] Add web component test for displaying Azure-seeded profile values in tests/TripPlanner.Web.Tests/Profile/ProfilePageSeedTests.cs"
Task: "T015 [P] [US1] Add E2E scenario for first profile load from authenticated claims in tests/TripPlanner.E2E.Tests/ProfileFlowTests.cs"
```

## Parallel Example: User Story 2

```bash
Task: "T023 [P] [US2] Add repository tests for notification preference persistence in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileNotificationPreferenceTests.cs"
Task: "T024 [P] [US2] Add API validation tests for email-dependent notification preferences in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfileNotificationTests.cs"
Task: "T025 [P] [US2] Add web component tests for notification preference controls and validation errors in tests/TripPlanner.Web.Tests/Profile/ProfileNotificationPreferenceTests.cs"
```

## Parallel Example: User Story 3

```bash
Task: "T032 [P] [US3] Add repository tests for personalization preference persistence and clearing optional values in tests/TripPlanner.Database.Tests/UserProfiles/UserProfilePersonalizationTests.cs"
Task: "T033 [P] [US3] Add API tests for saving and clearing personalization preferences in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfilePersonalizationTests.cs"
Task: "T034 [P] [US3] Add web component tests for personalization fields in tests/TripPlanner.Web.Tests/Profile/ProfilePersonalizationTests.cs"
```

## Parallel Example: User Story 4

```bash
Task: "T040 [P] [US4] Add repository tests for identity/contact profile updates and validation-preserved previous values in tests/TripPlanner.Database.Tests/UserProfiles/UserProfileUpdateTests.cs"
Task: "T041 [P] [US4] Add API tests for owner-scoped profile updates, invalid email rejection, and user id immutability in tests/TripPlanner.Api.Tests/UserProfiles/UpdateProfileEndpointTests.cs"
Task: "T042 [P] [US4] Add web component tests for profile save confirmation and validation failure rendering in tests/TripPlanner.Web.Tests/Profile/ProfileEditTests.cs"
Task: "T043 [P] [US4] Add E2E scenario for profile edit persistence across reload or return sign-in in tests/TripPlanner.E2E.Tests/ProfileFlowTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate: run the US1 database, API, web, and E2E tests.
5. Demo the first sign-in profile seed and no-overwrite return sign-in behavior.

### Incremental Delivery

1. Deliver US1 to create and display Azure-seeded profiles.
2. Add US2 to manage notification preferences.
3. Add US3 to manage optional personalization preferences.
4. Add US4 to support full profile review/edit behavior and validation-preserved previous values.
5. Run polish validation across all focused suites and the full solution.

### Parallel Team Strategy

1. Complete Setup and Foundational tasks together.
2. Implement US1 as the MVP.
3. Split US2 and US3 between developers after US1, coordinating shared profile update files.
4. Implement US4 after the update flow is stable.
5. Run focused test suites before the full solution validation.

## Notes

- [P] tasks use different files and can run in parallel unless their descriptions mention shared-file coordination.
- [US1], [US2], [US3], and [US4] labels map directly to the prioritized user stories in spec.md.
- Each user story has a concrete independent test criterion.
- Tests should be written before implementation and should fail before the corresponding implementation task is completed.
- Avoid overwriting existing user profile values with later Azure claim values.
