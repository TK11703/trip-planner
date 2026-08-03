# Contract: Relay Message Ingestion Endpoint

**Feature**: 022-accept-email-content | **Endpoint**: `POST /api/email-ingestion/messages`

Replaces `POST /api/email-ingestion/webhook` (Event Grid) and `POST /api/email-ingestion/dev-inject`, both of which are deleted.

## Authorization

- Policy: `EmailIngestionRelay`.
- Requires an authenticated Entra **application** token carrying the `EmailIngestion.Relay` app role claim (`roles`).
- A traveler's user token is **rejected** — this endpoint is not part of the interactive surface.
- The caller's identity is used **only** for authorization. It is never used to determine the owning traveler.

| Condition | Status |
|---|---|
| No/invalid token | `401 Unauthorized` |
| Valid token without the app role | `403 Forbidden` |

## Request

`Content-Type: application/json`

```json
{
  "messageId": "AAMkAGI2...AAA=",
  "sender": "traveler@contoso.com",
  "recipient": "trips@contoso.com",
  "subject": "Your flight confirmation ABC123",
  "receivedAt": "2026-07-31T14:05:00Z",
  "bodyText": "Confirmation ABC123 ... departs 08/12/2026 09:30 ...",
  "bodyHtml": "<html>...</html>",
  "attachments": [
    {
      "fileName": "itinerary.txt",
      "contentType": "text/plain",
      "contentBase64": "SXRpbmVyYXJ5..."
    }
  ]
}
```

### Field rules

| Field | Required | Rules |
|---|---|---|
| `messageId` | no | Stable originating identifier. Strongly preferred — drives duplicate detection. |
| `sender` | yes | Non-empty. Normalized (trimmed, lowercased) before traveler lookup. |
| `recipient` | no | Monitored mailbox address; stored for diagnostics. |
| `subject` | yes | May be empty string, not null. |
| `receivedAt` | yes | ISO-8601 with offset. |
| `bodyText` | conditional | Required unless `bodyHtml` or a text-bearing attachment is supplied. |
| `bodyHtml` | no | Used when `bodyText` is absent. |
| `attachments` | no | Defaults to empty. |
| `attachments[].fileName` | yes | Non-empty. |
| `attachments[].contentType` | yes | MIME type. |
| `attachments[].contentBase64` | yes | Base64. Decoded size ≤ 5 MB; total request ≤ 10 MB. |

## Responses

All successful processing returns `200 OK` with a conclusive `status`. The relay branches on `status`; it never polls (FR-005, FR-006, SC-002).

```json
{
  "status": "parsed",
  "inboxEmailId": "6f1c...",
  "draftIds": ["9ab2...", "4cd7..."],
  "detail": null
}
```

| `status` | Meaning | Relay action |
|---|---|---|
| `parsed` | One or more drafts created; traveler notified | Mark handled |
| `no_content` | Processed, nothing recognizable (`parse_status = 'unsupported'`) | Mark handled |
| `duplicate` | Message already ingested; no new drafts | Mark handled |

### Error responses

| Status | Body `status` | Cause | Relay action |
|---|---|---|---|
| `400 Bad Request` | `invalid_request` | Missing required field, malformed base64, no usable text at all | Dead-letter — do not retry |
| `413 Payload Too Large` | `too_large` | Attachment or request exceeds the cap | Dead-letter |
| `422 Unprocessable Entity` | `unknown_sender` | Sender matched zero or multiple travelers; nothing stored | Dead-letter / notify operator |
| `502 Bad Gateway` | `processing_failed` | Recognition provider unavailable or returned unusable output | Retry with backoff |

`processing_failed` is the only retryable outcome. Because duplicate detection is keyed on `messageId`, a retry after a partial failure is safe.

## Processing Order (normative)

1. Authorize the relay identity and app role.
2. Validate the payload and enforce size caps.
3. Normalize `sender`; resolve to exactly one traveler via `users.email`. Zero or many → `422 unknown_sender`, store nothing.
4. Compute the dedupe hash — `messageId` when present, otherwise `sender|subject|receivedAt`.
5. Decode attachments; extract text from text-based content types.
6. Insert the message. A unique-constraint violation on `(user_id, dedupe_hash)` → `200 duplicate`, stop.
7. Insert attachment rows.
8. Run recognition over subject + body + extracted attachment text.
9. Persist drafts (`review_status = 'pending_review'`), set the terminal `parse_status`, and dispatch the existing notification when drafts were created.
10. Return the outcome.

Steps 8–9 happen **inside the request**. No row is ever left awaiting a later pass.

## Guarantees

- Ingestion never creates, edits, or deletes a trip event (FR-012). Only draft confirmation does.
- The owning traveler always comes from the message sender, never from the caller (FR-008).
- Replaying an identical message produces exactly one draft set (FR-019, SC-005).

## Security Notes

- A sender address is spoofable. The trust boundary is the relay's app-role-gated identity plus the fact that ingestion only ever yields a draft requiring traveler confirmation. Per-traveler inbox addresses are the recorded hardening path.
- Recognition output is treated as untrusted text: it is persisted as draft field values and rendered as text, never executed or interpolated into SQL.
- Attachment bytes are stored and served back only to the owning traveler.
