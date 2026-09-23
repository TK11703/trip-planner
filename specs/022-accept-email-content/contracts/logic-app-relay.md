# Contract: Logic App Relay Responsibilities

**Feature**: 022-accept-email-content

The relay is a Consumption Logic App defined in [infra/email-relay.bicep](../../../infra/email-relay.bicep) and deployed with the rest of the infrastructure. It is infrastructure rather than application code — nothing in this contract is implemented in the API or Web projects. This document records the boundary so the API is built against a stable expectation.

> This contract originally described the relay as living **outside this repository**. It has since been brought into the Bicep deployment, so what follows is deployed behavior, not an expectation of a third-party system.

The workflow is always deployed; `EMAIL_RELAY_ENABLED` controls only its `state`, so it can ship disabled at no cost until the mailbox connection is authorized. Operational steps are in [docs/operations/production-runbook.md](../../../docs/operations/production-runbook.md) §1.6.

## Division of Responsibility

| Concern | Owner |
|---|---|
| Connecting to and monitoring the mailbox | Logic App |
| Detecting new messages | Logic App |
| Decomposing the message into sender/subject/body/attachments | Logic App |
| Calling the ingestion endpoint | Logic App |
| Retry | Logic App (default HTTP retry policy) |
| Filing handled mail out of the polled folder | Logic App |
| Spam and malware filtering | Mail platform, ahead of the Logic App |
| Authorizing the caller | API |
| Matching sender to traveler | API |
| Duplicate detection | API |
| Recognition, draft creation, notification | API |
| Traveler review and confirmation | API + Web |

The API performs **no** mailbox connection, discovery, or polling of any kind.

## Expected Relay Flow

1. Trigger: *When a new email arrives* (`/v2/Mail/OnNewEmail`) on `mailFolderPath`, polled every `pollingIntervalMinutes`. `splitOn` fans a polled batch into one run per message, so a single poison message cannot block the rest.
2. Project the message into the ingestion payload — see [relay-ingestion-endpoint.md](relay-ingestion-endpoint.md).
   - `messageId` comes from the platform's `InternetMessageId`.
   - `sender` is the message's `From`. For a forwarded message this is the traveler's own address, which is what enables attribution. It is never derived from the caller's token.
   - Attachments are projected to `fileName` / `contentType` / `contentBase64`.
3. Acquire a token for `apiResourceUri` using the workflow's **user-assigned managed identity**.
4. `POST /api/email-ingestion/messages` with that bearer token.
5. Branch on the response `status`, and file the message out of the polled folder when it was handled.

| Response | Relay behavior |
|---|---|
| `200` `parsed` / `no_content` / `duplicate` | Move the message to `processedFolderPath` |
| `400` `invalid_request`, `413` `too_large`, `422` `unknown_sender` | Run fails; message stays in the polled folder for an operator |
| `502` `processing_failed` | Retried by the HTTP action's default policy, then the run fails |
| `401` / `403` | Alert — identity or app role misconfiguration |

> **Reading a 401 against a 403.** A missing `EmailIngestion.Relay` grant surfaces as `401 authentication_required`, not `403`. A token carrying neither `scp` nor `roles` fails Microsoft.Identity.Web validation (IDW10201) before the authorization policy runs. A `403 reauthentication_required` means the token was valid but under-privileged.

The move is hygiene, not correctness: the trigger watermark advances regardless, so an unmoved message is not re-relayed, and the API reports `duplicate` if one ever is.

## Connector Constraint

The relay uses the **Outlook.com** managed API (`outlook`), not Office 365 Outlook. The two are mutually exclusive on account type: `office365` requires an Exchange Online work mailbox and returns `401` on every call for a personal Microsoft account, even though consent succeeds and the connection reports `Connected`. Moving the mailbox to Microsoft 365 means switching the managed API, and with it the trigger path (`/v3/Mail/OnNewEmail`), the move path (`/v2/Mail/Move/...`), and the trigger field casing.

## Configuration Required Outside Bicep

| Requirement | How it is satisfied |
|---|---|
| `EmailIngestion.Relay` app role exposed on the API registration | [scripts/grant-relay-app-role.ps1](../../../scripts/grant-relay-app-role.ps1), run automatically as the azd `postprovision` hook |
| That role assigned to the relay's managed identity | Same script. There is no Azure portal path for assigning an app role to a managed identity |
| Mailbox connection authorized | **Manual.** OAuth consent cannot be scripted; an operator signs in to the connection once |
| `processedFolderPath` exists in the mailbox | **Manual.** The connector does not create it, and the move fails `404` after the API has already ingested |
| API base URL and target audience | Bicep parameters `apiUri` and `apiResourceUri` |

Attachment size limits on the relay are aligned to the API caps (5 MB per attachment, 10 MB per request).

## Non-Goals

- The API does not expose a subscription-validation handshake. That was an Event Grid concern and is removed.
- The API does not accept the Event Grid event envelope.
- The API does not provide a status-polling endpoint for the relay; every call returns a conclusive outcome.
