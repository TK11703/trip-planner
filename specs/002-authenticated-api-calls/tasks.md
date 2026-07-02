# Tasks: Authenticated API Calls

**Input**: Design documents from `/specs/002-authenticated-api-calls/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/authenticated-api.md, contracts/ui-auth-flows.md, quickstart.md, `.specify/memory/constitution.md`

**Tests**: Included because the feature plan and quickstart explicitly require layered API, web, database, and end-to-end validation for authentication, authorization, ownership, and audit behavior. Write tests first and verify they fail before implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently after shared setup/foundation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and does not depend on another incomplete task.
- **[Story]**: Maps tasks to User Story 1, User Story 2, or User Story 3 from `spec.md`.
- Each task includes exact repository-relative file paths.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare authentication configuration, package references, and test harness entry points without implementing story behavior yet.

- [X] T001 Confirm Microsoft Identity Web, OpenID Connect, and server-side token acquisition package references in `src/TripPlanner.Web/TripPlanner.Web.csproj`
- [X] T002 Confirm JWT bearer and Microsoft Identity Web package references for API token validation in `src/TripPlanner.Api/TripPlanner.Api.csproj`
- [X] T003 [P] Add or normalize non-secret Azure Entra configuration placeholders for `AzureEntra:TenantId`, `AzureEntra:ClientId`, and `AzureEntra:ApiScopes` in `src/TripPlanner.Web/appsettings.json`
- [X] T004 [P] Add or normalize non-secret Azure Entra configuration placeholders for `AzureEntra:TenantId`, `AzureEntra:ClientId`, and `AzureEntra:Audience` in `src/TripPlanner.Api/appsettings.json`
- [X] T005 [P] Add authenticated-call test fixture placeholders for web token handler tests in `tests/TripPlanner.Web.Tests/Auth/AuthenticatedApiTokenHandlerTests.cs`
- [X] T006 [P] Add authenticated-call test fixture placeholders for API bearer validation tests in `tests/TripPlanner.Api.Tests/Trips/AuthenticatedApiContractTests.cs`
- [X] T007 [P] Add authenticated-call browser scenario placeholders for signed-in, anonymous, and cross-user validation in `tests/TripPlanner.E2E.Tests/AuthenticatedApiCallFlowTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core authentication boundary that MUST be complete before any user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 Configure Azure Entra OpenID Connect sign-in and Microsoft Identity Web token acquisition for configured API scopes in `src/TripPlanner.Web/Extensions/AuthenticationExtensions.cs`
- [X] T009 Register server-side token cache support for Microsoft Identity Web token acquisition in `src/TripPlanner.Web/Extensions/AuthenticationExtensions.cs`
- [X] T010 Create centralized bearer-token forwarding delegating handler that uses `ITokenAcquisition` and never logs token values in `src/TripPlanner.Web/Features/Trips/AuthenticatedApiTokenHandler.cs`
- [X] T011 Register the Trip Planner API `HttpClient` with Aspire service discovery and the authenticated delegating handler in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T012 Configure API JWT bearer validation for tenant, issuer, audience, and delegated API scope in `src/TripPlanner.Api/Extensions/AuthenticationExtensions.cs`
- [X] T013 Configure a reusable authenticated-user authorization policy for protected Minimal API endpoint groups in `src/TripPlanner.Api/Extensions/AuthenticationExtensions.cs`
- [X] T014 Harden immutable claim extraction for current user ID from validated Azure Entra claims in `src/TripPlanner.Api/Security/CurrentUser.cs`
- [X] T015 Register `ICurrentUser` and authentication/authorization middleware in API startup extensions in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [X] T016 Ensure API request logging and audit plumbing redact authorization headers, cookies, and token-bearing values in `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T017 Update the API test authentication handler to issue immutable object-id/subject and scope claims for protected endpoint tests in `tests/TripPlanner.Api.Tests/Infrastructure/TestAuthHandler.cs`

**Checkpoint**: Web can acquire an API access token through server-side Microsoft Identity Web, outbound protected API clients attach a bearer header centrally, and the API can validate bearer tokens and derive the immutable current user.

---

## Phase 3: User Story 1 - Signed-in traveler can access personal trip data (Priority: P1) 🎯 MVP

**Goal**: A signed-in traveler can view, create, and update their own trip data through bearer-authenticated web-to-API calls, with ownership derived by the API from validated claims.

**Independent Test**: Sign in as a traveler, open personal trip pages, create or update a trip, and verify the API accepts the bearer-authenticated request as the current user and returns only that traveler's data.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T018 [P] [US1] Add web handler test proving outbound protected trip requests include a bearer authorization header without exposing the token in assertions or logs in `tests/TripPlanner.Web.Tests/Auth/AuthenticatedApiTokenHandlerTests.cs`
- [X] T019 [P] [US1] Add web client test proving recent trip, trip detail, and trip mutation calls use the configured authenticated API client in `tests/TripPlanner.Web.Tests/Home/RecentTripsComponentTests.cs`
- [X] T020 [P] [US1] Add API contract tests proving valid scoped bearer tokens can call `GET /api/trips/recent`, `POST /api/trips`, and `PUT /api/trips/{tripId}` in `tests/TripPlanner.Api.Tests/Trips/AuthenticatedApiContractTests.cs`
- [ ] T021 [P] [US1] Add database integration tests proving recent trip and trip detail SQL filters return rows only for the supplied owner user ID in `tests/TripPlanner.Database.Tests/Trips/TripQueryOwnershipTests.cs`
- [ ] T022 [P] [US1] Add browser validation for signed-in recent-trip load and create-trip flow using bearer-authenticated API calls in `tests/TripPlanner.E2E.Tests/AuthenticatedApiCallFlowTests.cs`

### Implementation for User Story 1

- [X] T023 [US1] Update trip API client methods to rely exclusively on the registered authenticated `HttpClient` in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`
- [X] T024 [US1] Require authenticated UI state before loading personal recent trips and render the signed-in empty-state on success with no trips in `src/TripPlanner.Web/Components/Trips/RecentTripsList.razor`
- [X] T025 [US1] Require authenticated UI state before submitting personal trip creation and route calls through `TripApiClient` in `src/TripPlanner.Web/Components/Pages/Trips/NewTrip.razor`
- [X] T026 [US1] Require authenticated UI state before loading or updating trip details and timeline data in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- [X] T027 [US1] Apply the authenticated-user authorization policy to trip endpoint groups for recent, create, detail, and update operations in `src/TripPlanner.Api/Features/Trips/TripEndpointRouteBuilderExtensions.cs`
- [X] T028 [US1] Derive trip create ownership from `ICurrentUser.UserId` rather than request payload values in `src/TripPlanner.Api/Features/Trips/CreateTrip/CreateTripEndpoint.cs`
- [X] T029 [US1] Derive trip update ownership from `ICurrentUser.UserId` and target trip ID in `src/TripPlanner.Api/Features/Trips/UpdateTrip/UpdateTripEndpoint.cs`
- [X] T030 [US1] Derive recent and detail read ownership from `ICurrentUser.UserId` in `src/TripPlanner.Api/Features/Trips/GetRecentTrips/GetRecentTripsEndpoint.cs`
- [X] T031 [US1] Derive detail read ownership from `ICurrentUser.UserId` and target trip ID in `src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs`
- [X] T032 [US1] Record successful owner-scoped trip read/create/update access outcomes without token values in `src/TripPlanner.Database/Audit/AuditRepository.cs`

