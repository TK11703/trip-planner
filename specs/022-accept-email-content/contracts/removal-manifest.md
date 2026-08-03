# Contract: Mailbox-Monitoring Removal Manifest

**Feature**: 022-accept-email-content

This manifest is the checkable definition of FR-021, FR-022, FR-023, and SC-008. Every row must be satisfied before the feature is complete.

## Must Be Deleted

| Artifact | Path | Why |
|---|---|---|
| `EmailIngestionBackgroundService` | `src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionBackgroundService.cs` | Timer loop that polls `inbox_emails` every 15s — the deferred-processing behavior being removed |
| Hosted service registration | `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs` → `AddHostedService<EmailIngestionBackgroundService>()` | Starts the loop |
| `ReceiveEmailWebhookEndpoint` | `src/TripPlanner.Api/Features/EmailIngestion/ReceiveEmailWebhookEndpoint.cs` | Event Grid envelope + validation handshake; also the source of the wrong-identity attribution |
| Webhook route registration | `src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionEndpointRouteBuilderExtensions.cs` → `webhookGroup.MapReceiveEmailWebhook()` | Removes the route and the separate webhook group |
| `DevInjectEmailEndpoint` | `src/TripPlanner.Api/Features/EmailIngestion/DevInjectEmailEndpoint.cs` | Existed only to compensate for the missing push path; masked the identity defect |
| Dev-inject registration | `EmailIngestionEndpointRouteBuilderExtensions.cs` → `if (environment.IsDevelopment()) group.MapDevInjectEmail();` | Route removal |
| `DevInjectEmailRequest` | `src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs` | Contract for a deleted endpoint |
| `IInboxEmailRepository.GetPendingAsync` + implementation | `src/TripPlanner.Database/EmailIngestion/` | Polling accessor |
| `GetPendingInboxEmails.sql` | `src/TripPlanner.Database/Scripts/Queries/EmailIngestion/` | Polling query |
| `inbox_emails_pending_idx` | via `Scripts/Schema/011_email_relay_ingestion.sql` | Index supporting polling only |
| `'pending'` in `parse_status` check | via `011_email_relay_ingestion.sql` | No deferred state remains |

## Must Be Replaced

| Artifact | Change |
|---|---|
| `EmailIngestionPolicy.WebhookPolicy` | Becomes `EmailIngestionPolicy.RelayPolicy` requiring the `EmailIngestion.Relay` app role, not merely `RequireAuthenticatedUser()` |
| `EmailParserService.ParseAsync` | Takes assembled text (subject + body + attachment text) and returns **zero or more** drafts; no longer takes an `InboxEmailRecord` fetched by a poller |
| `EmailDeduplicationService.ComputeHash` | Prefers `messageId`; falls back to `sender|subject|receivedAt` |

## Must Be Preserved

Removing the above must not regress these (FR-024):

- `GetDraftListEndpoint`, `UpdateDraftEndpoint`, `ConfirmDraftEndpoint`, `DiscardDraftEndpoint`
- `GetInboxHistoryEndpoint` and `ReprocessEmailEndpoint`
- `InboxDrafts.razor`, `InboxHistory.razor`, and the `Inbox` nav entry
- `parsed_event_drafts` schema and `IParsedEventDraftRepository`
- `EmailIngestionNotificationKeys` and the notification dispatched when drafts appear

> `ReprocessEmailEndpoint` currently re-queues a message by resetting `parse_status` to `'pending'` for the poller. Since `'pending'` is gone, it must be reworked to re-run recognition **synchronously** against the stored message and its attachments, returning the same outcome shape as ingestion.

## Verification

| Check | Method |
|---|---|
| No hosted service remains in the API | Source search for `AddHostedService`, `BackgroundService`, `IHostedService` under `src/TripPlanner.Api/**` returns no email-ingestion match |
| No mailbox protocol client exists | Source search for `Imap`, `Pop3`, `MailKit`, `EmailReceived`, `EventGrid` returns no match in `src/**` |
| No polling query remains | Source search for `GetPending` under `src/**` returns no email-ingestion match |
| No `'pending'` parse status | `011_email_relay_ingestion.sql` constraint permits only `parsed`/`failed`/`unsupported` |
| Review surface intact | Existing draft/history endpoints and pages still resolve and render |
