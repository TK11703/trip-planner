# Tasks: Modern Trip Planner

**Input**: Design documents from `/specs/001-modern-trip-planner/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, contracts/ui-routes.md, quickstart.md

**Tests**: Included because the specification and quickstart require repeatable validation for authentication, owner isolation, data access, UI behavior, and key browser flows.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Each task includes an exact file path

## Path Conventions

- Solution files live at repository root.
- Application projects live under `src/`.
- Test projects live under `tests/`.
- SQL schema, query, and command files live under `src/TripPlanner.Database/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the .NET 10 Aspire solution shape, project references, and dependency baselines required by all slices.

- [X] T001 Create the .NET solution file and source/test directories in `TripPlanner.slnx`
- [X] T002 Create Aspire AppHost project in `src/TripPlanner.AppHost/TripPlanner.AppHost.csproj`
- [X] T003 Create Aspire ServiceDefaults project in `src/TripPlanner.ServiceDefaults/TripPlanner.ServiceDefaults.csproj`
- [X] T004 Create Blazor Web App project in `src/TripPlanner.Web/TripPlanner.Web.csproj`
- [X] T005 Create authenticated Minimal API project in `src/TripPlanner.Api/TripPlanner.Api.csproj`
- [X] T006 Create shared contracts project in `src/TripPlanner.Contracts/TripPlanner.Contracts.csproj`
- [X] T007 Create Dapper/PostgreSQL database project in `src/TripPlanner.Database/TripPlanner.Database.csproj`
- [X] T008 [P] Create API test project with xUnit and WebApplicationFactory packages in `tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`
- [X] T009 [P] Create Web test project with xUnit and bUnit packages in `tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`
- [X] T010 [P] Create Database test project with xUnit, Npgsql, Dapper, and Testcontainers packages in `tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj`
- [X] T011 [P] Create E2E test project with Playwright packages in `tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj`
- [X] T012 Add project references among AppHost, ServiceDefaults, Web, Api, Contracts, Database, and tests in `TripPlanner.slnx`
- [X] T013 Configure shared build settings, nullable, implicit usings, analyzers, and deterministic builds in `Directory.Build.props`
- [X] T014 [P] Configure local secret-safe environment placeholders for Web, API, and PostgreSQL in `src/TripPlanner.AppHost/appsettings.Development.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish cross-cutting architecture that MUST be complete before any user story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T015 Configure Aspire PostgreSQL, API, and Web resources in `src/TripPlanner.AppHost/Program.cs`
- [X] T016 Configure shared health checks, telemetry, service discovery, and resilience defaults in `src/TripPlanner.ServiceDefaults/Extensions.cs`
- [X] T017 Create Web startup extension methods for services, authentication, Razor components, HTTP clients, and middleware in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T018 Create Web pipeline extension methods to keep Program.cs concise in `src/TripPlanner.Web/Extensions/WebApplicationExtensions.cs`
- [X] T019 Create API startup extension methods for auth, authorization, OpenAPI, Dapper/database services, and endpoint groups in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T020 Create API pipeline and endpoint mapping extension methods to keep Program.cs concise in `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T021 Define a current-user abstraction based on validated Azure Entra immutable identifiers in `src/TripPlanner.Api/Security/ICurrentUser.cs`
- [X] T022 Implement current-user claim extraction without trusting request payload owner IDs in `src/TripPlanner.Api/Security/CurrentUser.cs`
- [X] T023 Define shared API error response contracts in `src/TripPlanner.Contracts/Errors/ApiError.cs`
- [X] T024 Define shared validation result helpers for user-recoverable errors in `src/TripPlanner.Contracts/Validation/ValidationProblemDetailsFactory.cs`
- [X] T025 Create PostgreSQL connection factory abstraction in `src/TripPlanner.Database/Connections/IPostgresConnectionFactory.cs`
- [X] T026 Implement environment-driven Npgsql connection factory in `src/TripPlanner.Database/Connections/PostgresConnectionFactory.cs`
- [X] T027 Create SQL file loader abstraction that reads scripts from the database project in `src/TripPlanner.Database/Sql/ISqlFileProvider.cs`
- [X] T028 Implement SQL file provider for `Scripts/Schema`, `Scripts/Queries`, and `Scripts/Commands` in `src/TripPlanner.Database/Sql/SqlFileProvider.cs`
- [X] T029 [P] Create shared clock abstraction for timestamps in `src/TripPlanner.Contracts/Common/IClock.cs`
- [X] T030 [P] Create shared pagination/limit constants for recent-trip lookups in `src/TripPlanner.Contracts/Common/QueryLimits.cs`
- [X] T031 Create database initialization service for local schema setup in `src/TripPlanner.Database/Initialization/DatabaseInitializer.cs`
- [X] T032 Create base schema script with extensions and common timestamp conventions in `src/TripPlanner.Database/Scripts/Schema/000_init.sql`
- [X] T033 Create test fixture for PostgreSQL containers and SQL-file initialization in `tests/TripPlanner.Database.Tests/Infrastructure/PostgresFixture.cs`
- [X] T034 Create API test authentication handler and claims helpers for owner-isolation tests in `tests/TripPlanner.Api.Tests/Infrastructure/TestAuthHandler.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in priority order or in parallel by separate developers.

---

## Phase 3: User Story 1 - Securely access personal trips (Priority: P1) 🎯 MVP

**Goal**: A traveler signs in with Azure Entra and can only list or open trip data owned by their immutable Entra user identifier; anonymous and cross-user access is blocked without private disclosure.

**Independent Test**: Sign in or test as two different users, seed trips for each user, verify anonymous API/UI access is rejected, verify each user only sees their own recent trips/details, and verify cross-user direct IDs return generic denied/not-found responses with audit records.

### Tests for User Story 1

- [X] T035 [P] [US1] Add API contract tests rejecting anonymous recent-trip and detail lookups in `tests/TripPlanner.Api.Tests/Trips/TripAuthorizationTests.cs`
- [X] T036 [P] [US1] Add API owner-isolation tests for recent-trip and direct trip detail lookups in `tests/TripPlanner.Api.Tests/Trips/TripOwnerIsolationTests.cs`
- [X] T037 [P] [US1] Add database tests proving trip queries filter by owner_user_id in `tests/TripPlanner.Database.Tests/Trips/TripQueryOwnershipTests.cs`
- [X] T038 [P] [US1] Add audit tests for denied cross-user trip access without token/secret logging in `tests/TripPlanner.Api.Tests/Audit/AuditEventTests.cs`
- [X] T039 [P] [US1] Add Blazor authorization tests for protected trip pages in `tests/TripPlanner.Web.Tests/Auth/ProtectedRouteTests.cs`

### Implementation for User Story 1

- [X] T040 [P] [US1] Define UserAccount, TripSummary, TripDetail, and AuditEvent contracts in `src/TripPlanner.Contracts/Trips/TripContracts.cs`
- [X] T041 [P] [US1] Define audit contracts for sensitive access and mutation outcomes in `src/TripPlanner.Contracts/Audit/AuditContracts.cs`
- [X] T042 [US1] Create owner-scoped users, trips, and audit_events schema in `src/TripPlanner.Database/Scripts/Schema/001_security_trips_audit.sql`
- [X] T043 [US1] Create owner-scoped recent trips SQL query in `src/TripPlanner.Database/Scripts/Queries/Trips/GetRecentTrips.sql`
- [X] T044 [US1] Create owner-scoped trip detail SQL query with generic missing/denied semantics in `src/TripPlanner.Database/Scripts/Queries/Trips/GetTripDetail.sql`
- [X] T045 [US1] Create audit event insert SQL command in `src/TripPlanner.Database/Scripts/Commands/Audit/InsertAuditEvent.sql`
- [X] T046 [US1] Implement Dapper trip read repository for recent and detail lookups in `src/TripPlanner.Database/Trips/TripReadRepository.cs`
- [X] T047 [US1] Implement Dapper audit repository in `src/TripPlanner.Database/Audit/AuditRepository.cs`
- [X] T048 [US1] Implement recent trips Minimal API slice with auth and owner scoping in `src/TripPlanner.Api/Features/Trips/GetRecentTrips/GetRecentTripsEndpoint.cs`
- [X] T049 [US1] Implement trip detail Minimal API slice with generic denied/not-found behavior in `src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs`
- [X] T050 [US1] Implement API endpoint registration for trip and audit slices in `src/TripPlanner.Api/Features/Trips/TripEndpointRouteBuilderExtensions.cs`
- [X] T051 [US1] Configure Azure Entra API authentication and authorization policy usage in `src/TripPlanner.Api/Extensions/AuthenticationExtensions.cs`
- [X] T052 [US1] Configure Azure Entra OIDC sign-in/sign-out for Blazor Web in `src/TripPlanner.Web/Extensions/AuthenticationExtensions.cs`
- [X] T053 [US1] Create authenticated Web API client that forwards authenticated context to the Minimal API in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`
- [X] T054 [US1] Add generic denied/not-found and re-authentication UI states for protected trip pages in `src/TripPlanner.Web/Components/Trips/TripAccessState.razor`
- [X] T055 [US1] Wire protected route handling for `/trips/{tripId}` and `/trips/new` in `src/TripPlanner.Web/Components/Routes.razor`

