# Implementation Plan: Email Event Ingestion

**Branch**: `021-email-event-ingestion` | **Date**: 2026-07-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/021-email-event-ingestion/spec.md`

## Summary

Email Event Ingestion lets travelers forward booking confirmation emails (flights, hotels, rental cars, activities) to a system-managed inbox. The system receives each email through **Azure Communication Services (ACS) Email**, publishes a delivery event to **Azure Event Grid**, and delivers it to a new `POST /api/email-ingestion/webhook` endpoint on the existing API using **Azure Managed Identity** — no function key, no shared secret.

The webhook stores the raw email, calls **Azure OpenAI** (also via managed identity) to extract structured event data, and queues a `ParsedEventDraft` for the user to review. From the review queue the user confirms, edits, or discards drafts; confirmed drafts are promoted to full `TrackedItem` trip events via the existing `ITripItemRepository`. Users can also view the full inbox processing history and re-trigger parsing for failed items. The existing notification infrastructure (feature 011) delivers in-app alerts when new drafts are awaiting review.

**Authentication decision — Managed Identity, not Function Key**: Event Grid delivers events to the API webhook using the Container App's **system-assigned managed identity**. The subscription is configured with `deliveryWithResourceIdentity`; Event Grid obtains a ****** scoped to the API's Azure AD application and presents it in the `Authorization` header. The existing `Microsoft.Identity.Web` JWT middleware validates the token under a dedicated `EmailIngestionPolicy` (audience = API scope, issuer = tenant). No Azure Function, no function key, and no stored credential are required. Locally, a development-only `POST /api/email-ingestion/dev-inject` endpoint (guarded by the normal user-auth policy and enabled only in the Development environment) accepts a raw email payload to simulate ingestion without live ACS or Event Grid.

## Technical Context

**Language/Version**: C# on .NET 10 (existing stack: Blazor Web App, Interactive Server render mode; ASP.NET Core Minimal APIs).

**Primary Dependencies**:
- `TripPlanner.Api` — new `EmailIngestion` vertical slice (webhook, draft CRUD, history, dev-inject)
- `TripPlanner.Database` — two new tables (`inbox_emails`, `parsed_event_drafts`) with Dapper repositories and SQL files
- `TripPlanner.Web` — two new Blazor pages (draft review queue, inbox history) + nav entry + two new API client interfaces
- `TripPlanner.Contracts` — new DTOs/enums for `InboxEmail`, `ParsedEventDraft`, `ParseStatus`, `ReviewStatus`
- **Azure Communication Services Email** — inbound email reception; one system-level domain address; user-level inbox address encoded as `{userId}@<acs-domain>` (or a shared address with subject-line trip reference)
- **Azure Event Grid System Topic** — `Microsoft.Communication.EmailReceived` events; subscription uses managed-identity delivery with API audience
- **Azure OpenAI** — chat completions for structured event extraction; credential via `DefaultAzureCredential` (managed identity when hosted, `az login` locally); API key never stored in code

New NuGet packages: `Azure.AI.OpenAI` (server-side only, in `TripPlanner.Api`). No new front-end libraries.

**Storage**: PostgreSQL — two new tables. No changes to existing tables or migrations.

**Testing**:
- `TripPlanner.Api.Tests` — unit tests for the email parser (`EmailParserService`): field extraction, deduplication hash, fallback on OpenAI failure, unsupported formats
- `TripPlanner.Web.Tests` (bUnit) — review queue page (draft list renders, confirm/discard actions, empty state), inbox history page (status list, re-process button)
- `TripPlanner.E2E.Tests` (Playwright) — full flow: dev-inject → draft appears in queue → user confirms → event on timeline

**Target Platform**: Azure Container Apps (existing deployment target); modern evergreen browsers for the Blazor front end.

**Project Type**: Web application (Blazor + Minimal API + PostgreSQL). The feature spans `TripPlanner.Api`, `TripPlanner.Database`, `TripPlanner.Contracts`, and `TripPlanner.Web`.

**Performance Goals**: Webhook acknowledgement (store raw email + enqueue parse) completes within 5 seconds to satisfy Event Grid's delivery timeout. Parse results are available for review within 2 minutes (SC-001). Review-and-confirm UI flow completes in under 60 seconds (SC-003).

**Constraints**:
- Authentication MUST use managed identity delivery; no function key, no API key in code or configuration.
- Deduplication MUST be enforced server-side via a hash of (sender + subject + received-date) to satisfy FR-007/SC-004.
- Raw email body MUST be persisted before any parse attempt to satisfy FR-008 and enable re-processing (FR-011).
- OpenAI call MUST be non-blocking for webhook acknowledgement — parsing runs as a background task after the email is stored.
- The `dev-inject` endpoint MUST be unreachable in non-Development environments.
- Parsed results MUST NOT auto-create trip events; they require explicit user confirmation (FR-004/FR-005).
- All new Blazor pages MUST use `@attribute [Authorize]` and inherit the standard `MainLayout` (no chrome stripping needed).

**Scale/Scope**: One new API slice (7 endpoints), two new DB tables, two new Blazor pages, one new parsing service. Typical usage: a few emails per trip per user; parse throughput not a bottleneck.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Reduces manual data entry for trip events directly sourced from booking emails. |
| II. .NET Application Stack | PASS | C# on .NET 10; Blazor front end; Aspire orchestration unchanged. `Azure.AI.OpenAI` is a server-side NuGet only. |
| III. Minimal API Vertical Slices | PASS | New `EmailIngestion` slice in `TripPlanner.Api.Features.EmailIngestion`; all endpoints colocated with their handlers, validators, and mappings. |
| IV. PostgreSQL with Dapper | PASS | Two new tables; Dapper repositories; SQL files in `TripPlanner.Database`. No EF introduced. |
| V. Container App Readiness | PASS | Managed identity delivery replaces function key; `DefaultAzureCredential` for OpenAI (managed identity when hosted, developer credentials locally); ACS endpoint URL and Event Grid topic are environment-driven (`AzureComms:InboundEndpoint`, `AzureComms:EventGridTopicId`); no local-only assumptions. |

**Post-Design Re-check**: PASS — the feature adds one new API slice, two DB tables (Dapper), two Blazor pages, and a new parsing service. No new project is added; `Azure.AI.OpenAI` is server-only. The `dev-inject` endpoint is gated to the Development environment. Managed identity is used for both Event Grid delivery and OpenAI — no secrets in code.

## Project Structure

### Documentation (this feature)

```text
specs/021-email-event-ingestion/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output (pending)
├── data-model.md        # Phase 1 output (pending)
├── quickstart.md        # Phase 1 output (pending)
├── contracts/           # Phase 1 output (pending)
│   ├── webhook-endpoint.md       # POST /api/email-ingestion/webhook: Event Grid delivery, managed identity auth
│   ├── draft-endpoints.md        # Draft CRUD + confirm/discard endpoints
│   ├── history-endpoint.md       # Inbox history + re-process endpoint
│   └── email-parse-schema.md     # Structured parse output contract (fields, confidence, fallback)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   └── EmailIngestion/
│       ├── InboxEmailDto.cs           # (new) DTO: id, sender, subject, receivedAt, status, parseStatus
│       ├── ParsedEventDraftDto.cs     # (new) DTO: id, inboxEmailId, tripId, tripLegId, eventType, title,
│       │                              #            location, startLocal, startTimezoneId, endLocal,
│       │                              #            endTimezoneId, confirmationCode, notes, confidence, reviewStatus
│       ├── ParseStatus.cs             # (new) enum: Pending, Parsed, Failed, Unsupported
│       └── ReviewStatus.cs            # (new) enum: PendingReview, Confirmed, Discarded
│
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Migrations/
│   │   │   └── 021_email_ingestion.sql        # (new) CREATE TABLE inbox_emails + parsed_event_drafts
│   │   └── Queries/
│   │       └── EmailIngestion/
│   │           ├── GetInboxEmails.sql          # (new) list inbox emails by userId, ordered by receivedAt desc
│   │           ├── GetParsedEventDrafts.sql    # (new) list pending drafts by userId
│   │           └── GetParsedEventDraftById.sql # (new) single draft by id + ownership check
│   └── EmailIngestion/
│       ├── IInboxEmailRepository.cs            # (new)
│       ├── InboxEmailRepository.cs             # (new) Dapper implementation
│       ├── IParsedEventDraftRepository.cs      # (new)
│       └── ParsedEventDraftRepository.cs       # (new) Dapper implementation
│
└── TripPlanner.Api/
    ├── Extensions/
    │   └── WebApplicationBuilderExtensions.cs  # Register EmailIngestion services + OpenAI client
    └── Features/
        └── EmailIngestion/
            ├── EmailIngestionEndpointRouteBuilderExtensions.cs   # (new) register all 7 endpoints
            ├── ReceiveEmailWebhookEndpoint.cs                    # (new) POST /api/email-ingestion/webhook
            │                                                     #   — Event Grid handshake + EmailReceived handler
            │                                                     #   — Validates managed identity ******
            │                                                     #   — Stores raw email; fires background parse
            ├── DevInjectEmailEndpoint.cs                         # (new) POST /api/email-ingestion/dev-inject
            │                                                     #   — Development environment only
            │                                                     #   — Accepts RawEmailRequest; stores + parses same as webhook
            ├── GetDraftListEndpoint.cs                           # (new) GET /api/email-ingestion/drafts
            ├── UpdateDraftEndpoint.cs                            # (new) PUT /api/email-ingestion/drafts/{id}
            ├── ConfirmDraftEndpoint.cs                           # (new) POST /api/email-ingestion/drafts/{id}/confirm
            │                                                     #   — Creates TrackedItem via existing ITripItemRepository
            ├── DiscardDraftEndpoint.cs                           # (new) POST /api/email-ingestion/drafts/{id}/discard
            ├── GetInboxHistoryEndpoint.cs                        # (new) GET /api/email-ingestion/inbox
            ├── ReprocessEmailEndpoint.cs                         # (new) POST /api/email-ingestion/inbox/{id}/reprocess
            ├── EmailParserService.cs                             # (new) Azure OpenAI chat completions parser
            │                                                     #   — DefaultAzureCredential (managed identity / az login)
            │                                                     #   — Structured prompt; returns ParsedEventDraft fields + confidence
            │                                                     #   — Fallback: ParseStatus.Failed when OpenAI unavailable
            ├── EmailDeduplicationService.cs                      # (new) SHA-256 hash of sender+subject+receivedDate
            ├── EmailIngestionBackgroundService.cs                # (new) BackgroundService: dequeues stored emails, runs parser
            └── EmailIngestionPolicy.cs                           # (new) authorization policy for webhook (managed identity JWT)

