# Phase 0 Research: Relayed Email Content Ingestion

**Feature**: 022-accept-email-content | **Date**: 2026-07-31

## Diagnosis — Where the Feature Went Astray

Feature 021 shipped a *pull-shaped* ingestion pipeline. The intended design is *push-shaped*: an external Logic App monitors the mailbox and calls the API. Five concrete drifts were found in the current code.

### Drift 1 — The API polls its own database on a timer

`src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionBackgroundService.cs` is a `BackgroundService` that loops every 15 seconds, calls `IInboxEmailRepository.GetPendingAsync(20)`, and parses whatever it finds. It is registered via `builder.Services.AddHostedService<EmailIngestionBackgroundService>()`.

This is the "background service monitoring an email box" the user wants gone. Even though it polls Postgres rather than IMAP, it produces exactly the behavior being rejected: ingestion is deferred, the caller gets no outcome, and every API replica runs a competing timer.

### Drift 2 — Ingestion was split into store-now / parse-later

`ReceiveEmailWebhookEndpoint` only inserts a row with `parse_status = 'pending'` and returns `200 OK`. All real work happens later in the background loop. The `inbox_emails_pending_idx` partial index and `GetPendingInboxEmails.sql` exist solely to feed that loop.

Because the relay receives `200 OK` before anything is parsed, it cannot distinguish success from "recognized nothing" or "failed", and cannot dead-letter intelligently.

### Drift 3 — Traveler attribution is wrong (correctness bug)

`ReceiveEmailWebhookEndpoint` resolves the owner with `currentUser.TryGetUserId()`. But Event Grid delivers using the *API's own managed identity*, so that claim identifies the delivery principal, not the traveler who forwarded the email. Every ingested message would be attributed to the machine identity, and `if (string.IsNullOrEmpty(userId)) continue;` silently drops the message otherwise.

`DevInjectEmailEndpoint` masked this in development, because there a real signed-in user *is* the caller — so the path appeared to work locally and could not work in Azure.

This drift is inherited by the Logic App design: the Logic App is also a daemon caller. The fix is structural — resolve the traveler from the message's **sender address**, never from the caller's token.

### Drift 4 — Transport coupling to ACS / Event Grid

The endpoint parses an Event Grid envelope (`Microsoft.Communication.EmailReceived`, `SubscriptionValidationEvent`, `validationCode` handshake). The Logic App does not speak that envelope, and the validation handshake is meaningless for it.

### Drift 5 — Attachments were never supported

There is no attachment field in the request, no column, and no table. The refined requirement explicitly includes attachments.

### Additional finding — the feature is untested

A search across `tests/**` for `EmailIngestion`, `InboxEmail`, and `ParsedEventDraft` returns **zero matches**, even though `specs/021-email-event-ingestion/tasks.md` marks the implementation tasks complete. There is no regression net protecting this rework, so the plan adds tests alongside the change.

## Decisions

### D1 — Replace the webhook with a relay ingestion endpoint

**Decision**: Add `POST /api/email-ingestion/messages` accepting a plain JSON message envelope (sender, recipient, subject, receivedAt, messageId, bodyText, bodyHtml, attachments). Delete `ReceiveEmailWebhookEndpoint`.

**Rationale**: A plain contract is trivial for a Logic App to build with the *Send an HTTP request* / *When a new email arrives* actions, and it removes the Event Grid envelope and validation handshake entirely.

**Alternatives considered**:

- *Keep the Event Grid envelope*: forces the Logic App to fabricate a foreign event shape. Rejected.
- *Multipart form upload for attachments*: harder to author in a Logic App than base64 JSON, and needs custom binding. Rejected for v1.

### D2 — Process synchronously inside the request

**Decision**: Insert the message, run recognition, persist drafts, and send the notification within the request, then return a conclusive outcome. Delete the `BackgroundService`, `GetPendingAsync`, `GetPendingInboxEmails.sql`, and the pending index.

**Rationale**: Directly satisfies FR-005/FR-022 and gives the relay a real result to branch on. A Logic App action tolerates a multi-second call, and its own retry policy replaces the poll loop.

**Alternatives considered**:

- *Queue + worker (Service Bus)*: correct at high volume, but adds infrastructure the constitution does not require and re-creates the deferred processing being removed. Rejected.
- *`202 Accepted` + status polling*: reintroduces polling on the relay side. Rejected.

### D3 — Resolve the traveler from the sender address

**Decision**: Look up `users.email` by the normalized sender address. Exactly one match → that traveler owns the message. Zero or multiple matches → reject with an `unknown_sender` outcome and store nothing.