**Checkpoint**: User Story 1 is independently functional and testable as the MVP security/data-ownership slice.

---

## Phase 4: User Story 2 - Discover and navigate the trip planning site (Priority: P2)

**Goal**: Visitors and signed-in travelers see a polished responsive landing page, FAQ, about page, navigation, and signed-in recent-trip list or empty state without exposing personal data to anonymous users.

**Independent Test**: Open `/`, `/faq`, and `/about` anonymously and as a signed-in user; verify responsive navigation, public content, no personal fetches on public pages, recent trips for signed-in users, and empty-state behavior for users with no trips.

### Tests for User Story 2

- [X] T056 [P] [US2] Add bUnit tests for anonymous landing page hero and no personal data rendering in `tests/TripPlanner.Web.Tests/Home/LandingPageTests.cs`
- [X] T057 [P] [US2] Add bUnit tests for signed-in recent trips and empty-state rendering in `tests/TripPlanner.Web.Tests/Home/RecentTripsComponentTests.cs`
- [X] T058 [P] [US2] Add bUnit tests for FAQ and about public content fallback states in `tests/TripPlanner.Web.Tests/PublicContent/PublicPageTests.cs`
- [X] T059 [P] [US2] Add Playwright smoke tests for mobile/desktop navigation on `/`, `/faq`, and `/about` in `tests/TripPlanner.E2E.Tests/PublicNavigationTests.cs`