**Checkpoint**: User Story 1 is fully functional and testable independently as the MVP bearer-authenticated owner trip flow.

---

## Phase 4: User Story 2 - Anonymous users cannot access protected trip data (Priority: P2)

**Goal**: Anonymous visitors retain access to public pages but are prompted to sign in for personal pages, and direct anonymous or invalid API calls are denied without personal data.

**Independent Test**: Attempt to load protected trip data without signing in and verify the web experience prompts sign-in or the API returns a generic authorization failure; then verify FAQ and About remain anonymously accessible and do not call protected endpoints.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T033 [P] [US2] Add web route tests proving anonymous protected trip pages render sign-in recovery and do not display personal trip data in `tests/TripPlanner.Web.Tests/Auth/ProtectedRouteTests.cs`
- [X] T034 [P] [US2] Add public-page tests proving Home, FAQ, and About remain anonymous and do not trigger protected trip API calls in `tests/TripPlanner.Web.Tests/PublicContent/PublicPageTests.cs`
- [X] T035 [P] [US2] Add API security tests proving missing, malformed, expired, wrong-audience, or missing-scope tokens are denied for protected trip endpoints in `tests/TripPlanner.Api.Tests/Security/SecurityRegressionTests.cs`
- [ ] T036 [P] [US2] Add browser validation proving anonymous visitors can open public pages and are redirected or prompted for sign-in before protected trip data loads in `tests/TripPlanner.E2E.Tests/PublicNavigationTests.cs`

