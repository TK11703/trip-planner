---
description: "Task list for Email Event Ingestion"
---

# Tasks: Email Event Ingestion

**Input**: Design documents from `/specs/021-email-event-ingestion/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md)

**Authentication**: All Event Grid → API communication uses **Azure Managed Identity** (bearer token delivery). No function key. The `dev-inject` endpoint is the local development equivalent, guarded by normal user auth and restricted to the Development environment.

**Tests**: Included — the plan calls for unit tests in `TripPlanner.Api.Tests`, bUnit page tests in `TripPlanner.Web.Tests`, and a Playwright E2E flow.

**Organization**: Tasks are grouped by phase (shared infrastructure first, then user story). The feature touches four projects (`Contracts`, `Database`, `Api`, `Web`) — cross-project tasks in a phase are marked `[P]` when they can proceed concurrently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (from spec.md)
- Exact file paths are included in each task

## Path Conventions

- Contracts: `src/TripPlanner.Contracts/`
- Database: `src/TripPlanner.Database/`
- API: `src/TripPlanner.Api/`
- Web: `src/TripPlanner.Web/`
- Tests: `tests/TripPlanner.Api.Tests/`, `tests/TripPlanner.Web.Tests/`, `tests/TripPlanner.E2E.Tests/`

---

## Phase 1: Contracts & Database Schema (Shared Infrastructure)

**Purpose**: Shared DTOs, enums, database tables, and repositories that every user story depends on. Must be complete before any API or UI work.

- [x] T001 [P] Add `ParseStatus` enum (Pending, Parsed, Failed, Unsupported) and `ReviewStatus` enum (PendingReview, Confirmed, Discarded) in `src/TripPlanner.Contracts/EmailIngestion/ParseStatus.cs` and `ReviewStatus.cs`
- [x] T002 [P] Add `InboxEmailDto` and `ParsedEventDraftDto` in `src/TripPlanner.Contracts/EmailIngestion/` — see plan.md for field lists
- [x] T003 Write database migration `021_email_ingestion.sql` creating `inbox_emails` (id, user_id, sender, subject, body_text, body_html, received_at, dedupe_hash, parse_status) and `parsed_event_drafts` (id, inbox_email_id, user_id, trip_id, trip_leg_id, event_type, title, location, start_local, start_timezone_id, end_local, end_timezone_id, confirmation_code, notes, confidence, review_status, created_at) in `src/TripPlanner.Database/Scripts/Schema/010_email_ingestion.sql`
- [x] T004 [P] Add `GetInboxEmails.sql`, `GetParsedEventDrafts.sql`, `GetParsedEventDraftById.sql` in `src/TripPlanner.Database/Scripts/Queries/EmailIngestion/`
- [x] T005 Create `IInboxEmailRepository` and `InboxEmailRepository` (Dapper) in `src/TripPlanner.Database/EmailIngestion/`
- [x] T006 Create `IParsedEventDraftRepository` and `ParsedEventDraftRepository` (Dapper) in `src/TripPlanner.Database/EmailIngestion/`

**Checkpoint**: Contracts and DB repositories ready — API and Web work can begin.

---

## Phase 2: API Core — Webhook & Parse Pipeline (Blocking Prerequisite)

**Purpose**: The inbound webhook endpoint (managed identity auth), deduplication, raw-email storage, and background parse pipeline that all three user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T007 Add `EmailIngestionPolicy` authorization policy (validates Event Grid managed identity bearer token: audience = API scope, issuer = tenant) and register it in `src/TripPlanner.Api/Extensions/AuthenticationExtensions.cs`
- [x] T008 Create `EmailDeduplicationService` — SHA-256 hash of `sender + subject + received_date` — in `src/TripPlanner.Api/Features/EmailIngestion/EmailDeduplicationService.cs`
- [x] T009 Create `EmailIngestionBackgroundService` (`BackgroundService`) that dequeues stored `inbox_emails` with `ParseStatus.Pending` and invokes `EmailParserService` in `src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionBackgroundService.cs`
- [x] T010 Create `EmailParserService` — calls Azure OpenAI chat completions via `DefaultAzureCredential` (managed identity when hosted, `az login` locally); structured prompt returns `ParsedEventDraft` fields + confidence; sets `ParseStatus.Parsed` on success, `ParseStatus.Failed` on exception, `ParseStatus.Unsupported` when confidence is below threshold — in `src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs`
- [x] T011 Register `Azure.AI.OpenAI.AzureOpenAIClient` (via `DefaultAzureCredential`) and `EmailIngestionBackgroundService` in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs`
- [x] T012 Create `ReceiveEmailWebhookEndpoint` (`POST /api/email-ingestion/webhook`) — handles Event Grid subscription validation handshake; stores raw email (dedup check → conflict on duplicate); returns 200 immediately; enqueues for background parse; secured by `EmailIngestionPolicy` — in `src/TripPlanner.Api/Features/EmailIngestion/ReceiveEmailWebhookEndpoint.cs`
- [x] T013 Create `DevInjectEmailEndpoint` (`POST /api/email-ingestion/dev-inject`) — Development environment only; accepts `RawEmailRequest` (sender, subject, bodyText, bodyHtml?); stores + enqueues same path as webhook; secured by existing user-auth policy — in `src/TripPlanner.Api/Features/EmailIngestion/DevInjectEmailEndpoint.cs`
- [ ] T014 [P] Unit tests for `EmailParserService` — field extraction from sample flight/hotel/car HTML bodies, confidence thresholds, fallback to `ParseStatus.Failed` on OpenAI exception, `ParseStatus.Unsupported` below threshold — in `tests/TripPlanner.Api.Tests/EmailIngestion/EmailParserServiceTests.cs`
- [ ] T015 [P] Unit tests for `ReceiveEmailWebhookEndpoint` — Event Grid handshake response, dedup rejection (409), background enqueue triggered, dev-inject blocked outside Development — in `tests/TripPlanner.Api.Tests/EmailIngestion/ReceiveEmailWebhookEndpointTests.cs`