### Implementation for User Story 2

- [X] T060 [P] [US2] Create responsive application layout and navigation shell in `src/TripPlanner.Web/Components/Layout/MainLayout.razor`
- [X] T061 [P] [US2] Create Bootstrap 5.3 theme, spacing, card, and responsive utility styles in `src/TripPlanner.Web/wwwroot/css/app.css`
- [X] T062 [US2] Create modern landing page with hero, call-to-action, recent-trip region, and public fallback in `src/TripPlanner.Web/Components/Pages/Home.razor`
- [X] T063 [US2] Create recent trips component consuming owner-scoped API data only when authenticated in `src/TripPlanner.Web/Components/Trips/RecentTripsList.razor`
- [X] T064 [US2] Create first-trip empty state component linking to the protected create-trip route in `src/TripPlanner.Web/Components/Trips/NoTripsEmptyState.razor`
- [X] T065 [P] [US2] Create FAQ page with data privacy, sign-in, and itinerary management content in `src/TripPlanner.Web/Components/Pages/Faq.razor`
- [X] T066 [P] [US2] Create about page with public product purpose and no personal data calls in `src/TripPlanner.Web/Components/Pages/About.razor`
- [X] T067 [US2] Add public route metadata and navigation links for landing, FAQ, and about pages in `src/TripPlanner.Web/Components/Routes.razor`
- [X] T068 [US2] Add user-friendly unavailable-content fallback component for FAQ/about sections in `src/TripPlanner.Web/Components/PublicContent/PublicContentFallback.razor`

**Checkpoint**: User Story 2 is independently functional and testable without requiring trip creation.

