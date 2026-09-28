# Tasks: Trip Data Chat

**Input**: Design documents from `specs/029-trip-data-chat/`: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [quickstart.md](quickstart.md), and [contracts/](contracts/).

**Tests**: Included because the feature request explicitly requires focused database, API, Web, E2E, and answer-quality/security evaluation. Write focused tests before the behavior they verify; run the solution and quickstart checks in the final phase.

**Organization**: Shared model, package, and pgvector gates precede foundational database/deployment work. User-story phases follow the specification: grounded trip Q&A (US1), verifiable citations and navigation (US2), and access/failure protection (US3). US2 and US3 can proceed independently after US1's API contract is stable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallel work only when it touches separate files and has no dependency on unfinished work.
- **[Story]**: Required for user-story tasks; omitted for setup, foundational, and polish tasks.
- Each task names the repository path(s) it implements or validates.

## Phase 1: Setup (Selection Gates)

**Purpose**: Resolve external technology and operational choices before implementation. Do not silently assume the existing email-parsing model/deployment is suitable.

- [X] T001 Select and verify the Foundry chat and embedding deployments, region, lifecycle/support, privacy, capacity/quota, expected quality, and embedding dimensions; record the non-secret deployment identifiers and settings to be used by `src/TripPlanner.Api/appsettings.json` and `infra/main.bicep` (blocks Foundry-dependent implementation; research prerequisite 1)
- [X] T002 After T001, verify the Microsoft Agent Framework .NET package and Foundry client integration are supported together, select a stable version, and pin it in `Directory.Packages.props` and `src/TripPlanner.Api/TripPlanner.Api.csproj` (blocks agent implementation; research prerequisite 2)
- [X] T003 Verify the supported pgvector extension/image version and the Azure Database for PostgreSQL Flexible Server `azure.extensions` value against `infra/postgres.bicep`, `src/TripPlanner.AppHost/Program.cs`, and the Testcontainers fixtures under `tests/` (research prerequisite 4)
- [X] T004 Set numeric p95 latency, request/message limits, throttle policy, and bounded index backfill/retry targets after T001 and representative data-volume review; place the selected non-secret defaults in `src/TripPlanner.Api/appsettings.json` and environment-specific values in `infra/api.bicep` (research prerequisite 5)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish pgvector availability, additive schema, Dapper surfaces, and deployment identity/configuration before any user-story behavior.

**Critical gate**: T001–T003 must be resolved before their dependent work. Do not add a compute service, durable conversation schema, Foundry database connectivity, or changes to canonical trip ownership/access semantics.

- [X] T005 [P] Add database migration tests in `tests/TripPlanner.Database.Tests/TripDataChat/TripSearchDocumentsMigrationTests.cs` for additive schema creation, uniqueness/foreign-key/check constraints, and safe repeat application; include source-row retention and trip-delete cascade assertions
- [X] T006 [P] Add authorized-candidate query tests in `tests/TripPlanner.Database.Tests/TripDataChat/TripSearchDocumentsRepositoryTests.cs` covering owner, shared user-ID, shared invited-email, viewer, collaborator, and inaccessible trips; assert inaccessible candidates are excluded before vector ranking
- [X] T007 Configure the selected pgvector-enabled PostgreSQL image for local Aspire and Testcontainers in `src/TripPlanner.AppHost/Program.cs`, `tests/TripPlanner.Database.Tests/Infrastructure/PostgresFixture.cs`, `tests/TripPlanner.Database.Tests/Infrastructure/MigrationFixture.cs`, and `tests/TripPlanner.Api.Tests/Infrastructure/PostgresApiFactory.cs`; preserve PostgreSQL 16 compatibility
- [X] T008 Allow-list the verified `vector` extension for production Flexible Server in `infra/postgres.bicep`; retain existing extensions and avoid changing database topology
- [X] T009 Add the next ordered, additive schema migration `src/TripPlanner.Database/Scripts/Schema/017_trip_search_documents.sql` for `trip_search_documents`, stable source identifiers, owner ID, content hash/version, vector/model/dimension metadata, uniqueness and reconciliation indexes; do not copy source text or confirmation codes into the sidecar
- [X] T010 Add SQL under `src/TripPlanner.Database/Scripts/Queries/TripDataChat/` for accessible-trip filtering before vector similarity ranking, stable candidate IDs, canonical trip/leg/item reloads, and bounded stale/orphan reconciliation; implement current owner/share matching equivalent to `GetTripAccess.sql`
- [X] T011 Add the Dapper search-document repository abstraction and implementation in `src/TripPlanner.Database/TripDataChat/ITripSearchDocumentRepository.cs` and `src/TripPlanner.Database/TripDataChat/TripSearchDocumentRepository.cs`; bind T010 SQL through the existing SQL provider and validate configured vector dimensions
- [X] T012 Add non-secret Foundry endpoint/deployment/dimension and chat-limit configuration to `src/TripPlanner.Api/appsettings.json`, wire those values to the existing API resource in `src/TripPlanner.AppHost/Program.cs`, and keep local credentials in user secrets/Azure CLI credentials
- [X] T013 Extend the existing Foundry deployment/reference and API configuration/role flow in `infra/main.bicep`, `infra/api.bicep`, and `infra/rbac-openai.bicep` so the existing API managed identity receives only the required Foundry inference permission at the narrowest supported scope; do not create another app or compute service

