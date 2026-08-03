# Implementation Plan: Relayed Email Content Ingestion

**Branch**: `022-accept-email-content` | **Date**: 2026-07-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/022-accept-email-content/spec.md`

## Summary

Correct the email ingestion architecture so content arrives by **push from an external Logic App** instead of being pulled by the API.

A Logic App monitors the mailbox, and on each new message calls a new endpoint — `POST /api/email-ingestion/messages` — with the sender, subject, body, and any attachments. The API validates the relay's identity, resolves the owning traveler **from the message's sender address**, decodes and stores attachments, runs recognition, persists any drafts, notifies the traveler, and returns a conclusive outcome, all **within that request**.

Everything resembling mailbox monitoring or deferred processing is deleted: the `EmailIngestionBackgroundService` polling loop, its hosted-service registration, the Event Grid webhook, the dev-inject endpoint, the pending-work query and index, and the `'pending'` parse status. See [contracts/removal-manifest.md](contracts/removal-manifest.md).

Feature 021 shipped the *pull* shape. Phase 0 identified five concrete drifts, including a correctness bug: the webhook attributed every email to `ICurrentUser`, but Event Grid authenticates as the API's **own managed identity**, so ingested mail could never be attributed to the right traveler in Azure. The dev-inject endpoint hid this locally because there a real user was the caller. That defect would have been inherited by the Logic App, since it is also a daemon caller — so sender-based attribution is a required part of this correction, not a nice-to-have. Full analysis in [research.md](research.md).

The traveler-facing review experience — draft queue, history page, notifications, confirm/discard — is **preserved unchanged**. This feature replaces how content arrives, not how it is reviewed.

## Technical Context

**Language/Version**: C# on .NET 10 (ASP.NET Core Minimal APIs; Blazor Web App for the existing review UI)

**Primary Dependencies**: Existing — `Microsoft.Identity.Web` (JWT validation, app-role authorization), `Azure.AI.OpenAI` with `DefaultAzureCredential` (recognition), Dapper (data access), Aspire (orchestration), existing `INotificationService`, `ITripItemRepository`, `IUserProfileRepository`. **No new NuGet packages** — attachments are base64 in JSON, and text extraction is limited to text-based content types so no document-parsing library is needed.

**Storage**: PostgreSQL. New migration `011_email_relay_ingestion.sql`: adds `message_id`/`recipient` to `inbox_emails`, adds the `inbox_email_attachments` table, drops `inbox_emails_pending_idx`, and narrows the `parse_status` check to `('parsed','failed','unsupported')`. `parsed_event_drafts` is untouched.

**Testing**: `TripPlanner.Api.Tests` (xUnit) for ingestion outcomes, authorization, sender resolution, attachment handling, deduplication, and a guard that no email-ingestion hosted service is registered. `TripPlanner.Web.Tests` (bUnit) to confirm the existing draft/history pages still render after the endpoint rework. Note: **no email-ingestion tests exist today**, so this feature establishes that coverage.

**Target Platform**: Linux containers on Azure Container Apps, orchestrated locally by Aspire. The Logic App is provisioned outside this repository.

**Project Type**: Web application — Blazor front end, Minimal API middle tier, PostgreSQL backend, shared contracts. The change is concentrated in `TripPlanner.Api` and `TripPlanner.Database`.

**Performance Goals**: Ingestion completes within a single request; recognition dominates. Target p95 under 20s per message, well inside SC-001's 2-minute budget and a Logic App HTTP action timeout. Request body capped at 10 MB, per attachment at 5 MB.

**Constraints**: The API MUST hold no hosted service, timer, or mailbox client for ingestion. The endpoint MUST accept only an app-role-bearing relay identity, and MUST NOT derive the traveler from the caller's token. Ingestion MUST NOT mutate any trip event. Every accepted request MUST return a terminal outcome — no "pending" state may be persisted or reported.

**Scale/Scope**: One new endpoint, one new table, one migration, one new repository, three deletions of endpoints/services, two reworked services, plus tests. Message volume is a handful per traveler per trip.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Trip Planning Domain | PASS | Turns booking confirmations into reviewable trip events on trip legs. |
| II. .NET Application Stack | PASS | C# on .NET 10, existing Blazor UI, Aspire orchestration. No new stack elements. |
| III. Minimal API Vertical Slices | PASS | The endpoint lands in the existing `Features/EmailIngestion` slice with its request/response records and handler colocated; routing stays in the slice's `MapEmailIngestionEndpoints` extension. **Net reduction** in endpoints (three removed, one added). |
| IV. PostgreSQL with Dapper | PASS | New table and repository use Dapper with SQL files under `TripPlanner.Database/Scripts`. No EF. |
| V. Container App Readiness | PASS | **Improved.** Removing the hosted timer eliminates duplicated work across replicas and makes the API purely request-driven, which suits Container Apps scale-to-zero. Relay auth uses managed identity and app roles; no secrets added. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS. Phase 1 adds no project, no package, and no infrastructure dependency; it removes a background worker, narrows an authorization policy that was too permissive, and keeps all data access on Dapper. Attachment bytes stay in PostgreSQL rather than introducing blob storage, consistent with keeping the deployment surface small.

## Project Structure

### Documentation (this feature)

```text
specs/022-accept-email-content/
├── plan.md                          # This file
├── research.md                      # Phase 0: drift diagnosis + decisions D1–D9
├── data-model.md                    # Phase 1: schema changes and state transitions
├── quickstart.md                    # Phase 1: run + validation guide
├── contracts/                       # Phase 1
│   ├── relay-ingestion-endpoint.md  # POST /api/email-ingestion/messages
│   ├── logic-app-relay.md           # Relay/API responsibility boundary
│   └── removal-manifest.md          # Checkable list of what must be deleted
├── checklists/
│   └── requirements.md              # Spec quality checklist
└── tasks.md                         # Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Api/
│   ├── Extensions/
│   │   └── WebApplicationBuilderExtensions.cs   # REMOVE AddHostedService<EmailIngestionBackgroundService>();
│   │                                            #   register attachment repository + relay policy
│   └── Features/EmailIngestion/
│       ├── EmailIngestionBackgroundService.cs   # DELETE — 15s polling loop
│       ├── ReceiveEmailWebhookEndpoint.cs       # DELETE — Event Grid envelope + wrong attribution
│       ├── DevInjectEmailEndpoint.cs            # DELETE — masked the attribution defect
│       ├── IngestRelayMessageEndpoint.cs        # NEW — POST /messages; validate, resolve, process, respond
│       ├── EmailSenderResolver.cs               # NEW — normalized sender -> single traveler via profiles
│       ├── EmailAttachmentTextExtractor.cs      # NEW — decode base64, extract text for text/* + JSON
│       ├── EmailIngestionPolicy.cs              # REWORK — RelayPolicy requires EmailIngestion.Relay role
│       ├── EmailDeduplicationService.cs         # REWORK — prefer messageId in the hash
│       ├── EmailParserService.cs                # REWORK — takes assembled text, returns 0..n drafts
│       ├── ReprocessEmailEndpoint.cs            # REWORK — re-run recognition synchronously
│       └── EmailIngestionEndpointRouteBuilderExtensions.cs  # REWORK — drop webhook/dev-inject groups
├── TripPlanner.Contracts/EmailIngestion/
│   └── EmailIngestionContracts.cs               # ADD relay request/response + attachment records;
│                                                #   REMOVE DevInjectEmailRequest
└── TripPlanner.Database/
    ├── EmailIngestion/
    │   ├── IInboxEmailRepository.cs             # REWORK — drop GetPendingAsync; add messageId/recipient
    │   ├── InboxEmailRepository.cs              # REWORK — surface unique-violation as duplicate
    │   └── IEmailAttachmentRepository.cs        # NEW (+ implementation)
    └── Scripts/
        ├── Schema/011_email_relay_ingestion.sql # NEW migration
        ├── Commands/EmailIngestion/             # ADD InsertEmailAttachment.sql
        └── Queries/EmailIngestion/
            └── GetPendingInboxEmails.sql        # DELETE — polling query