tests/
├── TripPlanner.Api.Tests/
│   └── EmailIngestion/
│       ├── EmailParserServiceTests.cs          # (new) unit: field extraction, fallback, dedup hash
│       └── ReceiveEmailWebhookEndpointTests.cs # (new) unit: handshake response, dedup enforcement, background enqueue
└── TripPlanner.Web.Tests/
    └── EmailIngestion/
        ├── InboxDraftsPageTests.cs             # (new) bUnit: draft list renders, confirm/discard actions, empty state
        └── InboxHistoryPageTests.cs            # (new) bUnit: history list, re-process button visibility
```

Web front end:

```text
src/TripPlanner.Web/
├── Features/EmailIngestion/
│   ├── IEmailIngestionApiClient.cs             # (new) GetDrafts, ConfirmDraft, DiscardDraft, UpdateDraft
│   └── EmailIngestionApiClient.cs              # (new) HttpClient implementation
├── Features/InboxHistory/
│   ├── IInboxHistoryApiClient.cs               # (new) GetHistory, ReprocessEmail
│   └── InboxHistoryApiClient.cs                # (new) HttpClient implementation
└── Components/Pages/Trips/
    ├── InboxDrafts.razor                        # (new) @page "/inbox/drafts"; review queue
    └── InboxHistory.razor                       # (new) @page "/inbox/history"; processing history
```

**Structure Decision**: Web application. The feature is a vertical slice spanning four projects (`TripPlanner.Contracts`, `TripPlanner.Database`, `TripPlanner.Api`, `TripPlanner.Web`). The API side follows the existing slice pattern (endpoint files + service files colocated under `Features/EmailIngestion/`). Database access follows the existing Dapper + SQL file pattern. The Blazor front end follows the existing `Components/Pages/` convention with two new pages and two new `ITripApiClient`-style service interfaces. **No new project is created.**

## Complexity Tracking

> No constitution violations — this section intentionally left empty.