**Checkpoint**: The database can apply the new sidecar schema using pgvector in local, test, and production configurations; Dapper can query authorized candidates; Foundry and package selections are explicit.

---

## Phase 3: User Story 1 - Ask Questions About My Trips (Priority: P1) — MVP

**Goal**: A signed-in traveler gets a contextual, trip-grounded answer across currently accessible trips, with typed, validated source citations.

**Independent Test**: Seed known owned and shared trips, legs, and tracked items; ask single-trip, cross-trip, and follow-up questions and verify supported answers use current canonical values, distinguish trips, and contain only validated citations. Verify unavailable retrieval/model dependencies do not turn into invented facts.

### Tests for User Story 1

- [X] T014 [P] [US1] Add request/response and authorization endpoint tests in `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatEndpointTests.cs` for bounded input, authenticated caller identity, prior-user-turn-only context, owners/shared viewers/collaborators, cross-trip distinctions, follow-ups, and no-accessible-trip status
- [X] T015 [P] [US1] Add retrieval and grounding integration cases in `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatRetrievalTests.cs` proving fresh share checks on each turn, SQL filtering before ranking, current canonical source reload after indexed content changes, zero inaccessible source data in model input, and no answer without a validated source
- [X] T016 [P] [US1] Add indexing behavior tests in `tests/TripPlanner.Api.Tests/TripDataChat/TripSearchIndexingTests.cs` for trip/leg/item projection, content-hash idempotency, confirmation-code exclusion, configured vector-dimension mismatch, bounded retry, and AI outage not failing a canonical write

### Implementation for User Story 1