### Implementation for User Story 2

- [X] T037 [US2] Mark personal trip routes with authorization requirements while preserving public routing in `src/TripPlanner.Web/Components/Routes.razor`
- [X] T038 [US2] Add sign-in-required and sign-in-again recovery UI state for protected trip access failures in `src/TripPlanner.Web/Components/Trips/TripAccessState.razor`
- [X] T039 [US2] Ensure Home, FAQ, About, and public content components do not load personal trip API data for anonymous users in `src/TripPlanner.Web/Components/Pages/Home.razor`
- [X] T040 [US2] Ensure FAQ remains anonymously accessible and free of protected API calls in `src/TripPlanner.Web/Components/Pages/Faq.razor`
- [X] T041 [US2] Ensure About remains anonymously accessible and free of protected API calls in `src/TripPlanner.Web/Components/Pages/About.razor`
- [X] T042 [US2] Return generic authentication-required or reauthentication-required API results for unauthenticated protected trip calls in `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`
- [X] T043 [US2] Record denied anonymous or invalid-token access outcomes without credentials, cookies, or authorization headers in `src/TripPlanner.Database/Audit/AuditRepository.cs`

**Checkpoint**: User Story 2 can be validated independently with anonymous browser, web component, and direct API scenarios.

---

## Phase 5: User Story 3 - Cross-user access is prevented (Priority: P3)

**Goal**: A signed-in traveler cannot view, change, or confirm the existence of another traveler's protected trip resources, and denied attempts are recorded safely.

**Independent Test**: Sign in as two different travelers, create trip data for each, and verify one traveler cannot view or modify the other's trip, timeline, trip legs, activities, reservations, or tracked items through web or direct API calls.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T044 [P] [US3] Add API owner-isolation tests for trip detail and trip update attempts with a different valid authenticated user in `tests/TripPlanner.Api.Tests/Trips/TripOwnerIsolationTests.cs`
- [ ] T045 [P] [US3] Add API owner-isolation tests for trip legs, tracked items, and timeline endpoint groups with a different valid authenticated user in `tests/TripPlanner.Api.Tests/TripItems/TripLegEndpointTests.cs`
- [ ] T046 [P] [US3] Add timeline owner-isolation tests for direct timeline endpoint access with another user's trip ID in `tests/TripPlanner.Api.Tests/Timeline/TimelineEndpointTests.cs`
- [ ] T047 [P] [US3] Add database command/query tests proving trip item, tracked item, and timeline SQL use owner-scoped predicates in `tests/TripPlanner.Database.Tests/Timeline/TimelineQueryTests.cs`
- [ ] T048 [P] [US3] Add audit tests proving denied cross-user attempts record requester, target reference when safe, denied result, and no secrets in `tests/TripPlanner.Api.Tests/Audit/AuditEventTests.cs`
- [ ] T049 [P] [US3] Add browser validation for signed-in User B attempting to open or mutate User A's trip by direct URL in `tests/TripPlanner.E2E.Tests/AuthenticatedApiCallFlowTests.cs`

### Implementation for User Story 3

