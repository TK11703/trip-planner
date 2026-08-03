# Quickstart: Relayed Email Content Ingestion

**Feature**: 022-accept-email-content

How to run and validate this feature end to end. Implementation detail lives in [plan.md](plan.md) and the [contracts](contracts/); this is the run/verify guide.

## Prerequisites

- .NET 10 SDK
- Docker (Aspire starts the PostgreSQL container)
- An `AzureOpenAI:Endpoint` value reachable by your developer identity (`az login`), since recognition runs through Azure OpenAI with `DefaultAzureCredential`
- A user profile row whose `email` matches the `sender` you will submit — sign in to the web app once so `EnsureFromAuthenticatedUserAsync` records your address

## Run

```powershell
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Use the Aspire dashboard to find the API base URL. Schema `011_email_relay_ingestion.sql` is applied by `DatabaseInitializer` on startup.

## Validate

### 1. The relay path produces a draft (User Story 1, FR-001 → FR-011)

`POST {api}/api/email-ingestion/messages` with a relay token and this body:

```json
{
  "messageId": "quickstart-001",
  "sender": "<your-profile-email>",
  "recipient": "trips@example.com",
  "subject": "Flight confirmation ABC123",
  "receivedAt": "2026-08-01T12:00:00Z",
  "bodyText": "Confirmation ABC123. Depart 08/12/2026 09:30 from SEA, arrive 13:05 ORD.",
  "attachments": []
}
```

Expected: `200 OK`, `status: "parsed"`, at least one id in `draftIds`.

> Getting a token locally: the endpoint requires the `EmailIngestion.Relay` app role. For local runs, assign that role to your developer app registration, or use the API test host (see below) which is the primary way this path is covered.

### 2. Processing is synchronous (FR-005, SC-002)

The response in step 1 already contains `draftIds`. Confirm no row is left waiting:

```sql
SELECT parse_status, count(*) FROM inbox_emails GROUP BY parse_status;
```

Expected: no `pending` rows — the status check constraint no longer allows the value.

### 3. Attachments are accepted and used (FR-003, FR-004)

Repeat step 1 with a new `messageId`, empty `bodyText`, and one `text/plain` attachment whose base64 content holds the booking details.

Expected: `200 parsed`; `inbox_email_attachments` has a row with `extracted_text` populated.

### 4. Unknown senders are rejected (FR-009, SC-004)

Repeat step 1 with `"sender": "nobody@example.com"`.

Expected: `422 Unprocessable Entity`, `status: "unknown_sender"`, and no new `inbox_emails` row.

### 5. Unauthorized callers are rejected (FR-007)

Call with no token, then with a normal traveler's user token.

Expected: `401` and `403` respectively; nothing stored.

### 6. Repeat delivery is deduplicated (FR-019, SC-005)

Submit step 1's payload again, unchanged.

Expected: `200 OK`, `status: "duplicate"`, and the draft count for that message is unchanged.

### 7. Review and confirm (User Story 2, FR-013 → FR-015)

Sign in to the web app, open **Inbox → Drafts**, edit the draft, assign a trip and leg, and confirm.

Expected: the event appears on that leg's timeline with the values you approved; the draft moves to `confirmed`. Before confirming, the timeline shows no event from ingestion (FR-012, SC-006).

### 8. No mailbox monitoring remains (FR-021 → FR-023, SC-008)

```powershell
Select-String -Path src/**/*.cs -Pattern 'AddHostedService|BackgroundService|IHostedService|Imap|Pop3|MailKit|EventGrid|EmailReceived|GetPending' |
  Where-Object { $_.Path -match 'EmailIngestion' }
```

Expected: no matches. Also confirm the API logs contain no "Email ingestion background service started." line on startup.

## Automated Tests

```powershell
dotnet test
```

Coverage added by this feature:

- `tests/TripPlanner.Api.Tests/EmailIngestion/` — ingestion outcomes (parsed, no_content, duplicate, unknown_sender, too_large, unauthorized), sender-to-traveler resolution, attachment decoding and text extraction, dedupe hash behavior, and confirmation creating a tracked item.
- A guard test asserting the API registers no hosted service for email ingestion.

There were **no** existing tests for email ingestion before this feature, so these are the first regression net over this area.