---

## Phase 5: User Story 3 - Create a trip and view details (Priority: P3)

**Goal**: A signed-in traveler creates a valid trip with name/context/date range, sees validation for invalid dates, and opens a details page showing the trip overview and itinerary containers.

**Independent Test**: Sign in, create a trip with valid dates, verify it is saved under the current user, appears in recent trips, opens from `/trips/{tripId}`, and rejects an end date before the start date with a clear recovery message.

### Tests for User Story 3

- [X] T069 [P] [US3] Add API contract tests for POST `/api/trips` validation, ownership, and 201 response in `tests/TripPlanner.Api.Tests/Trips/CreateTripEndpointTests.cs`
- [X] T070 [P] [US3] Add API contract tests for PUT `/api/trips/{tripId}` validation and owner scoping in `tests/TripPlanner.Api.Tests/Trips/UpdateTripEndpointTests.cs`
- [X] T071 [P] [US3] Add database tests for insert/update trip SQL and date validation persistence assumptions in `tests/TripPlanner.Database.Tests/Trips/TripCommandTests.cs`
- [X] T072 [P] [US3] Add bUnit tests for create-trip form validation and success navigation in `tests/TripPlanner.Web.Tests/Trips/CreateTripPageTests.cs`
- [X] T073 [P] [US3] Add Playwright flow test for creating and reopening a trip in `tests/TripPlanner.E2E.Tests/CreateTripFlowTests.cs`

### Implementation for User Story 3

- [X] T074 [P] [US3] Define create/update trip request and response contracts in `src/TripPlanner.Contracts/Trips/TripMutationContracts.cs`
- [X] T075 [US3] Create insert trip SQL command with owner_user_id from authenticated context in `src/TripPlanner.Database/Scripts/Commands/Trips/InsertTrip.sql`
- [X] T076 [US3] Create update trip SQL command filtered by trip_id and owner_user_id in `src/TripPlanner.Database/Scripts/Commands/Trips/UpdateTrip.sql`
- [X] T077 [US3] Implement Dapper trip command repository in `src/TripPlanner.Database/Trips/TripCommandRepository.cs`
- [X] T078 [US3] Implement create trip validation rules for required name and date range in `src/TripPlanner.Api/Features/Trips/CreateTrip/CreateTripValidator.cs`
- [X] T079 [US3] Implement update trip validation rules including date range and existing-item safety in `src/TripPlanner.Api/Features/Trips/UpdateTrip/UpdateTripValidator.cs`
- [X] T080 [US3] Implement create trip Minimal API slice with audit success/failure events in `src/TripPlanner.Api/Features/Trips/CreateTrip/CreateTripEndpoint.cs`
- [X] T081 [US3] Implement update trip Minimal API slice with owner scoping and generic denied/not-found behavior in `src/TripPlanner.Api/Features/Trips/UpdateTrip/UpdateTripEndpoint.cs`
- [X] T082 [US3] Add create/update methods to the Web trip API client in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`
- [X] T083 [US3] Create protected trip creation page with inline validation and recovery messages in `src/TripPlanner.Web/Components/Pages/Trips/NewTrip.razor`
- [X] T084 [US3] Create trip details page showing overview, date range, empty itinerary sections, and planning actions in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- [X] T085 [US3] Create reusable trip form component for create/update flows in `src/TripPlanner.Web/Components/Trips/TripForm.razor`

**Checkpoint**: User Story 3 is independently functional and testable as a create-and-review trip slice.

---

## Phase 6: User Story 4 - Plan trip timeline with legs and tracked items (Priority: P4)

**Goal**: A signed-in traveler adds, updates, removes dated legs and tracked items for their own trip and sees them ordered on a fullcalendar.io timeline covering the full trip date range.

**Independent Test**: Sign in as the trip owner, add a leg and tracked item within range, verify they render on correct timeline days with same-day ordering, update/remove them, verify refresh behavior, and confirm out-of-range/cross-user item mutations are rejected.

### Tests for User Story 4

- [X] T086 [P] [US4] Add API contract tests for trip leg create/update/delete ownership and date validation in `tests/TripPlanner.Api.Tests/TripItems/TripLegEndpointTests.cs`
- [X] T087 [P] [US4] Add API contract tests for tracked item create/update/delete ownership, item type, and date validation in `tests/TripPlanner.Api.Tests/TripItems/TrackedItemEndpointTests.cs`
- [X] T088 [P] [US4] Add API contract tests for GET `/api/trips/{tripId}/timeline` owner scoping and response shape in `tests/TripPlanner.Api.Tests/Timeline/TimelineEndpointTests.cs`
- [X] T089 [P] [US4] Add database tests for leg, tracked item, and timeline projection SQL files in `tests/TripPlanner.Database.Tests/Timeline/TimelineQueryTests.cs`
- [X] T090 [P] [US4] Add bUnit tests for item forms, validation messages, and timeline refresh triggers in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs`
- [X] T091 [P] [US4] Add Playwright flow test for adding timeline items and seeing them on mobile and desktop views in `tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs`