- [X] T050 [US3] Apply authenticated-user authorization policy to trip item endpoint groups for legs and tracked items in `src/TripPlanner.Api/Features/TripItems/TripItemEndpointRouteBuilderExtensions.cs`
- [X] T051 [US3] Apply authenticated-user authorization policy to timeline endpoint group in `src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs`
- [X] T052 [US3] Enforce owner-scoped trip-leg mutations by current user ID and trip ID in `src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs`
- [X] T053 [US3] Enforce owner-scoped tracked-item mutations by current user ID and trip ID in `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`
- [X] T054 [US3] Enforce owner-scoped timeline reads by current user ID and trip ID in `src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs`
- [X] T055 [US3] Ensure trip detail and update endpoints return generic not-found or unavailable responses for non-owner requests in `src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs`
- [X] T056 [US3] Ensure owner-scoped trip queries use both resource ID and owner user ID predicates in `src/TripPlanner.Database/Scripts/Queries/Trips/GetTripDetail.sql`
- [X] T057 [US3] Ensure timeline queries use both trip ID and owner user ID predicates in `src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql`
- [X] T058 [US3] Ensure trip leg and tracked item command SQL verifies owned trip membership before mutation in `src/TripPlanner.Database/Scripts/Commands/TripLegs/UpsertAndDeleteTripLegs.sql`
- [X] T059 [US3] Ensure tracked item command SQL verifies owned trip membership before mutation in `src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql`
- [X] T060 [US3] Record denied cross-user access outcomes without resource payloads, token values, or authorization headers in `src/TripPlanner.Database/Scripts/Commands/Audit/InsertAuditEvent.sql`

**Checkpoint**: User Story 3 can be validated independently against direct API, database, audit, and browser flows.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and security hardening across all stories.

- [X] T061 [P] Update implementation and local configuration guidance for non-secret Azure Entra settings in `specs/002-authenticated-api-calls/quickstart.md`
- [X] T062 [P] Update API contract notes with finalized generic response codes and configured scope/audience names in `specs/002-authenticated-api-calls/contracts/authenticated-api.md`
- [X] T063 [P] Update UI authentication recovery contract with finalized route names and recovery copy in `specs/002-authenticated-api-calls/contracts/ui-auth-flows.md`
- [X] T064 [P] Verify no token, cookie, authorization header, client secret, or refresh token value is persisted or logged by searching `src/TripPlanner.Web` and `src/TripPlanner.Api`
- [X] T065 Run the full automated validation suite for authenticated API calls using `TripPlanner.slnx`
- [ ] T066 Run the quickstart validation scenarios for public pages, signed-in owner access, expired-token recovery, anonymous denial, and cross-user denial in `specs/002-authenticated-api-calls/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Stories (Phase 3+)**: Depend on Foundational completion.
  - **US1 (P1)** is the MVP and should be implemented first for a usable authenticated owner flow.
  - **US2 (P2)** can start after Foundational, but should integrate with US1's authenticated client and protected endpoint policy.
  - **US3 (P3)** can start after Foundational, but depends conceptually on owner-scoped patterns established in US1.
- **Polish (Phase 6)**: Depends on completion of the desired user stories.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 2; no dependency on US2 or US3.
- **User Story 2 (P2)**: Starts after Phase 2; independently testable for anonymous/public behavior.
- **User Story 3 (P3)**: Starts after Phase 2; independently testable for cross-user denial, but should reuse US1 ownership abstractions.

### Within Each User Story

- Tests MUST be written first and verified to fail.
- Web/API/database test scaffolding comes before implementation.
- Current-user and token-boundary implementation comes before endpoint ownership changes.
- Endpoint authorization comes before browser/E2E validation.
- Audit recording must be checked before declaring a story complete.

### Parallel Opportunities

- Setup tasks T003-T007 can run in parallel after package-reference confirmation.
- Foundational tasks T010, T012, T014, and T017 can proceed in parallel after T008-T009 decisions are clear.
- US1 tests T018-T022 can run in parallel before implementation.
- US2 tests T033-T036 can run in parallel before implementation.
- US3 tests T044-T049 can run in parallel before implementation.
- Documentation polish tasks T061-T063 can run in parallel after story behavior is finalized.

---

## Parallel Example: User Story 1

```bash
# Write failing US1 tests in parallel:
Task: "Add web handler test proving outbound protected trip requests include a bearer authorization header without exposing the token in tests/TripPlanner.Web.Tests/Auth/AuthenticatedApiTokenHandlerTests.cs"
Task: "Add API contract tests proving valid scoped bearer tokens can call protected trip endpoints in tests/TripPlanner.Api.Tests/Trips/AuthenticatedApiContractTests.cs"
Task: "Add database integration tests proving owner-scoped trip filters in tests/TripPlanner.Database.Tests/Trips/TripQueryOwnershipTests.cs"