- [X] T017 [US1] Define typed chat request, status, response, and citation DTOs in `src/TripPlanner.Contracts/TripDataChat/TripDataChatContracts.cs` matching `specs/029-trip-data-chat/contracts/chat-api.md`; accept no user/owner/trip permission, model/deployment, URL, HTML, or write-operation fields
- [X] T018 [US1] Implement the API-side retrieval service in `src/TripPlanner.Api/Features/TripDataChat/TripSearchRetrievalService.cs` using the T011 repository; derive caller identity only from validated claims, authorize in SQL before ranking, reload current canonical trip/leg/item values, and exclude deleted or newly inaccessible rows
- [X] T019 [US1] Implement the selected Microsoft Agent Framework/Foundry integration in `src/TripPlanner.Api/Features/TripDataChat/TripDataChatAgent.cs` using the API managed identity; provide only current authorized canonical records and bounded prior user messages, expose no direct PostgreSQL connection or write-capable tools, and return structured citation keys rather than URLs
- [X] T020 [US1] Implement `src/TripPlanner.Api/Features/TripDataChat/TripDataChatHandler.cs` to orchestrate retrieval and generation, validate every model citation key against the current authorized result set, resolve trusted labels from canonical rows, discard invalid citations, and return `insufficient_data` instead of an unsupported factual answer
- [X] T021 [US1] Add the authenticated read-only `POST /api/chat/messages` Minimal API vertical slice in `src/TripPlanner.Api/Features/TripDataChat/AskTripDataEndpoint.cs` and register it through `src/TripPlanner.Api/Features/TripDataChat/TripDataChatEndpointRouteBuilderExtensions.cs` and `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`; enforce message/turn limits and contract status codes
- [X] T022 [US1] Implement source projection, hashing, and embedding upsert/delete behavior in `src/TripPlanner.Api/Features/TripDataChat/TripSearchIndexer.cs`; index only visible trip/leg/item fields, exclude confirmation codes and source-text logging, and leave canonical records authoritative
- [X] T023 [US1] Trigger indexing/reconciliation only after successful canonical mutations in `src/TripPlanner.Api/Features/Trips/CreateTrip/CreateTripEndpoint.cs`, `src/TripPlanner.Api/Features/Trips/UpdateTrip/UpdateTripEndpoint.cs`, `src/TripPlanner.Api/Features/Trips/DeleteTrip/DeleteTripEndpoint.cs`, `src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs`, and `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`; indexing failures must not roll back or fail trip mutations
- [X] T024 [US1] Implement bounded, idempotent backfill and orphan/stale-vector reconciliation inside the existing API process in `src/TripPlanner.Api/Features/TripDataChat/TripSearchReconciliationService.cs`; limit each batch, retry safely, and expose no new worker/container/app in `azure.yaml` or `src/TripPlanner.AppHost/Program.cs`
- [X] T025 [US1] Register the chat handler, agent, retrieval, index, and reconciliation services in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs` and wire selected configuration/health behavior without making API liveness depend on Foundry in `src/TripPlanner.Api/Health/ReadinessChecks.cs`
- [X] T026 [US1] Run and complete `tests/TripPlanner.Database.Tests/TripDataChat/TripSearchDocumentsMigrationTests.cs`, `tests/TripPlanner.Database.Tests/TripDataChat/TripSearchDocumentsRepositoryTests.cs`, `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatEndpointTests.cs`, `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatRetrievalTests.cs`, and `tests/TripPlanner.Api.Tests/TripDataChat/TripSearchIndexingTests.cs`; prove the endpoint cannot mutate trips, legs, items, or shares

**Checkpoint**: The read-only API answers from fresh, authorized canonical data; its structured citations are API-validated and the sidecar can be rebuilt without coupling trip writes to AI availability.

---

## Phase 4: User Story 2 - Verify an Answer Against Its Trips (Priority: P1)

**Goal**: A traveler can identify each supporting trip and open it while preserving the same-tab transcript and drawer state.

**Independent Test**: Receive an answer with trip/leg/item citations, open a citation, verify the existing trip-detail route enforces access, and confirm the same tab restores the transcript and pane. Exercise keyboard operation and narrow/desktop viewports.

### Tests for User Story 2

- [X] T027 [P] [US2] Add bUnit tests in `tests/TripPlanner.Web.Tests/TripDataChat/TripDataChatComponentTests.cs` for the signed-in-only fixed activator, accessible name/controls/expanded state, keyboard/focus/close behavior, responsive drawer states, text-only answer rendering, typed citation labels, and empty/loading states
- [X] T028 [P] [US2] Add session lifecycle tests in `tests/TripPlanner.Web.Tests/TripDataChat/TripChatSessionStoreTests.cs` for epoch namespacing, bounded session storage, corrupt/unavailable storage fallback, same-tab restore, and discard on sign-out, expiry, or changed epoch
- [X] T029 [P] [US2] Add authenticated citation-navigation browser coverage in `tests/TripPlanner.E2E.Tests/TripDataChat/TripDataChatFlowTests.cs` for opening a cited trip, transcript/pane restoration, source identity, and denied/revoked trip access at desktop and narrow viewport sizes

### Implementation for User Story 2

- [X] T030 [US2] Issue and rotate an opaque chat-session epoch in the existing sign-in cookie lifecycle in `src/TripPlanner.Web/Extensions/AuthenticationExtensions.cs`; ensure it is UI-state namespace data only, not an API authorization credential, and discard it on logout/session expiry
- [X] T031 [US2] Add the bounded tab-scoped transcript store in `src/TripPlanner.Web/Features/TripDataChat/TripChatSessionStore.cs` and `src/TripPlanner.Web/wwwroot/js/tripDataChat.js`; use `sessionStorage` keyed by the epoch, keep assistant turns display-only, and fail gracefully without logging content
- [X] T032 [US2] Add `ITripDataChatApiClient` and its authenticated implementation in `src/TripPlanner.Web/Features/TripDataChat/ITripDataChatApiClient.cs` and `src/TripPlanner.Web/Features/TripDataChat/TripDataChatApiClient.cs`; send only the current message and bounded prior user turns and deserialize typed response statuses/citations
- [X] T033 [US2] Add the lower-right authenticated activator and full-height drawer in `src/TripPlanner.Web/Components/Layout/TripDataChat.razor`, host it from `src/TripPlanner.Web/Components/Layout/MainLayout.razor`, and provide labeled input, message list, send/stop/close controls, focus behavior, and correct desktop non-modal/mobile overlay semantics
- [X] T034 [US2] Style the activator and drawer in `src/TripPlanner.Web/Components/Layout/TripDataChat.razor.css` for viewport-height layout, safe-area insets, bounded desktop width, narrow-screen full width, stable scroll regions, and no underlying-page layout shift
- [X] T035 [US2] Render each API-validated citation as a typed control in `src/TripPlanner.Web/Components/Layout/TripDataChat.razor`; construct only `/trips/{tripId}` links from stable IDs, identify optional leg/item sources, and never render model HTML or arbitrary href values
- [X] T036 [US2] Restore epoch-scoped transcript, drawer-open state, and practical scroll position after citation navigation in `src/TripPlanner.Web/Components/Layout/TripDataChat.razor` and `src/TripPlanner.Web/Features/TripDataChat/TripChatSessionStore.cs`; rely on the destination route's existing authorization
- [X] T037 [US2] Complete and run `tests/TripPlanner.Web.Tests/TripDataChat/TripDataChatComponentTests.cs` and `tests/TripPlanner.Web.Tests/TripDataChat/TripChatSessionStoreTests.cs` against the component/session behavior, including anonymous users receiving no chat data calls
- [ ] T038 [US2] Complete and run `tests/TripPlanner.E2E.Tests/TripDataChat/TripDataChatFlowTests.cs` to verify citation navigation restores the pane/transcript, source navigation works, and desktop/mobile layouts remain operable

**Checkpoint**: A citation opens its existing trip route without trusting model-generated links, and the authenticated tab restores only its own in-session transcript.

---

## Phase 5: User Story 3 - Handle Missing Data and Protect Trip Access (Priority: P2)

**Goal**: Missing, ambiguous, unauthorized, throttled, and unavailable cases fail plainly without invented facts, leaked trip data, or loss of the existing transcript.

**Independent Test**: Try empty and insufficient retrieval, anonymous access, revoked sharing, malformed/unsupported citations, throttling, and Foundry/retrieval outages. Verify no inaccessible content reaches the model/response/telemetry and canonical mutations remain unaffected.

### Tests for User Story 3

- [X] T039 [P] [US3] Add API security/failure tests in `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatSecurityTests.cs` for 401 before retrieval, fresh access after share revocation, no-accessible-data without existence disclosure, unrelated/ambiguous questions, unknown model citation keys, no partial citations, and generic retryable 503 responses
- [X] T040 [P] [US3] Add API throttling and content-free telemetry tests in `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatOperationalTests.cs` for 429 behavior, configured bounds, and absence of prompts, answers, retrieved text, confirmation codes, citation labels, and raw identities from logs/traces
- [X] T041 [P] [US3] Add Web failure-state tests in `tests/TripPlanner.Web.Tests/TripDataChat/TripDataChatComponentTests.cs` for insufficient data, no accessible trips, throttling, retryable service failure, preserved transcript, retry behavior, and live status/error announcements

### Implementation for User Story 3

- [X] T042 [US3] Enforce generic authentication, validation, throttling, empty-data, and provider/retrieval failure mappings in `src/TripPlanner.Api/Features/TripDataChat/AskTripDataEndpoint.cs` and `src/TripPlanner.Api/Features/TripDataChat/TripDataChatHandler.cs`; never return provider details, inaccessible-trip existence, partial invalid citations, or unsupported trip facts
- [X] T043 [US3] Add structured content-free chat spans/metrics in `src/TripPlanner.Api/Features/TripDataChat/TripDataChatTelemetry.cs` and instrument `src/TripPlanner.Api/Features/TripDataChat/TripDataChatHandler.cs` with bounded counts, durations, token metadata where supported, citation-validation outcome, and failure category only
- [X] T044 [US3] Render explicit insufficient-data, no-accessible-trips, loading, throttled, and retryable-failure states in `src/TripPlanner.Web/Components/Layout/TripDataChat.razor`; preserve prior transcript on request failure and announce status changes accessibly
- [X] T045 [US3] Complete and run `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatSecurityTests.cs`, `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatOperationalTests.cs`, and the focused chat cases in `tests/TripPlanner.Web.Tests/TripDataChat/TripDataChatComponentTests.cs`; verify zero unauthorized trip leakage and no canonical writes

**Checkpoint**: Every inability to answer is explicit and recoverable; all access is re-evaluated per request and default observability remains content-free.

---

## Phase 6: Polish & Cross-Cutting Validation

**Purpose**: Measure answer quality/security and verify the complete implementation through the feature quickstart and solution suites.

- [X] T046 [P] Add a controlled trip-data evaluation dataset and runner in `tests/TripPlanner.Api.Tests/TripDataChat/TestData/chat-evaluation-cases.json` and `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatEvaluationTests.cs` covering groundedness, cross-trip distinction, follow-ups, citation correctness/completeness, unanswerable questions, inaccessible/revoked-trip leakage, latency, throttling, and provider failure
- [X] T047 Run the evaluation from `tests/TripPlanner.Api.Tests/TripDataChat/TripDataChatEvaluationTests.cs`; report accuracy/follow-up/citation/leakage outcomes against spec targets and retrieval/model/end-to-end p50/p95 latency, keeping dataset/results access-controlled and out of default telemetry
- [ ] T048 Validate the additive migration and all documented setup, auth, sharing, route restoration, responsive, failure, telemetry, and managed-identity checks in `specs/029-trip-data-chat/quickstart.md`; record results there without committing secrets or model payloads
- [ ] T049 Run `dotnet build TripPlanner.slnx` and the focused/full suites `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj`, `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`, `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`, and `dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj`; resolve feature regressions and confirm no new compute service or durable conversation storage was introduced

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 Setup**: T001–T003 resolve Foundry, package, and pgvector gates; T004 depends on the selected model and representative capacity/data assumptions.
- **Phase 2 Foundational**: Depends on the relevant Phase 1 gates; blocks all stories. Database tests (T005–T006) can be authored in parallel with PostgreSQL/Bicep setup (T007–T008), then validate T009–T011.
- **User Story 1 (P1)**: Depends on the model/package gates, schema/repository, and API identity/configuration foundation. Delivers the read-only grounded endpoint and index lifecycle; it is the MVP.
- **User Story 2 (P1)**: Depends on the stable typed API contract from US1. Its UI/session work is independent of US3 and can run in parallel after that contract is available.
- **User Story 3 (P2)**: Depends on the US1 endpoint; its API failure/security work and UI state work can run in parallel with US2 after the API contract is stable.
- **Polish**: Depends on all stories. Evaluation requires selected/deployed model access and representative authorized test data; final build/test/quickstart validation follows implementation.

### Sequential constraints

- T001 precedes T002, T012, T013, and Foundry integration; T003 precedes T007–T011. Do not implement against a guessed model, package API, vector dimension, or production extension setting.
- T005–T006 tests precede T009–T011. T009 precedes T010–T011. T011 and T013 precede API retrieval/agent registration.
- T017 must precede T018–T021 and T032. T018/T019 precede T020; T020 precedes T021. T022 precedes T023–T024.
- T030 precedes T031 and T036. T032 precedes T033/T035. T033 and T035 precede T036–T038.
- T039–T041 tests precede the matching failure/telemetry/UI implementation in T042–T044. Complete T045 after those changes.
- Tasks that edit the same file are intentionally sequential: T020/T021/T042 touch the API endpoint/handler; T033–T036 touch the chat component/session store; T043 instruments the handler after its core implementation.

### Parallel opportunities

- T005, T006, T007, and T008 can proceed in parallel after the pgvector version is confirmed; their test/config/Bicep files are distinct.
- T014–T016 can be authored in parallel because each uses a separate test file.
- After US1 stabilizes its contracts, US2 and US3 are independent story streams. Within them, T027–T029, T039–T041, and T046's dataset/test files can be authored in parallel where their paths differ.
- Avoid parallel edits to `TripDataChatHandler.cs`, `AskTripDataEndpoint.cs`, `TripDataChat.razor`, or `TripChatSessionStore.cs`; their task dependencies above are deliberate.

## Implementation Strategy

### MVP First (User Story 1)

1. Resolve Phase 1 model, package, vector, and operational gates.
2. Complete Phase 2 and verify the additive pgvector schema and authorized Dapper queries.
3. Complete US1 and its database/API tests: freshly authorized retrieval, canonical reload, read-only model answer, validated citation DTOs, and fail-soft indexing.
4. Validate US1 independently before building citation navigation and session UX.

### Incremental Delivery

1. Deliver US1 as the grounded Q&A MVP.
2. Add US2 citation rendering, same-tab session restoration, and responsive accessible navigation.
3. Add US3 operational failure states, throttling, content-free telemetry, and explicit leakage/failure tests.
4. Run evaluation, the feature quickstart, and all required solution suites.

## Notes

- Every implementation task stays within the existing Web, API, Contracts, Database, Aspire, and Bicep surfaces. Do not introduce a separate application/compute service, durable conversation/message table, write-capable agent tool, or direct Foundry-to-PostgreSQL access.
- The sidecar is disposable search metadata only. Current canonical trip/leg/item rows and existing trip access rules remain authoritative.
- A checked task is implementation or validation evidence; tasks requiring unavailable live Foundry or authenticated-browser resources remain unchecked until those checks run.