**Checkpoint**: Emails can be injected (via `dev-inject`) and stored + parsed. Review queue population works.

---

## Phase 3: User Story 1 — Forward Confirmation Email to Trip Inbox (Priority: P1) 🎯 MVP

**Goal**: A traveler forwards a booking email to the inbox; the system parses it and creates a reviewable draft. If unparseble, the email is stored as a pending item and the user is notified.

**Independent Test**: Use `dev-inject` to submit a sample flight confirmation; verify a `ParsedEventDraft` record is created with the correct fields; verify a notification is dispatched; verify a duplicate submission is rejected.

### Tests for User Story 1

- [ ] T016 [P] [US1] Unit test — `EmailIngestionBackgroundService` calls `EmailParserService` on dequeued emails and persists the draft; notification is dispatched when `ParseStatus.Parsed` — in `tests/TripPlanner.Api.Tests/EmailIngestion/EmailIngestionBackgroundServiceTests.cs`

### Implementation for User Story 1

- [x] T017 [US1] Integrate `INotificationService.CreateAsync` into `EmailIngestionBackgroundService`: send in-app notification when a new `ParsedEventDraft` is created with `ReviewStatus.PendingReview` (FR-009) — in `src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionBackgroundService.cs`
- [x] T018 [US1] Register all EmailIngestion endpoints via `EmailIngestionEndpointRouteBuilderExtensions` and wire into `src/TripPlanner.Api/Extensions/WebApplicationExtensions.cs`

**Checkpoint**: End-to-end ingestion pipeline functional. Email stored → parsed → draft queued → notification sent.

---

## Phase 4: User Story 2 — Review and Confirm Parsed Events (Priority: P2)

**Goal**: The user opens the review queue, sees pre-populated editable draft fields, confirms or discards the draft; confirmed drafts become trip events.

**Independent Test**: Inject an email via `dev-inject`, navigate to `/inbox/drafts`, confirm the draft is pre-populated and editable, click Confirm, and verify a `TrackedItem` appears on the trip timeline.

### Tests for User Story 2

- [ ] T019 [P] [US2] bUnit — `InboxDrafts.razor`: pending drafts list renders; confirm action calls `ConfirmDraftEndpoint` and navigates to the trip; discard action calls `DiscardDraftEndpoint`; empty-state message when no drafts — in `tests/TripPlanner.Web.Tests/EmailIngestion/InboxDraftsPageTests.cs`

