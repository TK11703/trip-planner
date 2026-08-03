# Tasks: Relayed Email Content Ingestion

**Input**: Design documents from `/specs/022-accept-email-content/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included. The spec's success criteria are stated as pass-rate percentages (SC-004, SC-005, SC-006, SC-008) that cannot be claimed without automated coverage, and [research.md](research.md) found **zero existing tests** over email ingestion. Phase D of the plan makes verification a required phase.

**Granularity**: High level, as requested. Each task is a coherent unit of work rather than a per-file micro-step. Expand a task in place if it grows beyond one sitting.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

Web application per [plan.md](plan.md): `src/TripPlanner.Api/`, `src/TripPlanner.Database/`, `src/TripPlanner.Contracts/`, `src/TripPlanner.Web/`, `tests/`.

---

## Phase 1: Setup

**Purpose**: Shared shapes and test scaffolding that everything else builds on. No project initialization is needed — this feature lands inside existing projects.

- [X] T001 [P] Add relay ingestion contract records (request, attachment, response with `status`/`inboxEmailId`/`draftIds`/`detail`) to src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs per [contracts/relay-ingestion-endpoint.md](contracts/relay-ingestion-endpoint.md)
- [X] T002 [P] Create the email ingestion test suite scaffolding in tests/TripPlanner.Api.Tests/EmailIngestion/ — shared fixture for an authenticated relay caller, a seeded traveler profile with a known email, and a sample message builder

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, data access, and authorization that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Write migration src/TripPlanner.Database/Scripts/Schema/011_email_relay_ingestion.sql — add `message_id` and `recipient` to `inbox_emails`, create `inbox_email_attachments` with its index, migrate surviving `'pending'` rows to `'failed'`, drop `inbox_emails_pending_idx`, and narrow the `parse_status` check to `('parsed','failed','unsupported')` per [data-model.md](data-model.md)
- [X] T004 [P] Add src/TripPlanner.Database/Scripts/Commands/EmailIngestion/InsertEmailAttachment.sql and extend InsertInboxEmail.sql in the same folder to persist `message_id` and `recipient`
- [X] T005 Add IEmailAttachmentRepository and its Dapper implementation in src/TripPlanner.Database/EmailIngestion/ for inserting and reading attachments by message
- [X] T006 Rework src/TripPlanner.Database/EmailIngestion/IInboxEmailRepository.cs and InboxEmailRepository.cs — remove `GetPendingAsync`, accept `message_id`/`recipient` on insert, and surface a unique-constraint violation as a duplicate result rather than an exception
- [X] T007 Replace the permissive webhook policy in src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionPolicy.cs with an `EmailIngestionRelay` policy requiring the `EmailIngestion.Relay` app role, and register it plus the attachment repository in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs

**Checkpoint**: Schema, persistence, and relay authorization are in place — user story work can begin.

---

## Phase 3: User Story 1 - Relayed Email Becomes a Reviewable Draft (Priority: P1) 🎯 MVP

**Goal**: An authorized relay posts a message with body and attachments; the system resolves the owning traveler from the sender address, processes the content during the request, records drafts, and returns a conclusive outcome.

**Independent Test**: Have the relay submit a flight confirmation whose sender matches a known traveler, and verify the response reports `parsed`, a reviewable draft exists for that traveler, and the trip timeline is unchanged.

### Tests for User Story 1

> Write these first and confirm they fail before implementing.

- [X] T008 [P] [US1] Cover every ingestion outcome in tests/TripPlanner.Api.Tests/EmailIngestion/ — `parsed`, `no_content`, `unknown_sender` (no match and multiple matches), `invalid_request`, `too_large`, and `processing_failed` — asserting status codes and that no trip event is ever created (FR-006, FR-009, FR-010, FR-012, SC-002, SC-006)
- [X] T009 [P] [US1] Cover relay authorization in tests/TripPlanner.Api.Tests/EmailIngestion/ — anonymous rejected 401, authenticated caller without the app role rejected 403, a traveler's own user token rejected, and nothing stored in any rejected case (FR-007, SC-004)

### Implementation for User Story 1

- [X] T010 [P] [US1] Add src/TripPlanner.Api/Features/EmailIngestion/EmailSenderResolver.cs resolving a normalized sender address to exactly one traveler via `users.email`, returning a distinct result for no match and for multiple matches (FR-008, FR-009)
- [X] T011 [P] [US1] Add src/TripPlanner.Api/Features/EmailIngestion/EmailAttachmentTextExtractor.cs to decode base64 content, enforce the 5 MB per-attachment cap, and extract text for `text/*` and JSON content types while retaining unreadable attachments without text (FR-003, FR-004)
- [X] T012 [US1] Rework src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs to accept assembled text (subject + body + extracted attachment text) and return zero or more drafts, keeping the existing guard against copying payment, identity-document, and credential values into draft fields (FR-004, FR-011, FR-018)
- [X] T013 [US1] Add src/TripPlanner.Api/Features/EmailIngestion/IngestRelayMessageEndpoint.cs implementing `POST /api/email-ingestion/messages` in the normative 10-step order from [contracts/relay-ingestion-endpoint.md](contracts/relay-ingestion-endpoint.md) — validate, resolve traveler, dedupe, persist message and attachments, recognize, persist drafts, respond — all within the request (FR-001, FR-002, FR-005, FR-006)
- [X] T014 [US1] Register the new route and enforce the 10 MB request body limit in src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionEndpointRouteBuilderExtensions.cs (FR-010)
- [X] T015 [US1] Raise the existing new-drafts notification on successful recognition using src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionNotificationKeys.cs, and add structured logging that records outcome and traveler without logging message bodies or attachment content (FR-017)

**Checkpoint**: Ingestion works end to end through the relay endpoint. This is the MVP.

---

## Phase 4: User Story 2 - Review and Confirm Recognized Events (Priority: P2)

**Goal**: The traveler's existing review experience keeps working after the ingestion rework, including the one endpoint that depended on the removed `'pending'` state.

**Independent Test**: Open the review queue for a traveler with a draft, edit a field, assign a trip leg, confirm it, and verify the event appears on the timeline with the corrected values.

- [X] T016 [US2] Rework src/TripPlanner.Api/Features/EmailIngestion/ReprocessEmailEndpoint.cs to re-run recognition synchronously and return a conclusive outcome, since resetting a message to `'pending'` is no longer a valid state (FR-024, SC-002)
- [X] T017 [P] [US2] Add regression coverage in tests/TripPlanner.Api.Tests/EmailIngestion/ proving the draft list, update, confirm, and discard endpoints still enforce owner-only access and require a valid trip and leg before confirmation (FR-013, FR-014, FR-015, FR-016, SC-007)
- [X] T018 [P] [US2] Add a bUnit test in tests/TripPlanner.Web.Tests/ confirming src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor and the inbox history page still render after the contract changes (FR-024)

**Checkpoint**: Review and confirmation are verified intact.

---

## Phase 5: User Story 3 - Avoid Reprocessing the Same Message (Priority: P3)

**Goal**: A redelivered or retried message produces exactly one draft set.

**Independent Test**: Submit the identical message twice and verify the second call reports `duplicate` and adds no second draft.

- [X] T019 [US3] Rework src/TripPlanner.Api/Features/EmailIngestion/EmailDeduplicationService.cs to hash the originating `messageId` when supplied and fall back to sender, subject, received timestamp, and body characteristics when it is absent (FR-019, FR-020)
- [X] T020 [US3] Add deduplication coverage in tests/TripPlanner.Api.Tests/EmailIngestion/ — identical resubmission returns `duplicate` with one draft set, a relay retry is safe, and two different messages sharing sender and subject are both processed (SC-005)

**Checkpoint**: All three user stories are complete and independently verifiable.

---

## Phase 6: Removal, Verification & Cross-Cutting Concerns

**Purpose**: Satisfy FR-021 through FR-024 and SC-008 by deleting every mailbox-monitoring and deferred-processing artifact.

**Ordering**: These tasks MUST NOT run before the Phase 3 checkpoint — the replacement ingestion path has to exist before the old one is removed. They may run as soon as Phase 3 passes, in parallel with Phases 4 and 5.

- [X] T021 Delete src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionBackgroundService.cs and its `AddHostedService` registration in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs (FR-021, FR-022)
- [X] T022 [P] Delete src/TripPlanner.Api/Features/EmailIngestion/ReceiveEmailWebhookEndpoint.cs and DevInjectEmailEndpoint.cs, remove their route registrations from EmailIngestionEndpointRouteBuilderExtensions.cs, and drop `DevInjectEmailRequest` from src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs (FR-023)
- [X] T023 [P] Delete src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPendingInboxEmails.sql and remove every remaining reference to the `'pending'` parse status across src/ (FR-023)
- [X] T024 Add a guard test in tests/TripPlanner.Api.Tests/EmailIngestion/ asserting no email-ingestion `IHostedService` is registered in the built application, so the polling loop cannot return (FR-021, FR-022, SC-008)
- [X] T025 Work through [quickstart.md](quickstart.md) end to end and confirm every row of [contracts/removal-manifest.md](contracts/removal-manifest.md), including the source sweep proving no mailbox-monitoring component remains (SC-008)

---

## Dependencies

**Phase order**: Phase 1 → Phase 2 → Phase 3 → {Phase 4, Phase 5, Phase 6}.

**Story dependencies**:

- **US1** depends only on Phases 1–2. It is the MVP and is independently deliverable.
- **US2** depends on US1 because T016 must return the outcome shape introduced in T013.
- **US3** depends on US1 because deduplication is invoked from the ingestion endpoint.
- US2 and US3 touch different files and can proceed concurrently once US1 is done.

**Notable task dependencies**:

- T005 and T006 depend on T003 (schema must exist) and T004 (SQL scripts).
- T013 depends on T010, T011, T012, T006, and T007.
- T016 depends on T013. T019 depends on T013.
- T024 depends on T021.

## Parallel Opportunities

| Phase | Can run together |
|---|---|
| 1 | T001, T002 |
| 2 | T004 alongside T003 review; T005 and T006 after T003/T004 land |
| 3 | T008 and T009 (tests); then T010 and T011 |
| 4 | T017 and T018 |
| 6 | T022 and T023 |

Phases 4, 5, and 6 can be worked by separate contributors in parallel after the Phase 3 checkpoint.

## Implementation Strategy

**MVP**: Phases 1–3 (T001–T015). This delivers the entire relay ingestion path and is independently shippable, though the old polling code still exists alongside it.

**Increment 2**: Phase 6 (T021–T025) — remove the old path. Do this promptly after the MVP; leaving both paths live means the buggy managed-identity attribution described in [research.md](research.md) is still reachable.

**Increment 3**: Phases 4 and 5 — review regressions and deduplication.

**Requirement coverage**: FR-001 through FR-024 and SC-001 through SC-008 are each referenced by at least one task above.