### Implementation for User Story 4

- [X] T092 [P] [US4] Define trip leg, tracked item, and timeline event contracts in `src/TripPlanner.Contracts/TripItems/TripItemContracts.cs`
- [X] T093 [P] [US4] Define fullcalendar.io-compatible timeline response contracts in `src/TripPlanner.Contracts/Timeline/TimelineContracts.cs`
- [X] T094 [US4] Create trip_legs and tracked_items schema with owner_user_id, date indexes, and range constraints in `src/TripPlanner.Database/Scripts/Schema/002_trip_items.sql`
- [X] T095 [US4] Create trip leg insert/update/delete SQL commands filtered by owner_user_id in `src/TripPlanner.Database/Scripts/Commands/TripLegs/UpsertAndDeleteTripLegs.sql`
- [X] T096 [US4] Create tracked item insert/update/delete SQL commands filtered by owner_user_id in `src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql`
- [X] T097 [US4] Create timeline projection SQL combining legs and tracked items for owned trips in `src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql`
- [X] T098 [US4] Implement Dapper trip item repository for legs and tracked items in `src/TripPlanner.Database/TripItems/TripItemRepository.cs`
- [X] T099 [US4] Implement Dapper timeline repository in `src/TripPlanner.Database/Timeline/TimelineRepository.cs`
- [X] T100 [US4] Implement trip leg validation for owned parent trip, required title, date range, and end-after-start rules in `src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs`
- [X] T101 [US4] Implement tracked item validation for owned parent trip, supported item_type, required title, date range, and end-after-start rules in `src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs`
- [X] T102 [US4] Implement trip leg Minimal API slices for create/update/delete in `src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs`
- [X] T103 [US4] Implement tracked item Minimal API slices for create/update/delete in `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`
- [X] T104 [US4] Implement timeline Minimal API slice in `src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs`
- [X] T105 [US4] Register trip item and timeline endpoint groups through extension methods in `src/TripPlanner.Api/Features/TripItems/TripItemEndpointRouteBuilderExtensions.cs`
- [X] T106 [US4] Add trip item and timeline methods to the Web API client in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`
- [X] T107 [US4] Add fullcalendar.io static asset references without jQuery in `src/TripPlanner.Web/Components/App.razor`
- [X] T108 [US4] Create vanilla JavaScript fullcalendar interop module in `src/TripPlanner.Web/wwwroot/js/tripTimeline.js`
- [X] T109 [US4] Create Blazor timeline component with responsive fullcalendar/list options in `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`
- [X] T110 [US4] Create trip leg form component with validation and recovery messages in `src/TripPlanner.Web/Components/TripItems/TripLegForm.razor`
- [X] T111 [US4] Create tracked item form component with item-type, date, and validation controls in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T112 [US4] Integrate item forms, item lists, and timeline refresh behavior into trip details page in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`

**Checkpoint**: User Story 4 is independently functional and testable as the full itinerary timeline slice.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate the full implementation, harden security and container readiness, and complete documentation.