### Implementation for User Story 2

- [x] T020 [P] [US2] Create `GetDraftListEndpoint` (`GET /api/email-ingestion/drafts`) — returns list of `ParsedEventDraftDto` for the authenticated user with `ReviewStatus.PendingReview` — in `src/TripPlanner.Api/Features/EmailIngestion/GetDraftListEndpoint.cs`
- [x] T021 [P] [US2] Create `UpdateDraftEndpoint` (`PUT /api/email-ingestion/drafts/{id}`) — updates editable fields (title, location, start/end, tripId, tripLegId, etc.) on a draft owned by the user — in `src/TripPlanner.Api/Features/EmailIngestion/UpdateDraftEndpoint.cs`
- [x] T022 [US2] Create `ConfirmDraftEndpoint` (`POST /api/email-ingestion/drafts/{id}/confirm`) — validates ownership; creates a `TrackedItem` via `ITripItemRepository` using draft fields; sets `ReviewStatus.Confirmed`; returns the new `TrackedItemDto` — in `src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs`
- [x] T023 [US2] Create `DiscardDraftEndpoint` (`POST /api/email-ingestion/drafts/{id}/discard`) — validates ownership; sets `ReviewStatus.Discarded` — in `src/TripPlanner.Api/Features/EmailIngestion/DiscardDraftEndpoint.cs`
- [x] T024 [US2] Create `IEmailIngestionApiClient` and `EmailIngestionApiClient` (HttpClient, bearer auth) in `src/TripPlanner.Web/Features/EmailIngestion/`; register in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [x] T025 [US2] Create `InboxDrafts.razor` (`@page "/inbox/drafts"`, `@attribute [Authorize]`, `MainLayout`) — fetches draft list; renders each draft with editable fields (inline or modal); Confirm and Discard buttons; empty-state message; links to trip on confirm — in `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor`
- [x] T026 [US2] Add **Inbox** nav entry in `src/TripPlanner.Web/Components/Layout/NavMenu.razor` linking to `/inbox/drafts`

**Checkpoint**: User can review, edit, confirm, and discard parsed email drafts. Confirmed drafts appear on the trip timeline.

---

## Phase 5: User Story 3 — View Inbox Processing History (Priority: P3)

**Goal**: The user views all received emails with their status and can re-trigger parsing for failed or ignored items.

**Independent Test**: Inject two emails — one that parses successfully and one that fails — navigate to `/inbox/history`, confirm both appear with correct statuses; click Re-process on the failed one and verify the status changes.

### Tests for User Story 3

- [ ] T027 [P] [US3] bUnit — `InboxHistory.razor`: history list renders with subject, received date, and parse status; Re-process button visible only for `ParseStatus.Failed` / `ParseStatus.Unsupported`; empty-state message — in `tests/TripPlanner.Web.Tests/EmailIngestion/InboxHistoryPageTests.cs`

### Implementation for User Story 3

- [x] T028 [P] [US3] Create `GetInboxHistoryEndpoint` (`GET /api/email-ingestion/inbox`) — returns list of `InboxEmailDto` for the authenticated user, ordered by `receivedAt` desc — in `src/TripPlanner.Api/Features/EmailIngestion/GetInboxHistoryEndpoint.cs`
- [x] T029 [P] [US3] Create `ReprocessEmailEndpoint` (`POST /api/email-ingestion/inbox/{id}/reprocess`) — validates ownership; resets `ParseStatus` to `Pending`; enqueues for background parse — in `src/TripPlanner.Api/Features/EmailIngestion/ReprocessEmailEndpoint.cs`
- [x] T030 [US3] Create `IInboxHistoryApiClient` and `InboxHistoryApiClient` (HttpClient, bearer auth) in `src/TripPlanner.Web/Features/InboxHistory/`; register in `src/TripPlanner.Web/Extensions/WebApplicationBuilderExtensions.cs`
- [x] T031 [US3] Create `InboxHistory.razor` (`@page "/inbox/history"`, `@attribute [Authorize]`, `MainLayout`) — fetches history; renders email list with subject, received date, status chip; Re-process button for failed/unsupported items; empty-state message; Back link to `/inbox/drafts` — in `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxHistory.razor`

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: E2E verification, deduplication edge cases, accessibility, and final validation.