**Rationale**: Fixes Drift 3. `users.email` is already populated from the signed-in user's claims by `EnsureFromAuthenticatedUserAsync`, so no new capture step is needed.

**Alternatives considered**:

- *Per-traveler inbox addresses (`{userId}@domain`)*: robust against spoofing, but requires mailbox/domain provisioning outside this codebase and a traveler onboarding step. Recorded as the future hardening path.
- *Trip reference in the subject line*: brittle and demands user discipline. Rejected.

**Security note**: A sender address is spoofable. The trust boundary is therefore the relay identity (D4) plus the fact that ingestion only ever produces a *draft* the traveler must confirm — it never writes to a timeline. That is an acceptable posture for v1 and is called out in the contract.

### D4 — Authorize the relay as an application identity with an app role

**Decision**: New policy `EmailIngestionRelay` requiring an authenticated caller **and** the `EmailIngestion.Relay` app role claim. Replace the existing `EmailIngestionPolicy.WebhookPolicy`, which only calls `RequireAuthenticatedUser()`.

**Rationale**: The current webhook policy accepts *any* token the tenant issued — meaning any signed-in traveler could post arbitrary email content for anyone else once D3 makes sender-based attribution possible. Requiring an app role restricts ingestion to the Logic App's managed identity.

**Alternatives considered**:

- *Shared secret / API key header*: a stored credential, weaker than managed identity, and against the existing pattern in this repo. Rejected.
- *Reuse `WebhookPolicy` as-is*: leaves the privilege-escalation hole described above. Rejected.

### D5 — Store attachments in a dedicated table

**Decision**: New `inbox_email_attachments` table (id, inbox_email_id, file_name, content_type, size_bytes, content `bytea`, extracted_text, created_at), with `ON DELETE CASCADE`.

**Rationale**: Keeps `inbox_emails` single-row-per-message, supports many attachments, and lets extracted text be stored beside its source. `bytea` avoids introducing blob storage for the modest sizes involved.

**Alternatives considered**:

- *Azure Blob Storage*: better for large files, but adds a dependency and lifecycle management not needed at the enforced size cap. Rejected for v1.
- *JSON column on `inbox_emails`*: awkward for binary content and size accounting. Rejected.

### D6 — Text extraction scope and size caps

**Decision**: Extract text from `text/*` and JSON attachment content types. Store binary formats (PDF, images) without extraction. Cap total request at 10 MB and per-attachment at 5 MB; reject oversize with a validation outcome.

**Rationale**: Matches the spec assumption, avoids adding a PDF parsing dependency, and bounds memory for synchronous processing.

### D7 — Message-identifier-first deduplication

**Decision**: Extend the dedupe hash to prefer the relay-supplied `messageId`, falling back to the existing `sender|subject|receivedAt` shape. Keep the `inbox_emails_dedupe_uq (user_id, dedupe_hash)` constraint and translate a unique violation into the `duplicate` outcome.

**Rationale**: `messageId` is stable across relay retries, unlike `receivedAt`, which the current implementation truncates to the minute — a retry crossing a minute boundary would currently create a second row. Relying on the existing DB constraint also makes concurrent double delivery safe without a transaction dance.

### D8 — Retire the dev-inject endpoint

**Decision**: Delete `DevInjectEmailEndpoint`.

**Rationale**: It existed only because nothing could push email in locally. The new relay endpoint is directly callable from `curl`/REST client in development, so the extra surface is redundant (and it is the endpoint that hid Drift 3).

### D9 — Preserve the review and history experience

**Decision**: Leave the draft queue, history page, notification wiring, `parsed_event_drafts`, and the confirm/discard/update endpoints unchanged apart from what the above requires.

**Rationale**: FR-024. The correction is to the arrival path only; removing shipped, working traveler-facing UI would exceed the request.

## Resolved Unknowns

| Unknown | Resolution |
|---|---|
| How does the relay authenticate? | Entra app-only token + `EmailIngestion.Relay` app role (D4) |
| How is the traveler identified? | Normalized sender address → `users.email`, exactly one match (D3) |
| What happens to unmatched senders? | Rejected with `unknown_sender`; nothing stored; relay dead-letters (D3) |
| Where do attachments live? | `inbox_email_attachments` with `bytea` content (D5) |
| Which attachments get parsed? | Text-based content types only (D6) |
| How are retries deduplicated? | `messageId`-first hash + existing unique constraint (D7) |
| Is any polling retained? | None. All polling components are deleted (D2) |