- [X] T113 [P] Add README setup instructions for .NET 10, Aspire, Azure Entra configuration, PostgreSQL, and secret handling in `README.md`
- [X] T114 [P] Add developer configuration examples without secrets in `.env.example`
- [X] T115 [P] Add accessibility and responsive viewport checks for landing, details, FAQ, about, and timeline pages in `tests/TripPlanner.E2E.Tests/AccessibilityAndResponsiveTests.cs`
- [X] T116 Add security regression tests for direct URL tampering, expired auth, and generic denied/not-found messaging in `tests/TripPlanner.Api.Tests/Security/SecurityRegressionTests.cs`
- [X] T117 Add audit coverage for successful and failed trip/item mutations in `tests/TripPlanner.Api.Tests/Audit/AuditMutationTests.cs`
- [X] T118 Validate SQL files are loaded from `src/TripPlanner.Database/Scripts/` rather than embedded in handlers in `tests/TripPlanner.Database.Tests/Sql/SqlFileProviderTests.cs`
- [X] T119 Run formatting and static analysis checks for all projects in `TripPlanner.slnx`
- [X] T120 Run `dotnet build` for the complete solution in `TripPlanner.slnx`
- [X] T121 Run `dotnet test` for unit, component, API, database, and E2E test projects in `TripPlanner.slnx`
- [X] T122 Execute the quickstart validation scenarios and record results in `specs/001-modern-trip-planner/quickstart.md`
- [X] T123 Review Program.cs files for concise extension-method setup and update `src/TripPlanner.Web/Program.cs` and `src/TripPlanner.Api/Program.cs`
- [X] T124 Review container-readiness and environment-driven configuration in `src/TripPlanner.AppHost/Program.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories.
- **User Story 1 (Phase 3, P1)**: Depends on Foundational completion and is the MVP.
- **User Story 2 (Phase 4, P2)**: Depends on Foundational completion; can use US1 recent-trip API when authenticated but public pages are independently testable.
- **User Story 3 (Phase 5, P3)**: Depends on Foundational and benefits from US1 owner-scoped trip reads.
- **User Story 4 (Phase 6, P4)**: Depends on Foundational and requires owned trips from US3 for end-to-end validation.
- **Polish (Phase 7)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 Securely access personal trips**: Can start after Phase 2; no dependencies on other stories.
- **US2 Discover and navigate the site**: Can start after Phase 2; public content is independent, recent trips integrate with US1.
- **US3 Create a trip and view details**: Can start after Phase 2, but should reuse US1 owner-scoped read/audit patterns.
- **US4 Plan timeline with legs and tracked items**: Can start after Phase 2, but full validation requires a trip created by US3 or seeded test data.

### Within Each User Story

- Tests are listed first and should be written to fail before implementation.
- Contracts and SQL schema/queries precede repositories.
- Repositories precede API handlers/endpoints.
- API endpoints precede Web client integration.
- Web components and pages complete the slice.
- Audit and owner-scoping validation must be complete before marking a personal-data slice done.

### Parallel Opportunities

- Setup project creation tasks T008-T011 and T014 can run in parallel once the solution exists.
- Foundational abstractions T029-T030 can run in parallel with database and auth foundations.
- US1 tests T035-T039 can run in parallel; US1 contracts T040-T041 can run in parallel.
- US2 tests T056-T059 can run in parallel; FAQ/about page tasks T065-T066 can run in parallel.
- US3 tests T069-T073 can run in parallel before implementation.
- US4 tests T086-T091 can run in parallel; contracts T092-T093 can run in parallel.
- Public UI work in US2 can proceed in parallel with API/security work in US1 after Phase 2.
- Polish tasks T113-T115 can run in parallel with security regression expansion T116-T118 after relevant stories land.

---

## Parallel Example: User Story 1

```text
Task: "Add API contract tests rejecting anonymous recent-trip and detail lookups in tests/TripPlanner.Api.Tests/Trips/TripAuthorizationTests.cs"
Task: "Add API owner-isolation tests for recent-trip and direct trip detail lookups in tests/TripPlanner.Api.Tests/Trips/TripOwnerIsolationTests.cs"
Task: "Add database tests proving trip queries filter by owner_user_id in tests/TripPlanner.Database.Tests/Trips/TripQueryOwnershipTests.cs"
Task: "Add audit tests for denied cross-user trip access without token/secret logging in tests/TripPlanner.Api.Tests/Audit/AuditEventTests.cs"
Task: "Add Blazor authorization tests for protected trip pages in tests/TripPlanner.Web.Tests/Auth/ProtectedRouteTests.cs"
```

## Parallel Example: User Story 2

```text
Task: "Add bUnit tests for anonymous landing page hero and no personal data rendering in tests/TripPlanner.Web.Tests/Home/LandingPageTests.cs"
Task: "Add bUnit tests for signed-in recent trips and empty-state rendering in tests/TripPlanner.Web.Tests/Home/RecentTripsComponentTests.cs"
Task: "Create FAQ page with data privacy, sign-in, and itinerary management content in src/TripPlanner.Web/Components/Pages/Faq.razor"
Task: "Create about page with public product purpose and no personal data calls in src/TripPlanner.Web/Components/Pages/About.razor"
```

## Parallel Example: User Story 3

```text
Task: "Add API contract tests for POST /api/trips validation, ownership, and 201 response in tests/TripPlanner.Api.Tests/Trips/CreateTripEndpointTests.cs"
Task: "Add API contract tests for PUT /api/trips/{tripId} validation and owner scoping in tests/TripPlanner.Api.Tests/Trips/UpdateTripEndpointTests.cs"
Task: "Add database tests for insert/update trip SQL and date validation persistence assumptions in tests/TripPlanner.Database.Tests/Trips/TripCommandTests.cs"
Task: "Add bUnit tests for create-trip form validation and success navigation in tests/TripPlanner.Web.Tests/Trips/CreateTripPageTests.cs"
```

## Parallel Example: User Story 4

```text
Task: "Add API contract tests for trip leg create/update/delete ownership and date validation in tests/TripPlanner.Api.Tests/TripItems/TripLegEndpointTests.cs"
Task: "Add API contract tests for tracked item create/update/delete ownership, item type, and date validation in tests/TripPlanner.Api.Tests/TripItems/TrackedItemEndpointTests.cs"
Task: "Add API contract tests for GET /api/trips/{tripId}/timeline owner scoping and response shape in tests/TripPlanner.Api.Tests/Timeline/TimelineEndpointTests.cs"
Task: "Add database tests for leg, tracked item, and timeline projection SQL files in tests/TripPlanner.Database.Tests/Timeline/TimelineQueryTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational architecture, auth, SQL loading, and test fixtures.
3. Complete Phase 3: US1 secure personal trip access.
4. **STOP and VALIDATE**: Verify anonymous access rejection, owner-scoped recent/detail queries, generic cross-user denial, and audit events.
5. Demo the secure authenticated shell/API boundary before adding broader UI and creation flows.