- [ ] T032 [P] Playwright E2E — dev-inject flight email → draft appears in `/inbox/drafts` → user confirms → event visible on trip timeline; and: dev-inject duplicate email → verify 409 / no duplicate draft — in `tests/TripPlanner.E2E.Tests/`
- [ ] T033 [P] Verify deduplication edge case: same email forwarded twice; assert only one `inbox_email` row and one `parsed_event_draft` row are created (maps to SC-004/FR-007) — in `tests/TripPlanner.Api.Tests/EmailIngestion/`
- [ ] T034 [P] Accessibility pass — draft review form labels, inbox history table `<thead>` with `scope="col"`, status chip ARIA descriptions — in `InboxDrafts.razor` and `InboxHistory.razor`
- [x] T035 Run the full API and Web test suites: `dotnet test tests/TripPlanner.Api.Tests/` and `dotnet test tests/TripPlanner.Web.Tests/`
- [ ] T036 Infrastructure documentation — update `specs/021-email-event-ingestion/contracts/` with:
  - `webhook-endpoint.md` — Event Grid subscription + managed identity delivery configuration
  - `email-parse-schema.md` — OpenAI prompt contract, output fields, confidence thresholds
- [ ] T037 Update `specs/021-email-event-ingestion/quickstart.md` with dev-inject walkthrough scenarios

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Contracts & DB)**: No dependencies — start immediately (T001–T002 in parallel, T003–T006 after T001).
- **Phase 2 (API Core)**: Depends on Phase 1 — **blocks all user stories**. T007–T013 can proceed sequentially or in pairs; T014–T015 in parallel with implementation.
- **User Stories (Phase 3–5)**: All depend on Phase 2.
  - US1 (P1) is MVP and should come first.
  - US2 (P2) depends on the draft pipeline (Phase 2) and the notification dispatch (T017).
  - US3 (P3) is independent of US2 and only needs the history endpoint (T028).
- **Polish (Phase 6)**: Depends on all targeted user stories.

### Story Dependencies

- **US1**: Needs Phase 2 complete (webhook + parser + background service). Self-contained.
- **US2**: Needs US1 draft pipeline (phases 2–3) and draft endpoints (T020–T023).
- **US3**: Needs Phase 2 (email storage) and history endpoints (T028–T029); otherwise independent of US2.

### Within Each Story

- Write the story's bUnit tests first (T016, T019, T027) and let them fail before implementing.
- API endpoints before API client (e.g., T020 before T024).
- API client before Blazor page (e.g., T024 before T025).

### Parallel Opportunities

- Phase 1: T001 ‖ T002 ‖ T004 (after T003).
- Phase 2: T007 ‖ T008 ‖ T009/T010; T014 ‖ T015.
- Once Phase 2 is complete: US2 API endpoints (T020 ‖ T021) and US3 API endpoints (T028 ‖ T029) can proceed in parallel by different people.
- Polish: T032 ‖ T033 ‖ T034.

---

## Parallel Example: Phase 2

```
# Once Phase 1 is complete, start these together:
T007  EmailIngestionPolicy (webhook auth)
T008  EmailDeduplicationService
T010  EmailParserService (Azure OpenAI, DefaultAzureCredential)
# Then, once T008+T010 are done:
T009  EmailIngestionBackgroundService
T012  ReceiveEmailWebhookEndpoint (needs T007+T008+T009)
T013  DevInjectEmailEndpoint
```

---

## Implementation Strategy

- **MVP first**: Complete Phase 1 → Phase 2 → **Phase 3 (US1)**. This delivers a working ingest-and-parse pipeline testable via `dev-inject` and verifiable in the database, with notifications dispatched.
- **Incremental delivery**: Add **US2** to expose the review queue and confirm/discard flow, then **US3** for processing history and re-process.
- **Managed identity first**: The `EmailIngestionPolicy` (T007) and `DefaultAzureCredential` OpenAI setup (T010–T011) must be in place before the webhook endpoint is wired up. Never fall back to function keys or stored API keys.
- **Validation**: Finish with Phase 6 (E2E, dedup edge case, accessibility, full test run).