tests/
├── TripPlanner.Api.Tests/EmailIngestion/        # NEW — outcomes, authz, sender resolution,
│                                                #   attachments, dedupe, no-hosted-service guard
└── TripPlanner.Web.Tests/                       # Draft/history pages still render
```

**Structure Decision**: Web application, delivered as a vertical slice inside the existing `TripPlanner.Api/Features/EmailIngestion` folder, with data access in `TripPlanner.Database/EmailIngestion` and shared records in `TripPlanner.Contracts`. `TripPlanner.Web` needs no change, because the review UI it already hosts is intentionally preserved.

## Phased Approach

Ordered so the system is never left without an ingestion path.

1. **Phase A — Data**: migration `011`, attachment repository, `IInboxEmailRepository` rework.
2. **Phase B — Ingestion slice**: relay policy, sender resolver, attachment text extractor, parser rework, new endpoint. Delivers User Story 1.
3. **Phase C — Removal**: delete the background service, webhook, dev-inject, and polling query/index per the removal manifest; rework `ReprocessEmailEndpoint` to run synchronously. Satisfies FR-021 → FR-024.
4. **Phase D — Verification**: API tests for every outcome, the no-hosted-service guard, and Web tests confirming the review surface still works.

Phase C follows B so ingestion is replaced before it is removed.

## Risks

| Risk | Mitigation |
|---|---|
| Sender addresses are spoofable | Relay identity is app-role gated, and ingestion only ever creates a draft requiring traveler confirmation. Per-traveler inbox addresses recorded as the hardening path. |
| Recognition latency approaches the relay timeout | Size caps bound input; `502 processing_failed` is retryable and dedupe makes retries safe. |
| No existing tests over this area | Phase D adds the first coverage alongside the rework. |
| Existing `'pending'` rows in a deployed database | Migration rewrites them to `'failed'` before tightening the constraint, since nothing will process them again. |

## Complexity Tracking

> No constitution violations — this section intentionally left empty.