### Incremental Delivery

1. Setup + Foundation → solution shape, Aspire composition, auth, database, and test scaffolding.
2. US1 → secure personal data boundary and owner-scoped trip reads.
3. US2 → polished public navigation and recent-trip landing experience.
4. US3 → create and view trips with date validation.
5. US4 → legs, tracked items, and fullcalendar.io timeline.
6. Polish → quickstart validation, security regression tests, responsive/accessibility checks, and container readiness.

### Parallel Team Strategy

1. Team completes Phase 1 and Phase 2 together.
2. After Phase 2:
   - Developer A: US1 security/API/database owner scoping.
   - Developer B: US2 public/responsive Blazor UI.
   - Developer C: US3 trip creation/details using US1 patterns.
   - Developer D: US4 contracts/tests and timeline UI after seeded trip support is available.
3. Integrate by priority, validating each story independently before moving to the next production-ready increment.

---

## Notes

- [P] tasks operate on distinct files and avoid dependencies on incomplete tasks.
- SQL must remain in `src/TripPlanner.Database/Scripts/` and not be embedded in handlers.
- API slices must remain Minimal APIs organized under `src/TripPlanner.Api/Features/`.
- Program.cs setup must stay concise by delegating middleware and endpoint setup to extension methods.
- All owner-scoped personal data must use the immutable Azure Entra user identifier from validated claims, never client-supplied owner IDs.
- FAQ and about pages must remain public and must not fetch or render personal trip data.