# Then implement owner flow in order:
Task: "Update src/TripPlanner.Web/Features/Trips/TripApiClient.cs to use the authenticated API HttpClient"
Task: "Apply authorization in src/TripPlanner.Api/Features/Trips/TripEndpointRouteBuilderExtensions.cs"
Task: "Derive ownership in src/TripPlanner.Api/Features/Trips/CreateTrip/CreateTripEndpoint.cs and related trip endpoints"
```

## Parallel Example: User Story 2

```bash
# Write failing US2 tests in parallel:
Task: "Add anonymous protected route tests in tests/TripPlanner.Web.Tests/Auth/ProtectedRouteTests.cs"
Task: "Add public page no-protected-call tests in tests/TripPlanner.Web.Tests/PublicContent/PublicPageTests.cs"
Task: "Add missing/malformed token denial tests in tests/TripPlanner.Api.Tests/Security/SecurityRegressionTests.cs"

# Then implement public/protected separation:
Task: "Mark personal routes authorized in src/TripPlanner.Web/Components/Routes.razor"
Task: "Add recovery UI in src/TripPlanner.Web/Components/Trips/TripAccessState.razor"
Task: "Return generic auth failures in src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs"
```

## Parallel Example: User Story 3

```bash
# Write failing US3 tests in parallel:
Task: "Add owner-isolation tests in tests/TripPlanner.Api.Tests/Trips/TripOwnerIsolationTests.cs"
Task: "Add timeline owner-isolation tests in tests/TripPlanner.Api.Tests/Timeline/TimelineEndpointTests.cs"
Task: "Add audit denial tests in tests/TripPlanner.Api.Tests/Audit/AuditEventTests.cs"

# Then implement endpoint and SQL owner predicates:
Task: "Enforce owner-scoped trip item mutations in src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs"
Task: "Enforce timeline owner reads in src/TripPlanner.Api/Features/Timeline/GetTripTimelineEndpoint.cs"
Task: "Update SQL predicates in src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup.
2. Complete Phase 2 foundation for Microsoft Identity Web token acquisition, centralized bearer forwarding, JWT bearer validation, immutable current-user extraction, and safe logging.
3. Complete Phase 3 User Story 1.
4. **STOP and VALIDATE**: Run the US1 API, web, database, and E2E validations for signed-in owner access.
5. Demo the MVP: signed-in user can view/create/update only their own trips through authenticated web-to-API calls.

### Incremental Delivery

1. Setup + Foundation → authenticated boundary exists.
2. US1 → signed-in owner access works and is auditable.
3. US2 → anonymous access is blocked for protected data while public pages stay anonymous.
4. US3 → cross-user access is denied generically and audited safely.
5. Polish → documentation, full validation, and no-secret/no-token verification.

### Parallel Team Strategy

1. One developer completes web authentication/token forwarding tasks.
2. One developer completes API JWT/current-user/authorization policy tasks.
3. One developer writes database/audit and endpoint tests.
4. After Phase 2, split by user story while preserving shared conventions and avoiding simultaneous edits to the same files.

## Notes

- Keep Azure Entra tenant IDs, client IDs, scopes, and audiences environment-driven; do not commit client secrets.
- Do not persist or log bearer tokens, ID tokens, refresh tokens, cookies, authorization headers, or client secrets.
- The API must derive owner identity from validated immutable claims, not from request payloads, emails, display names, query strings, or route-provided owner IDs.
- Public pages must remain anonymous and must not trigger protected personal-data API calls for anonymous visitors.
- Generic denied/not-found responses must avoid confirming whether another user's resource exists.
