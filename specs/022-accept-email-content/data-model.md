# Phase 1 Data Model: Relayed Email Content Ingestion

**Feature**: 022-accept-email-content | **Date**: 2026-07-31

Migration file: `src/TripPlanner.Database/Scripts/Schema/011_email_relay_ingestion.sql`

Schema `010_email_ingestion.sql` is left in place (it may already be applied); `011` alters it forward. All statements are idempotent so a fresh database and an existing one converge.

## Entity: Ingested Message (`inbox_emails` — modified)

Existing table, retained. One row per relayed email.

| Column | Type | Notes |
|---|---|---|
| `inbox_email_id` | uuid PK | unchanged |
| `user_id` | text NOT NULL | **now resolved from the sender address**, not the caller's token |
| `sender` | text NOT NULL | unchanged |
| `subject` | text NOT NULL | unchanged |
| `body_text` | text NOT NULL | unchanged |
| `body_html` | text NULL | unchanged |
| `received_at` | timestamptz NOT NULL | supplied by the relay |
| `dedupe_hash` | text NOT NULL | **now derived from `message_id` when present** |
| `parse_status` | text NOT NULL | **`'pending'` removed from the allowed set** |
| `created_at_utc` | timestamptz NOT NULL | unchanged |
| `message_id` | text NULL | **new** — originating mail identifier from the relay |
| `recipient` | text NULL | **new** — monitored mailbox the message arrived at |

### Changes

- **Add** `message_id text NULL`, `recipient text NULL`.
- **Drop** index `inbox_emails_pending_idx` — it existed only to feed the polling loop.
- **Replace** `inbox_emails_parse_status_chk` so the allowed values become `('parsed','failed','unsupported')`. Any surviving `'pending'` rows are migrated to `'failed'` before the constraint is re-applied, because nothing will ever pick them up again.
- **Keep** `inbox_emails_dedupe_uq (user_id, dedupe_hash)` — it becomes the concurrency-safe duplicate guard.
- **Keep** `inbox_emails_user_created_idx` — still serves the history view.

### Validation rules

- `sender`, `subject`, `received_at` required; `body_text` may be empty only when at least one attachment yields text.
- `parse_status` is terminal at insert time — a row is never written in a state awaiting later processing (FR-005, FR-022).

## Entity: Message Attachment (`inbox_email_attachments` — new)

| Column | Type | Notes |
|---|---|---|
| `attachment_id` | uuid PK, default `gen_random_uuid()` | |
| `inbox_email_id` | uuid NOT NULL | FK → `inbox_emails` `ON DELETE CASCADE` |
| `file_name` | text NOT NULL | as supplied by the relay |
| `content_type` | text NOT NULL | MIME type |
| `size_bytes` | integer NOT NULL | decoded length; `CHECK (size_bytes >= 0)` |
| `content` | bytea NOT NULL | decoded attachment bytes |
| `extracted_text` | text NULL | populated only for text-based content types |
| `created_at_utc` | timestamptz NOT NULL, default `timezone('utc', now())` | |

Index: `inbox_email_attachments_email_idx (inbox_email_id)`.

### Validation rules

- `size_bytes` ≤ 5 MB per attachment; total request ≤ 10 MB (D6). Violations reject the whole message.
- `extracted_text` is set when `content_type` starts with `text/` or is a JSON type; otherwise left null (D6).

## Entity: Recognized Event Draft (`parsed_event_drafts` — unchanged)

No schema change. Drafts continue to carry `trip_id`/`trip_leg_id` (nullable until the traveler assigns them), the recognized event fields, `confidence`, and `review_status` in `('pending_review','confirmed','discarded')`.

Relationship: `inbox_emails` 1 → 0..* `parsed_event_drafts` (cascade delete already defined).

## Entity: Trip Event (`tracked_items` — unchanged)

Created only by `ConfirmDraftEndpoint` through the existing `ITripItemRepository`. Ingestion never writes here (FR-012).

## Entity: Relay Identity (not persisted)

The Logic App's application identity, asserted per request by the `EmailIngestion.Relay` app role claim. Deliberately **not** stored and never mapped to `user_id` (FR-008).

## Relationships

```text
users (email)
      │  matched by normalized sender address
      ▼
inbox_emails ──1:N──> inbox_email_attachments
      │
      └────1:N──> parsed_event_drafts ──confirm──> tracked_items
```

## State Transitions

### Ingested message `parse_status`

```text
(insert) ──> parsed        drafts were created
         ──> unsupported   processed, nothing recognizable
         ──> failed        recognition errored
```

There is no `pending` state. A message reaches a terminal status within the ingesting request.

### Draft `review_status`

```text
pending_review ──confirm──> confirmed
               ──discard──> discarded
```

Unchanged from feature 021.

## Data Removed

| Artifact | Reason |
|---|---|
| `inbox_emails_pending_idx` | supported polling only |
| `'pending'` value in `parse_status` | no deferred processing remains |
| `Scripts/Queries/EmailIngestion/GetPendingInboxEmails.sql` | polling query |
| `IInboxEmailRepository.GetPendingAsync` | polling accessor |
